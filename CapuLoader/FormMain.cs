using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using CapuLoader.Internals;
using CapuLoader.Internals.SimpleJSON;

namespace CapuLoader;

public class FormMain : Form
{
	private enum RegSAM
	{
		QueryValue = 1,
		SetValue = 2,
		CreateSubKey = 4,
		EnumerateSubKeys = 8,
		Notify = 16,
		CreateLink = 32,
		WOW64_32Key = 512,
		WOW64_64Key = 256,
		WOW64_Res = 768,
		Read = 131097,
		Write = 131078,
		Execute = 131097,
		AllAccess = 983103
	}

	private static class RegHive
	{
		public static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(2147483650u);

		public static UIntPtr HKEY_CURRENT_USER = new UIntPtr(2147483649u);
	}

	private static class RegistryWOW6432
	{
		[DllImport("Advapi32.dll")]
		private static extern uint RegOpenKeyEx(UIntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out int phkResult);

		[DllImport("Advapi32.dll")]
		private static extern uint RegCloseKey(int hKey);

		[DllImport("advapi32.dll")]
		public static extern int RegQueryValueEx(int hKey, string lpValueName, int lpReserved, ref uint lpType, StringBuilder lpData, ref uint lpcbData);

		public static string GetRegKey64(UIntPtr inHive, string inKeyName, string inPropertyName)
		{
			return GetRegKey64(inHive, inKeyName, RegSAM.WOW64_64Key, inPropertyName);
		}

		public static string GetRegKey32(UIntPtr inHive, string inKeyName, string inPropertyName)
		{
			return GetRegKey64(inHive, inKeyName, RegSAM.WOW64_32Key, inPropertyName);
		}

		public static string GetRegKey64(UIntPtr inHive, string inKeyName, RegSAM in32or64key, string inPropertyName)
		{
			int phkResult = 0;
			try
			{
				if (RegOpenKeyEx(RegHive.HKEY_LOCAL_MACHINE, inKeyName, 0u, (int)(RegSAM.QueryValue | in32or64key), out phkResult) != 0)
				{
					return null;
				}
				uint lpType = 0u;
				uint lpcbData = 1024u;
				StringBuilder stringBuilder = new StringBuilder(1024);
				RegQueryValueEx(phkResult, inPropertyName, 0, ref lpType, stringBuilder, ref lpcbData);
				return stringBuilder.ToString();
			}
			finally
			{
				if (phkResult != 0)
				{
					RegCloseKey(phkResult);
				}
			}
		}
	}

	private const string BaseEndpoint = "https://api.github.com/repos/";

	private const short CurrentVersion = 1;

	private List<ReleaseInfo> releases;

	private Dictionary<string, int> groups = new Dictionary<string, int>();

	private string InstallDirectory = "";

	private bool modsDisabled;

	public bool isSteam = true;

	public bool platformDetected;

	public const int WM_NCLBUTTONDOWN = 161;

	public const int HT_CAPTION = 2;

	private CookieContainer PermCookie;

	private IContainer components;

	private TextBox textBoxDirectory;

	private Button buttonFolderBrowser;

	private Button buttonInstall;

	private Label labelStatus;

	private ContextMenuStrip contextMenuStripMain;

	private Button buttonModInfo;

	private Button buttonUninstallAll;

	private Button buttonBackupMods;

	private Button buttonRestoreMods;

	private Button buttonToggleMods;

	private Button buttonMods;

	private Button buttonOpenConfig;

	private Button buttonOpenGameFolder;

	public ListView listViewMods;

	private ColumnHeader columnHeaderName;

	private ColumnHeader columnHeaderAuthor;

	private ToolStripMenuItem viewInfoToolStripMenuItem;

	private ContextMenuStrip contextMenuStrip1;

	private ToolStripMenuItem quitToolStripMenuItem;

	private ToolStripMenuItem minimizeToolStripMenuItem;
    private Button closeButton;
    private Panel panel1;

	[DllImport("Gdi32.dll")]
	private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

	public FormMain()
	{
		InitializeComponent();
		base.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, base.Width, base.Height, 20, 20));
	}

	private void FormMain_Load(object sender, EventArgs e)
	{
		foreach (Button item in base.Controls.OfType<Button>())
		{
			item.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, item.Width, item.Height, 13, 13));
		}
		LocationHandler();
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		releases = new List<ReleaseInfo>();
		if (!File.Exists(Path.Combine(InstallDirectory, "winhttp.dll")))
		{
			if (File.Exists(Path.Combine(InstallDirectory, "mods.disable")))
			{
				buttonToggleMods.Text = "Enable Mods";
				modsDisabled = true;
				buttonToggleMods.Enabled = true;
			}
			else
			{
				buttonToggleMods.Enabled = false;
			}
		}
		else
		{
			buttonToggleMods.Enabled = true;
		}
		new Thread((ThreadStart)delegate
		{
			LoadRequiredPlugins();
		}).Start();
	}

	private void LoadReleases()
	{
		JSONNode jSONNode = JSON.Parse(DownloadSite("https://raw.githubusercontent.com/NoxGamesOfficial/CapuchinModManager/master/modinfo.json"));
		JSONNode jSONNode2 = JSON.Parse(DownloadSite("https://raw.githubusercontent.com/NoxGamesOfficial/CapuchinModManager/master/groupinfo.json"));
		JSONArray asArray = jSONNode.AsArray;
		JSONArray asArray2 = jSONNode2.AsArray;
		for (int i = 0; i < asArray.Count; i++)
		{
			JSONNode jSONNode3 = asArray[i];
			ReleaseInfo item = new ReleaseInfo(jSONNode3["name"], jSONNode3["author"], jSONNode3["version"], jSONNode3["group"], jSONNode3["download_url"], jSONNode3["install_location"], jSONNode3["git_path"], jSONNode3["dependencies"].AsArray);
			releases.Add(item);
		}
		asArray2.Linq.OrderBy((KeyValuePair<string, JSONNode> x) => x.Value["rank"]);
		for (int j = 0; j < asArray2.Count; j++)
		{
			JSONNode current = asArray2[j];
			if (releases.Any((ReleaseInfo x) => (JSONNode)x.Group == (object)current["name"]))
			{
				groups.Add(current["name"], groups.Count());
			}
		}
		groups.Add("Uncategorized", groups.Count());
		foreach (ReleaseInfo release in releases)
		{
			foreach (string dep in release.Dependencies)
			{
				releases.Where((ReleaseInfo x) => x.Name == dep).FirstOrDefault()?.Dependents.Add(release.Name);
			}
		}
	}

	private void LoadRequiredPlugins()
	{
		CheckVersion();
		UpdateStatus("Getting latest version info...");
		LoadReleases();
		Invoke((MethodInvoker)delegate
		{
			new Dictionary<string, int>();
			int i;
			for (i = 0; i < groups.Count(); i++)
			{
				string key = groups.First((KeyValuePair<string, int> x) => x.Value == i).Key;
				int value = listViewMods.Groups.Add(new ListViewGroup(key, HorizontalAlignment.Left));
				groups[key] = value;
			}
			foreach (ReleaseInfo release in releases)
			{
				ListViewItem listViewItem = new ListViewItem
				{
					BackColor = Color.FromArgb(28, 28, 28),
					ForeColor = Color.White,
					Text = release.Name
				};
				if (!string.IsNullOrEmpty(release.Version))
				{
					listViewItem.Text = release.Name + " - " + release.Version;
				}
				if (!string.IsNullOrEmpty(release.Tag))
				{
					listViewItem.Text = $"{release.Name} - ({release.Tag})";
				}
				listViewItem.SubItems.Add(release.Author);
				listViewItem.Tag = release;
				if (release.Install)
				{
					listViewMods.Items.Add(listViewItem);
				}
				CheckDefaultMod(release, listViewItem);
				if (release.Group == null || !groups.ContainsKey(release.Group))
				{
					listViewItem.Group = listViewMods.Groups[groups["Uncategorized"]];
				}
				else if (groups.ContainsKey(release.Group))
				{
					int index = groups[release.Group];
					listViewItem.Group = listViewMods.Groups[index];
				}
			}
			buttonInstall.Enabled = true;
		});
		UpdateStatus("Release info updated!");
	}

	private void UpdateReleaseInfo(ref ReleaseInfo release)
	{
		Thread.Sleep(100);
		string uRL = "https://api.github.com/repos/" + release.GitPath + "/releases";
		JSONNode jSONNode = JSON.Parse(DownloadSite(uRL))[0];
		release.Version = jSONNode["tag_name"];
		JSONNode jSONNode2 = jSONNode["assets"][release.ReleaseId];
		release.Link = jSONNode2["browser_download_url"];
		JSONNode jSONNode3 = jSONNode2["uploader"];
		if (release.Author.Equals(string.Empty))
		{
			release.Author = jSONNode3["login"];
		}
	}

	private void Install()
	{
		ChangeInstallButtonState(enabled: false);
		UpdateStatus("Starting install sequence...");
		foreach (ReleaseInfo release in releases)
		{
			if (!release.Install)
			{
				continue;
			}
			UpdateStatus($"Downloading...{release.Name}");
			byte[] array = DownloadFile(release.Link);
			UpdateStatus($"Installing...{release.Name}");
			string fileName = Path.GetFileName(release.Link);
			if (Path.GetExtension(fileName).Equals(".dll"))
			{
				string text;
				if (release.InstallLocation == null)
				{
					text = Path.Combine(InstallDirectory, "BepInEx\\plugins", Regex.Replace(release.Name, "\\s+", string.Empty));
					if (!Directory.Exists(text))
					{
						Directory.CreateDirectory(text);
					}
				}
				else
				{
					text = Path.Combine(InstallDirectory, release.InstallLocation);
				}
				File.WriteAllBytes(Path.Combine(text, fileName), array);
				string path = Path.Combine(InstallDirectory, "BepInEx\\plugins", fileName);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			else
			{
				UnzipFile(array, (release.InstallLocation != null) ? Path.Combine(InstallDirectory, release.InstallLocation) : InstallDirectory);
			}
			UpdateStatus($"Installed {release.Name}!");
		}
		UpdateStatus("Install complete!");
		ChangeInstallButtonState(enabled: true);
		Invoke((MethodInvoker)delegate
		{
			buttonToggleMods.Enabled = true;
		});
	}

	[DllImport("user32.dll")]
	public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	private void Form1_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(base.Handle, 161, 2, 0);
		}
	}

	private void buttonInstall_Click(object sender, EventArgs e)
	{
		new Thread((ThreadStart)delegate
		{
			Install();
		}).Start();
	}

	private void buttonFolderBrowser_Click(object sender, EventArgs e)
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.FileName = "Capuchin Executable";
		openFileDialog.Filter = "Exe Files (.exe)|*.exe|All Files (*.*)|*.*";
		openFileDialog.FilterIndex = 1;
		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			string fileName = openFileDialog.FileName;
			if (Path.GetFileName(fileName).Equals("Capuchin.exe") | Path.GetFileName(fileName).Equals("Capuchin.exe"))
			{
				InstallDirectory = Path.GetDirectoryName(fileName);
				textBoxDirectory.Text = InstallDirectory;
			}
			else
			{
				MessageBox.Show("That's not the Capuchin exectuable! please try again!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}
	}

	private void listViewMods_ItemChecked(object sender, ItemCheckedEventArgs e)
	{
		ReleaseInfo release = (ReleaseInfo)e.Item.Tag;
		if (release.Dependencies.Count > 0)
		{
			foreach (ListViewItem item in listViewMods.Items)
			{
				ReleaseInfo plugin = (ReleaseInfo)item.Tag;
				if (plugin.Name == release.Name || !release.Dependencies.Contains(plugin.Name))
				{
					continue;
				}
				if (e.Item.Checked)
				{
					item.Checked = true;
					continue;
				}
				release.Install = false;
				if (releases.Count((ReleaseInfo x) => plugin.Dependents.Contains(x.Name) && x.Install) <= 1)
				{
					item.Checked = false;
				}
			}
		}
		if (release.Dependents.Count > 0 && releases.Count((ReleaseInfo x) => release.Dependents.Contains(x.Name) && x.Install) > 0)
		{
			e.Item.Checked = true;
		}
		if (release.Name.Contains("BepInEx") || release.Name.Contains("Caputilla"))
		{
			e.Item.Checked = true;
		}
		release.Install = e.Item.Checked;
	}

	private void listViewMods_DoubleClick(object sender, EventArgs e)
	{
		OpenLinkFromRelease();
	}

	private void buttonModInfo_Click(object sender, EventArgs e)
	{
		OpenLinkFromRelease();
	}

	private void viewInfoToolStripMenuItem_Click(object sender, EventArgs e)
	{
		OpenLinkFromRelease();
	}

	private void clickQuit(object sender, EventArgs e)
	{
		Application.Exit();
	}

	private void clickMinimize(object sender, EventArgs e)
	{
		base.WindowState = FormWindowState.Minimized;
	}

	private void listViewMods_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
	{
		if (listViewMods.SelectedItems.Count > 0)
		{
			buttonModInfo.ForeColor = Color.White;
		}
		else
		{
			buttonModInfo.ForeColor = Color.LightGray;
		}
	}

	private void buttonUninstallAll_Click(object sender, EventArgs e)
	{
		if (MessageBox.Show("You are about to delete all your mods (including any saved data in your plugins). This cannot be undone!\n\nAre you sure you wish to continue?", "Confirm Delete", MessageBoxButtons.YesNo) != DialogResult.Yes)
		{
			return;
		}
		UpdateStatus("Uninstalling all mods");
		string path = Path.Combine(InstallDirectory, "BepInEx\\plugins");
		try
		{
			string[] directories = Directory.GetDirectories(path);
			for (int i = 0; i < directories.Length; i++)
			{
				Directory.Delete(directories[i], recursive: true);
			}
			directories = Directory.GetFiles(path);
			for (int i = 0; i < directories.Length; i++)
			{
				File.Delete(directories[i]);
			}
		}
		catch (Exception)
		{
			MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			UpdateStatus("Failed to uninstall mods.");
			return;
		}
		UpdateStatus("All mods uninstalled successfully!");
	}

	private void buttonBackupMods_Click(object sender, EventArgs e)
	{
		string sourceDirectoryName = Path.Combine(InstallDirectory, "BepInEx\\plugins");
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			InitialDirectory = InstallDirectory,
			FileName = "Mod Backup",
			Filter = "ZIP Folder (.zip)|*.zip",
			Title = "Save Mod Backup"
		};
		if (saveFileDialog.ShowDialog() != DialogResult.OK || !(saveFileDialog.FileName != ""))
		{
			return;
		}
		UpdateStatus("Backing up mods...");
		try
		{
			if (File.Exists(saveFileDialog.FileName))
			{
				File.Delete(saveFileDialog.FileName);
			}
			ZipFile.CreateFromDirectory(sourceDirectoryName, saveFileDialog.FileName);
		}
		catch (Exception)
		{
			MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			UpdateStatus("Failed to back up mods.");
			return;
		}
		UpdateStatus("Successfully backed up mods!");
	}

	private void buttonRestoreMods_Click(object sender, EventArgs e)
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.InitialDirectory = InstallDirectory;
		openFileDialog.FileName = "Mod Backup.zip";
		openFileDialog.Filter = "ZIP Folder (.zip)|*.zip";
		openFileDialog.FilterIndex = 1;
		if (openFileDialog.ShowDialog() != DialogResult.OK)
		{
			return;
		}
		if (!Path.GetExtension(openFileDialog.FileName).Equals(".zip", StringComparison.InvariantCultureIgnoreCase))
		{
			MessageBox.Show("Invalid file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			UpdateStatus("Failed to restore mods.");
			return;
		}
		string path = Path.Combine(InstallDirectory, "BepInEx\\plugins");
		try
		{
			UpdateStatus("Restoring mods...");
			using (ZipArchive zipArchive = ZipFile.OpenRead(openFileDialog.FileName))
			{
				foreach (ZipArchiveEntry entry in zipArchive.Entries)
				{
					string path2 = Path.Combine(InstallDirectory, "BepInEx\\plugins", Path.GetDirectoryName(entry.FullName));
					if (!Directory.Exists(path2))
					{
						Directory.CreateDirectory(path2);
					}
					entry.ExtractToFile(Path.Combine(path, entry.FullName), overwrite: true);
				}
			}
			UpdateStatus("Successfully restored mods!");
		}
		catch (Exception)
		{
			MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			UpdateStatus("Failed to restore mods.");
		}
	}

	private void buttonOpenGameFolder_Click(object sender, EventArgs e)
	{
		if (Directory.Exists(InstallDirectory))
		{
			Process.Start(InstallDirectory);
		}
	}

	private void buttonOpenConfigFolder_Click(object sender, EventArgs e)
	{
		string text = Path.Combine(InstallDirectory, "BepInEx\\config");
		if (Directory.Exists(text))
		{
			Process.Start(text);
		}
	}

	private void buttonOpenModsFolder_Click(object sender, EventArgs e)
	{
		string text = Path.Combine(InstallDirectory, "BepInEx\\plugins");
		if (Directory.Exists(text))
		{
			Process.Start(text);
		}
	}

	private void buttonOpenWiki_Click(object sender, EventArgs e)
	{
		Process.Start("https://Capuchinmodding.burrito.software/");
	}

	private void buttonDiscordLink_Click(object sender, EventArgs e)
	{
		Process.Start("https://discord.gg/monkemod");
	}

	private string DownloadSite(string URL)
	{
		try
		{
			if (PermCookie == null)
			{
				PermCookie = new CookieContainer();
			}
			HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(URL);
			obj.Method = "GET";
			obj.KeepAlive = true;
			obj.CookieContainer = PermCookie;
			obj.ContentType = "application/x-www-form-urlencoded";
			obj.Referer = "";
			obj.UserAgent = "Monke-Mod-Manager";
			obj.Proxy = null;
			StreamReader streamReader = new StreamReader(((HttpWebResponse)obj.GetResponse()).GetResponseStream());
			string result = streamReader.ReadToEnd();
			streamReader.Close();
			return result;
		}
		catch (Exception ex)
		{
			if (ex.Message.Contains("403"))
			{
				MessageBox.Show("Failed to update version info, GitHub has rate limited you, please check back in 15 - 30 minutes. If this problem persists, share this error to helpers in the modding discord:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			else
			{
				MessageBox.Show("Failed to update version info, please check your internet connection. If this problem persists, share this error to helpers in the modding discord:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			Process.GetCurrentProcess().Kill();
			return null;
		}
	}

	private void UnzipFile(byte[] data, string directory)
	{
		using MemoryStream stream = new MemoryStream(data);
		using Unzip unzip = new Unzip(stream);
		unzip.ExtractToDirectory(directory);
	}

	private byte[] DownloadFile(string url)
	{
		return new WebClient
		{
			Proxy = null
		}.DownloadData(url);
	}

	private void UpdateStatus(string status)
	{
		string formattedText = $"Status: {status}";
		Invoke((MethodInvoker)delegate
		{
			labelStatus.Text = formattedText;
		});
	}

	private void NotFoundHandler()
	{
		bool flag = false;
		while (!flag)
		{
			using OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.FileName = "Capuchin Executable";
			openFileDialog.Filter = "Exe Files (.exe)|*.exe|All Files (*.*)|*.*";
			openFileDialog.FilterIndex = 1;
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				string fileName = openFileDialog.FileName;
				if (Path.GetFileName(fileName).Equals("Capuchin.exe") | Path.GetFileName(fileName).Equals("Capuchin.exe"))
				{
					InstallDirectory = Path.GetDirectoryName(fileName);
					textBoxDirectory.Text = InstallDirectory;
					flag = true;
				}
				else
				{
					MessageBox.Show("That's not the Capuchin exectuable! please try again!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
			}
			else
			{
				Process.GetCurrentProcess().Kill();
			}
		}
	}

	private void CheckVersion()
	{
		UpdateStatus("Checking for updates...");
		if (Convert.ToInt16(DownloadSite("https://raw.githubusercontent.com/NoxGamesOfficial/CapuchinModManager/master/update.txt")) > 1)
		{
			Invoke((MethodInvoker)delegate
			{
				MessageBox.Show("Your version of the mod installer is outdated! Please download the new one!", "Update available!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				Process.Start("https://github.com/BzzzThe18th/CapuLoader/releases/latest");
				Process.GetCurrentProcess().Kill();
				Environment.Exit(0);
			});
		}
	}

	private void ChangeInstallButtonState(bool enabled)
	{
		Invoke((MethodInvoker)delegate
		{
			buttonInstall.Enabled = enabled;
		});
	}

	private void OpenLinkFromRelease()
	{
		if (listViewMods.SelectedItems.Count <= 0)
		{
			return;
		}
		foreach (ListViewItem selectedItem in listViewMods.SelectedItems)
		{
			ReleaseInfo releaseInfo = (ReleaseInfo)selectedItem.Tag;
			UpdateStatus("Opening GitHub page for " + releaseInfo.Name);
			Process.Start($"https://github.com/{releaseInfo.GitPath}");
		}
	}

	private void LocationHandler()
	{
		string steamLocation = GetSteamLocation();
		if (steamLocation != null && Directory.Exists(steamLocation) && (File.Exists(steamLocation + "\\Capuchin.exe") | File.Exists(steamLocation + "\\Capuchin.exe")))
		{
			textBoxDirectory.Text = steamLocation;
			InstallDirectory = steamLocation;
			platformDetected = true;
		}
		else
		{
			ShowErrorFindingDirectoryMessage();
		}
	}

	private void ShowErrorFindingDirectoryMessage()
	{
		MessageBox.Show("We couldn't seem to find your Capuchin installation, please press \"OK\" and point us to it", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		NotFoundHandler();
		base.TopMost = true;
	}

	private string GetSteamLocation()
	{
		string text = RegistryWOW6432.GetRegKey64(RegHive.HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 2767950", "InstallLocation");
		if (text != null)
		{
			text += "\\";
		}
		return text;
	}

	private void CheckDefaultMod(ReleaseInfo release, ListViewItem item)
	{
		if (release.Name.Contains("BepInEx") || release.Name.Contains("Caputilla"))
		{
			item.Checked = true;
			item.ForeColor = Color.LightGray;
		}
		else
		{
			release.Install = false;
		}
	}

	private void buttonToggleMods_Click(object sender, EventArgs e)
	{
		if (modsDisabled)
		{
			if (File.Exists(Path.Combine(InstallDirectory, "mods.disable")))
			{
				File.Move(Path.Combine(InstallDirectory, "mods.disable"), Path.Combine(InstallDirectory, "winhttp.dll"));
				buttonToggleMods.Text = "Disable Mods";
				buttonToggleMods.BackColor = Color.FromArgb(120, 0, 0);
				modsDisabled = false;
				UpdateStatus("Enabled mods!");
			}
		}
		else if (File.Exists(Path.Combine(InstallDirectory, "winhttp.dll")))
		{
			File.Move(Path.Combine(InstallDirectory, "winhttp.dll"), Path.Combine(InstallDirectory, "mods.disable"));
			buttonToggleMods.Text = "Enable Mods";
			buttonToggleMods.BackColor = Color.FromArgb(0, 120, 0);
			modsDisabled = true;
			UpdateStatus("Disabled mods!");
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.textBoxDirectory = new System.Windows.Forms.TextBox();
            this.buttonFolderBrowser = new System.Windows.Forms.Button();
            this.buttonInstall = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.contextMenuStripMain = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.viewInfoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonRestoreMods = new System.Windows.Forms.Button();
            this.buttonBackupMods = new System.Windows.Forms.Button();
            this.buttonUninstallAll = new System.Windows.Forms.Button();
            this.buttonModInfo = new System.Windows.Forms.Button();
            this.buttonToggleMods = new System.Windows.Forms.Button();
            this.buttonOpenGameFolder = new System.Windows.Forms.Button();
            this.buttonOpenConfig = new System.Windows.Forms.Button();
            this.buttonMods = new System.Windows.Forms.Button();
            this.listViewMods = new System.Windows.Forms.ListView();
            this.columnHeaderName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderAuthor = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.quitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.minimizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.closeButton = new System.Windows.Forms.Button();
            this.contextMenuStripMain.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // textBoxDirectory
            // 
            this.textBoxDirectory.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.textBoxDirectory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxDirectory.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBoxDirectory.Enabled = false;
            this.textBoxDirectory.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxDirectory.ForeColor = System.Drawing.Color.White;
            this.textBoxDirectory.Location = new System.Drawing.Point(12, 34);
            this.textBoxDirectory.Name = "textBoxDirectory";
            this.textBoxDirectory.Size = new System.Drawing.Size(497, 20);
            this.textBoxDirectory.TabIndex = 0;
            // 
            // buttonFolderBrowser
            // 
            this.buttonFolderBrowser.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.buttonFolderBrowser.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonFolderBrowser.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFolderBrowser.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonFolderBrowser.ForeColor = System.Drawing.Color.White;
            this.buttonFolderBrowser.Location = new System.Drawing.Point(515, 34);
            this.buttonFolderBrowser.Name = "buttonFolderBrowser";
            this.buttonFolderBrowser.Size = new System.Drawing.Size(26, 23);
            this.buttonFolderBrowser.TabIndex = 1;
            this.buttonFolderBrowser.Text = "..";
            this.buttonFolderBrowser.UseVisualStyleBackColor = false;
            this.buttonFolderBrowser.Click += new System.EventHandler(this.buttonFolderBrowser_Click);
            // 
            // buttonInstall
            // 
            this.buttonInstall.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonInstall.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonInstall.Enabled = false;
            this.buttonInstall.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonInstall.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonInstall.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonInstall.ForeColor = System.Drawing.Color.White;
            this.buttonInstall.Location = new System.Drawing.Point(12, 147);
            this.buttonInstall.Name = "buttonInstall";
            this.buttonInstall.Size = new System.Drawing.Size(112, 54);
            this.buttonInstall.TabIndex = 4;
            this.buttonInstall.Text = "Install / Update";
            this.buttonInstall.UseVisualStyleBackColor = false;
            this.buttonInstall.Click += new System.EventHandler(this.buttonInstall_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelStatus.AutoSize = true;
            this.labelStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelStatus.ForeColor = System.Drawing.Color.White;
            this.labelStatus.ImageAlign = System.Drawing.ContentAlignment.TopRight;
            this.labelStatus.Location = new System.Drawing.Point(12, 9);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(61, 13);
            this.labelStatus.TabIndex = 5;
            this.labelStatus.Text = "Status: Null";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.labelStatus.Click += new System.EventHandler(this.labelStatus_Click);
            // 
            // contextMenuStripMain
            // 
            this.contextMenuStripMain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.contextMenuStripMain.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.contextMenuStripMain.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F);
            this.contextMenuStripMain.ImageScalingSize = new System.Drawing.Size(0, 0);
            this.contextMenuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewInfoToolStripMenuItem});
            this.contextMenuStripMain.Name = "contextMenuStripMain";
            this.contextMenuStripMain.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.contextMenuStripMain.Size = new System.Drawing.Size(119, 26);
            // 
            // viewInfoToolStripMenuItem
            // 
            this.viewInfoToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.viewInfoToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.viewInfoToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            this.viewInfoToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.viewInfoToolStripMenuItem.Name = "viewInfoToolStripMenuItem";
            this.viewInfoToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            this.viewInfoToolStripMenuItem.Text = "View Info";
            this.viewInfoToolStripMenuItem.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.viewInfoToolStripMenuItem.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.viewInfoToolStripMenuItem.Click += new System.EventHandler(this.viewInfoToolStripMenuItem_Click);
            // 
            // buttonRestoreMods
            // 
            this.buttonRestoreMods.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonRestoreMods.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonRestoreMods.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonRestoreMods.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRestoreMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRestoreMods.ForeColor = System.Drawing.Color.White;
            this.buttonRestoreMods.Location = new System.Drawing.Point(12, 327);
            this.buttonRestoreMods.Name = "buttonRestoreMods";
            this.buttonRestoreMods.Size = new System.Drawing.Size(112, 45);
            this.buttonRestoreMods.TabIndex = 3;
            this.buttonRestoreMods.Text = "Restore Mods";
            this.buttonRestoreMods.UseVisualStyleBackColor = false;
            this.buttonRestoreMods.Click += new System.EventHandler(this.buttonRestoreMods_Click);
            // 
            // buttonBackupMods
            // 
            this.buttonBackupMods.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonBackupMods.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonBackupMods.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonBackupMods.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonBackupMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonBackupMods.ForeColor = System.Drawing.Color.White;
            this.buttonBackupMods.Location = new System.Drawing.Point(12, 267);
            this.buttonBackupMods.Name = "buttonBackupMods";
            this.buttonBackupMods.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonBackupMods.Size = new System.Drawing.Size(112, 54);
            this.buttonBackupMods.TabIndex = 1;
            this.buttonBackupMods.Text = "Backup Mods";
            this.buttonBackupMods.UseVisualStyleBackColor = false;
            this.buttonBackupMods.Click += new System.EventHandler(this.buttonBackupMods_Click);
            // 
            // buttonUninstallAll
            // 
            this.buttonUninstallAll.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonUninstallAll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonUninstallAll.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonUninstallAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonUninstallAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonUninstallAll.ForeColor = System.Drawing.Color.White;
            this.buttonUninstallAll.Location = new System.Drawing.Point(12, 207);
            this.buttonUninstallAll.Name = "buttonUninstallAll";
            this.buttonUninstallAll.Size = new System.Drawing.Size(112, 54);
            this.buttonUninstallAll.TabIndex = 0;
            this.buttonUninstallAll.Text = "Uninstall All Mods";
            this.buttonUninstallAll.UseVisualStyleBackColor = false;
            this.buttonUninstallAll.Click += new System.EventHandler(this.buttonUninstallAll_Click);
            // 
            // buttonModInfo
            // 
            this.buttonModInfo.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonModInfo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonModInfo.Enabled = false;
            this.buttonModInfo.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonModInfo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonModInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonModInfo.ForeColor = System.Drawing.Color.White;
            this.buttonModInfo.Location = new System.Drawing.Point(12, 87);
            this.buttonModInfo.Name = "buttonModInfo";
            this.buttonModInfo.Size = new System.Drawing.Size(112, 54);
            this.buttonModInfo.TabIndex = 9;
            this.buttonModInfo.Text = "View Mod Info";
            this.buttonModInfo.UseVisualStyleBackColor = false;
            this.buttonModInfo.Click += new System.EventHandler(this.buttonModInfo_Click);
            // 
            // buttonToggleMods
            // 
            this.buttonToggleMods.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonToggleMods.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.buttonToggleMods.Enabled = false;
            this.buttonToggleMods.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonToggleMods.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonToggleMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonToggleMods.ForeColor = System.Drawing.Color.White;
            this.buttonToggleMods.Location = new System.Drawing.Point(546, 34);
            this.buttonToggleMods.Name = "buttonToggleMods";
            this.buttonToggleMods.Size = new System.Drawing.Size(112, 47);
            this.buttonToggleMods.TabIndex = 10;
            this.buttonToggleMods.Text = "Disable Mods";
            this.buttonToggleMods.UseVisualStyleBackColor = false;
            this.buttonToggleMods.Click += new System.EventHandler(this.buttonToggleMods_Click);
            // 
            // buttonOpenGameFolder
            // 
            this.buttonOpenGameFolder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonOpenGameFolder.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonOpenGameFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonOpenGameFolder.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonOpenGameFolder.ForeColor = System.Drawing.Color.White;
            this.buttonOpenGameFolder.Location = new System.Drawing.Point(407, 60);
            this.buttonOpenGameFolder.Name = "buttonOpenGameFolder";
            this.buttonOpenGameFolder.Size = new System.Drawing.Size(134, 23);
            this.buttonOpenGameFolder.TabIndex = 5;
            this.buttonOpenGameFolder.Text = "Game Folder";
            this.buttonOpenGameFolder.UseVisualStyleBackColor = false;
            this.buttonOpenGameFolder.Click += new System.EventHandler(this.buttonOpenGameFolder_Click);
            // 
            // buttonOpenConfig
            // 
            this.buttonOpenConfig.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonOpenConfig.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonOpenConfig.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonOpenConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonOpenConfig.ForeColor = System.Drawing.Color.White;
            this.buttonOpenConfig.Location = new System.Drawing.Point(12, 60);
            this.buttonOpenConfig.Name = "buttonOpenConfig";
            this.buttonOpenConfig.Size = new System.Drawing.Size(134, 23);
            this.buttonOpenConfig.TabIndex = 5;
            this.buttonOpenConfig.Text = "Config Folder";
            this.buttonOpenConfig.UseVisualStyleBackColor = false;
            this.buttonOpenConfig.Click += new System.EventHandler(this.buttonOpenConfigFolder_Click);
            // 
            // buttonMods
            // 
            this.buttonMods.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.buttonMods.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buttonMods.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonMods.ForeColor = System.Drawing.Color.White;
            this.buttonMods.Location = new System.Drawing.Point(210, 60);
            this.buttonMods.Name = "buttonMods";
            this.buttonMods.Size = new System.Drawing.Size(134, 23);
            this.buttonMods.TabIndex = 5;
            this.buttonMods.Text = "Mods Folder";
            this.buttonMods.UseVisualStyleBackColor = false;
            this.buttonMods.Click += new System.EventHandler(this.buttonOpenModsFolder_Click);
            // 
            // listViewMods
            // 
            this.listViewMods.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this.listViewMods.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.listViewMods.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listViewMods.CheckBoxes = true;
            this.listViewMods.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderAuthor});
            this.listViewMods.ContextMenuStrip = this.contextMenuStripMain;
            this.listViewMods.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listViewMods.ForeColor = System.Drawing.Color.White;
            this.listViewMods.FullRowSelect = true;
            this.listViewMods.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewMods.HideSelection = false;
            this.listViewMods.Location = new System.Drawing.Point(130, 87);
            this.listViewMods.Name = "listViewMods";
            this.listViewMods.ShowItemToolTips = true;
            this.listViewMods.Size = new System.Drawing.Size(529, 280);
            this.listViewMods.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.listViewMods.TabIndex = 0;
            this.listViewMods.UseCompatibleStateImageBehavior = false;
            this.listViewMods.View = System.Windows.Forms.View.Details;
            this.listViewMods.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewMods_ItemChecked);
            this.listViewMods.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewMods_ItemSelectionChanged);
            this.listViewMods.SelectedIndexChanged += new System.EventHandler(this.listViewMods_SelectedIndexChanged);
            this.listViewMods.DoubleClick += new System.EventHandler(this.listViewMods_DoubleClick);
            // 
            // columnHeaderName
            // 
            this.columnHeaderName.Text = "Name";
            this.columnHeaderName.Width = 321;
            // 
            // columnHeaderAuthor
            // 
            this.columnHeaderAuthor.Text = "Author";
            this.columnHeaderAuthor.Width = 162;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.contextMenuStrip1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F);
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(0, 0);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quitToolStripMenuItem,
            this.minimizeToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.contextMenuStrip1.Size = new System.Drawing.Size(115, 48);
            // 
            // quitToolStripMenuItem
            // 
            this.quitToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.quitToolStripMenuItem.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.quitToolStripMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.quitToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            this.quitToolStripMenuItem.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            this.quitToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.quitToolStripMenuItem.Text = "Quit";
            this.quitToolStripMenuItem.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.clickQuit);
            // 
            // minimizeToolStripMenuItem
            // 
            this.minimizeToolStripMenuItem.ForeColor = System.Drawing.Color.White;
            this.minimizeToolStripMenuItem.Name = "minimizeToolStripMenuItem";
            this.minimizeToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.minimizeToolStripMenuItem.Text = "Minimize";
            this.minimizeToolStripMenuItem.Click += new System.EventHandler(this.clickMinimize);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(42)))), ((int)(((byte)(42)))));
            this.panel1.ContextMenuStrip = this.contextMenuStrip1;
            this.panel1.Cursor = System.Windows.Forms.Cursors.Default;
            this.panel1.ForeColor = System.Drawing.Color.White;
            this.panel1.Location = new System.Drawing.Point(130, 89);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(2, 275);
            this.panel1.TabIndex = 11;
            // 
            // closeButton
            // 
            this.closeButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.closeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.closeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.closeButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.closeButton.ForeColor = System.Drawing.Color.White;
            this.closeButton.Location = new System.Drawing.Point(609, 6);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(49, 22);
            this.closeButton.TabIndex = 12;
            this.closeButton.Text = "X";
            this.closeButton.UseVisualStyleBackColor = false;
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.ClientSize = new System.Drawing.Size(671, 383);
            this.ContextMenuStrip = this.contextMenuStrip1;
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.listViewMods);
            this.Controls.Add(this.buttonOpenConfig);
            this.Controls.Add(this.buttonMods);
            this.Controls.Add(this.buttonOpenGameFolder);
            this.Controls.Add(this.buttonBackupMods);
            this.Controls.Add(this.buttonRestoreMods);
            this.Controls.Add(this.buttonToggleMods);
            this.Controls.Add(this.buttonModInfo);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonUninstallAll);
            this.Controls.Add(this.buttonInstall);
            this.Controls.Add(this.buttonFolderBrowser);
            this.Controls.Add(this.textBoxDirectory);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FormMain";
            this.Opacity = 0.98D;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CapuLoader";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.contextMenuStripMain.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

	}

    private void listViewMods_SelectedIndexChanged(object sender, EventArgs e)
    {

    }

    private void labelStatus_Click(object sender, EventArgs e)
    {

    }

	private void closeButton_Click(object sender, EventArgs e) =>
		Application.Exit();
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CapuLoader.Internals;

internal class Unzip : IDisposable
{
	public class Entry
	{
		public string Name { get; set; }

		public string Comment { get; set; }

		public uint Crc32 { get; set; }

		public int CompressedSize { get; set; }

		public int OriginalSize { get; set; }

		public bool Deflated { get; set; }

		public bool IsDirectory => Name.EndsWith("/");

		public DateTime Timestamp { get; set; }

		public bool IsFile => !IsDirectory;

		[EditorBrowsable(EditorBrowsableState.Never)]
		public int HeaderOffset { get; set; }

		[EditorBrowsable(EditorBrowsableState.Never)]
		public int DataOffset { get; set; }
	}

	public class Crc32Calculator
	{
		private static readonly uint[] Crc32Table = new uint[256]
		{
			0u, 1996959894u, 3993919788u, 2567524794u, 124634137u, 1886057615u, 3915621685u, 2657392035u, 249268274u, 2044508324u,
			3772115230u, 2547177864u, 162941995u, 2125561021u, 3887607047u, 2428444049u, 498536548u, 1789927666u, 4089016648u, 2227061214u,
			450548861u, 1843258603u, 4107580753u, 2211677639u, 325883990u, 1684777152u, 4251122042u, 2321926636u, 335633487u, 1661365465u,
			4195302755u, 2366115317u, 997073096u, 1281953886u, 3579855332u, 2724688242u, 1006888145u, 1258607687u, 3524101629u, 2768942443u,
			901097722u, 1119000684u, 3686517206u, 2898065728u, 853044451u, 1172266101u, 3705015759u, 2882616665u, 651767980u, 1373503546u,
			3369554304u, 3218104598u, 565507253u, 1454621731u, 3485111705u, 3099436303u, 671266974u, 1594198024u, 3322730930u, 2970347812u,
			795835527u, 1483230225u, 3244367275u, 3060149565u, 1994146192u, 31158534u, 2563907772u, 4023717930u, 1907459465u, 112637215u,
			2680153253u, 3904427059u, 2013776290u, 251722036u, 2517215374u, 3775830040u, 2137656763u, 141376813u, 2439277719u, 3865271297u,
			1802195444u, 476864866u, 2238001368u, 4066508878u, 1812370925u, 453092731u, 2181625025u, 4111451223u, 1706088902u, 314042704u,
			2344532202u, 4240017532u, 1658658271u, 366619977u, 2362670323u, 4224994405u, 1303535960u, 984961486u, 2747007092u, 3569037538u,
			1256170817u, 1037604311u, 2765210733u, 3554079995u, 1131014506u, 879679996u, 2909243462u, 3663771856u, 1141124467u, 855842277u,
			2852801631u, 3708648649u, 1342533948u, 654459306u, 3188396048u, 3373015174u, 1466479909u, 544179635u, 3110523913u, 3462522015u,
			1591671054u, 702138776u, 2966460450u, 3352799412u, 1504918807u, 783551873u, 3082640443u, 3233442989u, 3988292384u, 2596254646u,
			62317068u, 1957810842u, 3939845945u, 2647816111u, 81470997u, 1943803523u, 3814918930u, 2489596804u, 225274430u, 2053790376u,
			3826175755u, 2466906013u, 167816743u, 2097651377u, 4027552580u, 2265490386u, 503444072u, 1762050814u, 4150417245u, 2154129355u,
			426522225u, 1852507879u, 4275313526u, 2312317920u, 282753626u, 1742555852u, 4189708143u, 2394877945u, 397917763u, 1622183637u,
			3604390888u, 2714866558u, 953729732u, 1340076626u, 3518719985u, 2797360999u, 1068828381u, 1219638859u, 3624741850u, 2936675148u,
			906185462u, 1090812512u, 3747672003u, 2825379669u, 829329135u, 1181335161u, 3412177804u, 3160834842u, 628085408u, 1382605366u,
			3423369109u, 3138078467u, 570562233u, 1426400815u, 3317316542u, 2998733608u, 733239954u, 1555261956u, 3268935591u, 3050360625u,
			752459403u, 1541320221u, 2607071920u, 3965973030u, 1969922972u, 40735498u, 2617837225u, 3943577151u, 1913087877u, 83908371u,
			2512341634u, 3803740692u, 2075208622u, 213261112u, 2463272603u, 3855990285u, 2094854071u, 198958881u, 2262029012u, 4057260610u,
			1759359992u, 534414190u, 2176718541u, 4139329115u, 1873836001u, 414664567u, 2282248934u, 4279200368u, 1711684554u, 285281116u,
			2405801727u, 4167216745u, 1634467795u, 376229701u, 2685067896u, 3608007406u, 1308918612u, 956543938u, 2808555105u, 3495958263u,
			1231636301u, 1047427035u, 2932959818u, 3654703836u, 1088359270u, 936918000u, 2847714899u, 3736837829u, 1202900863u, 817233897u,
			3183342108u, 3401237130u, 1404277552u, 615818150u, 3134207493u, 3453421203u, 1423857449u, 601450431u, 3009837614u, 3294710456u,
			1567103746u, 711928724u, 3020668471u, 3272380065u, 1510334235u, 755167117u
		};

		private uint crcValue = uint.MaxValue;

		public uint Crc32 => crcValue ^ 0xFFFFFFFFu;

		public void UpdateWithBlock(byte[] buffer, int numberOfBytes)
		{
			for (int i = 0; i < numberOfBytes; i++)
			{
				crcValue = (crcValue >> 8) ^ Crc32Table[buffer[i] ^ (crcValue & 0xFF)];
			}
		}
	}

	public class FileProgressEventArgs : ProgressChangedEventArgs
	{
		public int CurrentFile { get; private set; }

		public int TotalFiles { get; private set; }

		public string FileName { get; private set; }

		public FileProgressEventArgs(int currentFile, int totalFiles, string fileName)
			: base((totalFiles != 0) ? (currentFile * 100 / totalFiles) : 100, fileName)
		{
			CurrentFile = currentFile;
			TotalFiles = totalFiles;
			FileName = fileName;
		}
	}

	private const int EntrySignature = 33639248;

	private const int FileSignature = 67324752;

	private const int DirectorySignature = 101010256;

	private const int BufferSize = 16384;

	private Entry[] entries;

	private Stream Stream { get; set; }

	private BinaryReader Reader { get; set; }

	public IEnumerable<string> FileNames => from e in Entries
		select e.Name into f
		where !f.EndsWith("/")
		orderby f
		select f;

	public Entry[] Entries
	{
		get
		{
			if (entries == null)
			{
				entries = ReadZipEntries().ToArray();
			}
			return entries;
		}
	}

	public event EventHandler<FileProgressEventArgs> ExtractProgress;

	public Unzip(string fileName)
		: this(File.OpenRead(fileName))
	{
	}

	public Unzip(Stream stream)
	{
		Stream = stream;
		Reader = new BinaryReader(Stream);
	}

	public void Dispose()
	{
		if (Stream != null)
		{
			Stream.Dispose();
			Stream = null;
		}
		if (Reader != null)
		{
			Reader.Close();
			Reader = null;
		}
	}

	public void ExtractToDirectory(string directoryName)
	{
		for (int i = 0; i < Entries.Length; i++)
		{
			Entry entry = Entries[i];
			string text = Path.Combine(directoryName, entry.Name);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			if (!entry.IsDirectory)
			{
				Extract(entry.Name, text);
			}
			this.ExtractProgress?.Invoke(this, new FileProgressEventArgs(i + 1, Entries.Length, entry.Name));
		}
	}

	public void Extract(string fileName, string outputFileName)
	{
		Entry entry = GetEntry(fileName);
		using (FileStream outputStream = File.Create(outputFileName))
		{
			Extract(entry, outputStream);
		}
		FileInfo fileInfo = new FileInfo(outputFileName);
		if (fileInfo.Length != entry.OriginalSize)
		{
			throw new InvalidDataException($"Corrupted archive: {outputFileName} has an uncompressed size {fileInfo.Length} which does not match its expected size {entry.OriginalSize}");
		}
		File.SetLastWriteTime(outputFileName, entry.Timestamp);
	}

	private Entry GetEntry(string fileName)
	{
		fileName = fileName.Replace("\\", "/").Trim().TrimStart('/');
		return Entries.FirstOrDefault((Entry e) => e?.Name.Replace("\\", "/") == fileName) ?? throw new FileNotFoundException("File not found in the archive: " + fileName);
	}

	public void Extract(string fileName, Stream outputStream)
	{
		Extract(GetEntry(fileName), outputStream);
	}

	public void Extract(Entry entry, Stream outputStream)
	{
		Stream.Seek(entry.HeaderOffset, SeekOrigin.Begin);
		if (Reader.ReadInt32() != 67324752)
		{
			throw new InvalidDataException("File signature doesn't match.");
		}
		Stream.Seek(entry.DataOffset, SeekOrigin.Begin);
		Stream stream = Stream;
		if (entry.Deflated)
		{
			stream = new DeflateStream(Stream, CompressionMode.Decompress, leaveOpen: true);
		}
		int num = entry.OriginalSize;
		int num2 = Math.Min(16384, entry.OriginalSize);
		byte[] buffer = new byte[num2];
		Crc32Calculator crc32Calculator = new Crc32Calculator();
		while (num > 0)
		{
			int num3 = stream.Read(buffer, 0, num2);
			if (num3 == 0)
			{
				break;
			}
			crc32Calculator.UpdateWithBlock(buffer, num3);
			outputStream.Write(buffer, 0, num3);
			num -= num3;
		}
		if (crc32Calculator.Crc32 != entry.Crc32)
		{
			throw new InvalidDataException($"Corrupted archive: CRC32 doesn't match on file {entry.Name}: expected {entry.Crc32:x8}, got {crc32Calculator.Crc32:x8}.");
		}
	}

	private IEnumerable<Entry> ReadZipEntries()
	{
		if (Stream.Length < 22)
		{
			yield break;
		}
		Stream.Seek(-22L, SeekOrigin.End);
		while (Reader.ReadInt32() != 101010256)
		{
			if (Stream.Position <= 5)
			{
				yield break;
			}
			Stream.Seek(-5L, SeekOrigin.Current);
		}
		Stream.Seek(6L, SeekOrigin.Current);
		ushort entries = Reader.ReadUInt16();
		Reader.ReadInt32();
		uint num = Reader.ReadUInt32();
		Stream.Seek(num, SeekOrigin.Begin);
		for (int i = 0; i < entries; i++)
		{
			if (Reader.ReadInt32() == 33639248)
			{
				Reader.ReadInt32();
				bool num2 = (Reader.ReadInt16() & 0x800) != 0;
				short num3 = Reader.ReadInt16();
				int dosTimestamp = Reader.ReadInt32();
				uint crc = Reader.ReadUInt32();
				int compressedSize = Reader.ReadInt32();
				int originalSize = Reader.ReadInt32();
				short count = Reader.ReadInt16();
				short num4 = Reader.ReadInt16();
				short count2 = Reader.ReadInt16();
				Reader.ReadInt32();
				Reader.ReadInt32();
				int num5 = Reader.ReadInt32();
				byte[] bytes = Reader.ReadBytes(count);
				Stream.Seek(num4, SeekOrigin.Current);
				byte[] bytes2 = Reader.ReadBytes(count2);
				int dataOffset = CalculateFileDataOffset(num5);
				Encoding encoding = (num2 ? Encoding.UTF8 : Encoding.Default);
				yield return new Entry
				{
					Name = encoding.GetString(bytes),
					Comment = encoding.GetString(bytes2),
					Crc32 = crc,
					CompressedSize = compressedSize,
					OriginalSize = originalSize,
					HeaderOffset = num5,
					DataOffset = dataOffset,
					Deflated = (num3 == 8),
					Timestamp = ConvertToDateTime(dosTimestamp)
				};
			}
		}
	}

	private int CalculateFileDataOffset(int fileHeaderOffset)
	{
		long position = Stream.Position;
		Stream.Seek(fileHeaderOffset + 26, SeekOrigin.Begin);
		short num = Reader.ReadInt16();
		short num2 = Reader.ReadInt16();
		int result = (int)Stream.Position + num + num2;
		Stream.Seek(position, SeekOrigin.Begin);
		return result;
	}

	public static DateTime ConvertToDateTime(int dosTimestamp)
	{
		return new DateTime((dosTimestamp >> 25) + 1980, (dosTimestamp >> 21) & 0xF, (dosTimestamp >> 16) & 0x1F, (dosTimestamp >> 11) & 0x1F, (dosTimestamp >> 5) & 0x3F, (dosTimestamp & 0x1F) * 2);
	}
}

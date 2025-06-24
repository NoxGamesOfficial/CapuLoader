using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CapuLoader;

public class FormSelectPlatform : Form
{
	private new readonly FormMain Parent;

	private IContainer components;

	private RadioButton radioButtonSteam;

	private RadioButton radioButtonOculus;

	private Button buttonConfirm;

	public FormSelectPlatform(FormMain parent)
	{
		InitializeComponent();
		Parent = parent;
	}

	private void buttonConfirm_Click(object sender, EventArgs e)
	{
		Parent.platformDetected = true;
		if (radioButtonOculus.Checked)
		{
			Parent.isSteam = false;
		}
		Close();
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
		this.radioButtonSteam = new System.Windows.Forms.RadioButton();
		this.radioButtonOculus = new System.Windows.Forms.RadioButton();
		this.buttonConfirm = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.radioButtonSteam.AutoSize = true;
		this.radioButtonSteam.Location = new System.Drawing.Point(53, 12);
		this.radioButtonSteam.Name = "radioButtonSteam";
		this.radioButtonSteam.Size = new System.Drawing.Size(187, 17);
		this.radioButtonSteam.TabIndex = 0;
		this.radioButtonSteam.TabStop = true;
		this.radioButtonSteam.Text = "I purchased the game on Steam";
		this.radioButtonSteam.UseVisualStyleBackColor = true;
		this.radioButtonOculus.AutoSize = true;
		this.radioButtonOculus.Location = new System.Drawing.Point(25, 35);
		this.radioButtonOculus.Name = "radioButtonOculus";
		this.radioButtonOculus.Size = new System.Drawing.Size(242, 17);
		this.radioButtonOculus.TabIndex = 1;
		this.radioButtonOculus.TabStop = true;
		this.radioButtonOculus.Text = "I purchased the game on the Oculus Store";
		this.radioButtonOculus.UseVisualStyleBackColor = true;
		this.buttonConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
		this.buttonConfirm.Location = new System.Drawing.Point(109, 60);
		this.buttonConfirm.Name = "buttonConfirm";
		this.buttonConfirm.Size = new System.Drawing.Size(75, 23);
		this.buttonConfirm.TabIndex = 2;
		this.buttonConfirm.Text = "Confirm";
		this.buttonConfirm.UseVisualStyleBackColor = true;
		this.buttonConfirm.Click += new System.EventHandler(buttonConfirm_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(32, 32, 32);
		base.ClientSize = new System.Drawing.Size(292, 95);
		base.Controls.Add(this.buttonConfirm);
		base.Controls.Add(this.radioButtonOculus);
		base.Controls.Add(this.radioButtonSteam);
		this.Font = new System.Drawing.Font("Segoe UI", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.ForeColor = System.Drawing.Color.White;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FormSelectPlatform";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Please select your platform";
		base.TopMost = true;
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}

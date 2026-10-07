using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	public partial class AboutForm : global::System.Windows.Forms.Form
	{
		/// <summary>Releases the designer-managed components when <paramref name="disposing"/> is true, then the form itself.</summary>
		/// <param name="disposing">True when called from <see cref="System.ComponentModel.Component.Dispose()"/>; false from the finalizer.</param>
		[global::System.Diagnostics.DebuggerNonUserCode]
		protected override void Dispose(bool disposing)
		{
			try
			{
				bool flag = disposing && this.components != null;
				if (flag)
				{
					this.components.Dispose();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		}

		[global::System.Diagnostics.DebuggerStepThrough]
		private void InitializeComponent()
		{
			this.picIcon = new global::System.Windows.Forms.PictureBox();
			this.lblTitle = new global::System.Windows.Forms.Label();
			this.lblVersion = new global::System.Windows.Forms.Label();
			this.lblDescription = new global::System.Windows.Forms.Label();
			this.lblCopyright = new global::System.Windows.Forms.Label();
			this.btnClose = new global::System.Windows.Forms.Button();
			this.Label1 = new global::System.Windows.Forms.Label();
			this.Label2 = new global::System.Windows.Forms.Label();
			this.Label3 = new global::System.Windows.Forms.Label();
			this.Label4 = new global::System.Windows.Forms.Label();
			this.picPaypallQR = new global::System.Windows.Forms.PictureBox();
			this.Label5 = new global::System.Windows.Forms.Label();
			((global::System.ComponentModel.ISupportInitialize)this.picIcon).BeginInit();
			((global::System.ComponentModel.ISupportInitialize)this.picPaypallQR).BeginInit();
			base.SuspendLayout();
			this.picIcon.Location = new global::System.Drawing.Point(20, 20);
			this.picIcon.Name = "picIcon";
			this.picIcon.Size = new global::System.Drawing.Size(48, 48);
			this.picIcon.SizeMode = global::System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.picIcon.TabIndex = 0;
			this.picIcon.TabStop = false;
			this.lblTitle.AutoSize = true;
			this.lblTitle.Font = new global::System.Drawing.Font("Segoe UI", 14f, global::System.Drawing.FontStyle.Bold, global::System.Drawing.GraphicsUnit.Point, 0);
			this.lblTitle.Location = new global::System.Drawing.Point(82, 18);
			this.lblTitle.Name = "lblTitle";
			this.lblTitle.Size = new global::System.Drawing.Size(143, 25);
			this.lblTitle.TabIndex = 1;
			this.lblTitle.Text = "Wave Dude 2D";
			this.lblVersion.AutoSize = true;
			this.lblVersion.Location = new global::System.Drawing.Point(84, 54);
			this.lblVersion.Name = "lblVersion";
			this.lblVersion.Size = new global::System.Drawing.Size(90, 18);
			this.lblVersion.TabIndex = 2;
			this.lblVersion.Text = "Version 0.29";
			this.lblDescription.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Bold, global::System.Drawing.GraphicsUnit.Point, 0);
			this.lblDescription.Location = new global::System.Drawing.Point(15, 90);
			this.lblDescription.Name = "lblDescription";
			this.lblDescription.Size = new global::System.Drawing.Size(461, 28);
			this.lblDescription.TabIndex = 3;
			this.lblDescription.Text = "Build a scene and watch waves do their thing!";
			this.lblCopyright.AutoSize = true;
			this.lblCopyright.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.lblCopyright.Location = new global::System.Drawing.Point(15, 507);
			this.lblCopyright.Name = "lblCopyright";
			this.lblCopyright.Size = new global::System.Drawing.Size(71, 18);
			this.lblCopyright.TabIndex = 4;
			this.lblCopyright.Text = "Copyright";
			this.btnClose.Anchor = global::System.Windows.Forms.AnchorStyles.Bottom | global::System.Windows.Forms.AnchorStyles.Right;
			this.btnClose.DialogResult = global::System.Windows.Forms.DialogResult.OK;
			this.btnClose.Location = new global::System.Drawing.Point(319, 495);
			this.btnClose.Name = "btnClose";
			this.btnClose.Size = new global::System.Drawing.Size(90, 30);
			this.btnClose.TabIndex = 5;
			this.btnClose.Text = "OK";
			this.btnClose.UseVisualStyleBackColor = true;
			this.Label1.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.Label1.Location = new global::System.Drawing.Point(13, 254);
			this.Label1.Name = "Label1";
			this.Label1.Size = new global::System.Drawing.Size(392, 68);
			this.Label1.TabIndex = 3;
			this.Label1.Text = "If you use this software and found it useful, consider making a donation to support future projects.\r\n\r\n";
			this.Label2.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.Label2.Location = new global::System.Drawing.Point(15, 128);
			this.Label2.Name = "Label2";
			this.Label2.Size = new global::System.Drawing.Size(394, 58);
			this.Label2.TabIndex = 3;
			this.Label2.Text = "Developed by Huygens Optics / JJM Vleggaar with support of Claude Sonnet 5 (meaning Claude did most of the heavy lifting and I get the credits). ";
			this.Label3.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 12f, global::System.Drawing.FontStyle.Bold, global::System.Drawing.GraphicsUnit.Point, 0);
			this.Label3.Location = new global::System.Drawing.Point(12, 301);
			this.Label3.Name = "Label3";
			this.Label3.Size = new global::System.Drawing.Size(387, 46);
			this.Label3.TabIndex = 3;
			this.Label3.Text = "Patreon: www.patreon.com/c/huygens_optics";
			this.Label4.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 12f, global::System.Drawing.FontStyle.Bold, global::System.Drawing.GraphicsUnit.Point, 0);
			this.Label4.Location = new global::System.Drawing.Point(16, 341);
			this.Label4.Name = "Label4";
			this.Label4.Size = new global::System.Drawing.Size(81, 46);
			this.Label4.TabIndex = 3;
			this.Label4.Text = "Paypal:";
			this.picPaypallQR.Image = global::_2D_wave_sim_V01.My.Resources.Resources.QR_code;
			this.picPaypallQR.Location = new global::System.Drawing.Point(121, 341);
			this.picPaypallQR.Name = "picPaypallQR";
			this.picPaypallQR.Size = new global::System.Drawing.Size(139, 135);
			this.picPaypallQR.SizeMode = global::System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.picPaypallQR.TabIndex = 6;
			this.picPaypallQR.TabStop = false;
			this.Label5.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.Label5.Location = new global::System.Drawing.Point(15, 199);
			this.Label5.Name = "Label5";
			this.Label5.Size = new global::System.Drawing.Size(392, 42);
			this.Label5.TabIndex = 3;
			this.Label5.Text = "Send bugs or suggestions for improvement to : info@huygensoptics.com";
			base.AcceptButton = this.btnClose;
			base.CancelButton = this.btnClose;
			base.ClientSize = new global::System.Drawing.Size(429, 545);
			base.Controls.Add(this.picPaypallQR);
			base.Controls.Add(this.btnClose);
			base.Controls.Add(this.lblCopyright);
			base.Controls.Add(this.Label2);
			base.Controls.Add(this.Label4);
			base.Controls.Add(this.Label3);
			base.Controls.Add(this.Label5);
			base.Controls.Add(this.Label1);
			base.Controls.Add(this.lblDescription);
			base.Controls.Add(this.lblVersion);
			base.Controls.Add(this.lblTitle);
			base.Controls.Add(this.picIcon);
			this.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			base.FormBorderStyle = global::System.Windows.Forms.FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.Name = "AboutForm";
			base.ShowIcon = false;
			base.ShowInTaskbar = false;
			base.StartPosition = global::System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "About 2D Wave Dude ";
			((global::System.ComponentModel.ISupportInitialize)this.picIcon).EndInit();
			((global::System.ComponentModel.ISupportInitialize)this.picPaypallQR).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		private PictureBox picIcon;
		private Label lblTitle;
		private Label lblVersion;
		private Label lblDescription;
		private Label lblCopyright;
		private Button btnClose;
		private Label Label1;
		private Label Label2;
		private Label Label3;
		private Label Label4;
		private PictureBox picPaypallQR;
		private Label Label5;
		private global::System.ComponentModel.IContainer components;
	}
}

using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	public partial class HelpForm : global::System.Windows.Forms.Form
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
			this.rtbHelp = new global::System.Windows.Forms.RichTextBox();
			this.pnlButtons = new global::System.Windows.Forms.FlowLayoutPanel();
			this.btnClose = new global::System.Windows.Forms.Button();
			this.pnlButtons.SuspendLayout();
			base.SuspendLayout();
			this.rtbHelp.BackColor = global::System.Drawing.SystemColors.Window;
			this.rtbHelp.BorderStyle = global::System.Windows.Forms.BorderStyle.None;
			this.rtbHelp.Dock = global::System.Windows.Forms.DockStyle.Fill;
			this.rtbHelp.Font = new global::System.Drawing.Font("Arial", 12f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.rtbHelp.Location = new global::System.Drawing.Point(0, 0);
			this.rtbHelp.Margin = new global::System.Windows.Forms.Padding(12);
			this.rtbHelp.Name = "rtbHelp";
			this.rtbHelp.ReadOnly = true;
			this.rtbHelp.Size = new global::System.Drawing.Size(1039, 843);
			this.rtbHelp.TabIndex = 0;
			this.rtbHelp.Text = "";
			this.pnlButtons.Controls.Add(this.btnClose);
			this.pnlButtons.Dock = global::System.Windows.Forms.DockStyle.Bottom;
			this.pnlButtons.FlowDirection = global::System.Windows.Forms.FlowDirection.RightToLeft;
			this.pnlButtons.Location = new global::System.Drawing.Point(0, 843);
			this.pnlButtons.Name = "pnlButtons";
			this.pnlButtons.Padding = new global::System.Windows.Forms.Padding(8);
			this.pnlButtons.Size = new global::System.Drawing.Size(1039, 46);
			this.pnlButtons.TabIndex = 1;
			this.btnClose.DialogResult = global::System.Windows.Forms.DialogResult.OK;
			this.btnClose.Location = new global::System.Drawing.Point(930, 11);
			this.btnClose.Name = "btnClose";
			this.btnClose.Size = new global::System.Drawing.Size(90, 30);
			this.btnClose.TabIndex = 0;
			this.btnClose.Text = "Close";
			this.btnClose.UseVisualStyleBackColor = true;
			base.AcceptButton = this.btnClose;
			base.CancelButton = this.btnClose;
			base.ClientSize = new global::System.Drawing.Size(1039, 889);
			base.Controls.Add(this.rtbHelp);
			base.Controls.Add(this.pnlButtons);
			base.MinimizeBox = false;
			this.MinimumSize = new global::System.Drawing.Size(560, 400);
			base.Name = "HelpForm";
			base.ShowIcon = false;
			base.ShowInTaskbar = false;
			base.StartPosition = global::System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Help - Wave Maker 2D";
			this.pnlButtons.ResumeLayout(false);
			base.ResumeLayout(false);
		}

		private RichTextBox rtbHelp;
		private FlowLayoutPanel pnlButtons;
		private Button btnClose;
		private global::System.ComponentModel.IContainer components;
	}
}

using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	public partial class frmDescription : global::System.Windows.Forms.Form
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
			this.TextBox1 = new global::System.Windows.Forms.TextBox();
			this.btnSaveDescription = new global::System.Windows.Forms.Button();
			this.btnCancelDescription = new global::System.Windows.Forms.Button();
			base.SuspendLayout();
			this.TextBox1.BorderStyle = global::System.Windows.Forms.BorderStyle.FixedSingle;
			this.TextBox1.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.TextBox1.Location = new global::System.Drawing.Point(22, 23);
			this.TextBox1.Multiline = true;
			this.TextBox1.Name = "TextBox1";
			this.TextBox1.Size = new global::System.Drawing.Size(662, 267);
			this.TextBox1.TabIndex = 0;
			this.btnSaveDescription.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.btnSaveDescription.Location = new global::System.Drawing.Point(575, 305);
			this.btnSaveDescription.Name = "btnSaveDescription";
			this.btnSaveDescription.Size = new global::System.Drawing.Size(109, 32);
			this.btnSaveDescription.TabIndex = 1;
			this.btnSaveDescription.Text = "OK";
			this.btnSaveDescription.UseVisualStyleBackColor = true;
			this.btnCancelDescription.Font = new global::System.Drawing.Font("Microsoft Sans Serif", 11.25f, global::System.Drawing.FontStyle.Regular, global::System.Drawing.GraphicsUnit.Point, 0);
			this.btnCancelDescription.Location = new global::System.Drawing.Point(460, 305);
			this.btnCancelDescription.Name = "btnCancelDescription";
			this.btnCancelDescription.Size = new global::System.Drawing.Size(109, 32);
			this.btnCancelDescription.TabIndex = 1;
			this.btnCancelDescription.Text = "Cancel";
			this.btnCancelDescription.UseVisualStyleBackColor = true;
			// event wiring (was VB WithEvents/Handles)
			this.btnSaveDescription.Click += new EventHandler(this.btnSaveDescription_Click);
			this.btnCancelDescription.Click += new EventHandler(this.btnCancelDescription_Click);
			base.AutoScaleDimensions = new global::System.Drawing.SizeF(6f, 13f);
			base.AutoScaleMode = global::System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new global::System.Drawing.Size(705, 349);
			base.Controls.Add(this.btnCancelDescription);
			base.Controls.Add(this.btnSaveDescription);
			base.Controls.Add(this.TextBox1);
			base.Name = "frmDescription";
			this.Text = "Edit project description";
			base.ResumeLayout(false);
			base.PerformLayout();
		}

		private TextBox TextBox1;
		private Button btnSaveDescription;
		private Button btnCancelDescription;
		private global::System.ComponentModel.IContainer components;
	}
}

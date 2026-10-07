using System;
using System.Drawing;
using System.Windows.Forms;
using _2D_wave_sim_V01.My;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// "About" dialog: shows the display version, the assembly copyright and the program icon.
	/// Opened modally from the Info menu of <see cref="Form1"/>.
	/// </summary>
	public partial class AboutForm : Form
	{
		/// <summary>
		/// Fills in the version and copyright labels and the icon associated with the running
		/// executable. The icon is optional: any failure to extract it is swallowed and the
		/// picture box stays empty.
		/// </summary>
		public AboutForm()
		{
			base.Load += this.AboutForm_Load;
			this.InitializeComponent();
			this.lblVersion.Text = "Version " + DisplayVersion;
			this.lblCopyright.Text = MyProject.Application.Info.Copyright;
			try
			{
				using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
				{
					if (icon != null)
					{
						this.picIcon.Image = icon.ToBitmap();
					}
				}
			}
			catch
			{
			}
		}

		/// <summary>Does nothing; still wired to <see cref="Form.Load"/> by the constructor.</summary>
		private void AboutForm_Load(object sender, EventArgs e)
		{
		}

		/// <summary>Version text shown in the dialog. A hand-maintained literal, not derived from the assembly version.</summary>
		private const string DisplayVersion = "0.27";
	}
}

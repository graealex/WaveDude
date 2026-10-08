using System;
using System.Drawing;
using System.Windows.Forms;
using _2D_wave_sim_V01.My;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The Info &gt; About window: program name, version and a one-paragraph description.
	/// Designer-editable: the layout lives in <c>AboutForm.Designer.cs</c>; this file only fills in
	/// the two pieces of text that are not fixed at design time (the version, from
	/// <see cref="DisplayVersion"/>, and the copyright string, read from the assembly) and loads the
	/// application icon. Opened modally from <see cref="Form1.AboutToolStripMenuItem_Click"/>.
	/// </summary>
	public partial class AboutForm : Form
	{
		/// <summary>
		/// Fills in the version and copyright labels and the icon associated with the running
		/// executable. The icon is optional: when none is available (e.g. running outside a normal
		/// exe) the failure is swallowed and the picture box just stays empty.
		/// </summary>
		public AboutForm()
		{
			base.Load += AboutForm_Load;
			InitializeComponent();
			lblVersion.Text = "Version " + DisplayVersion;
			lblCopyright.Text = MyProject.Application.Info.Copyright;
			try
			{
				using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
				{
					if (icon != null)
					{
						picIcon.Image = icon.ToBitmap();
					}
				}
			}
			catch
			{
				// no icon available (e.g. running outside a normal exe) - the picture box just stays empty
			}
		}

		/// <summary>Does nothing; still wired to <see cref="Form.Load"/> by the constructor.</summary>
		private void AboutForm_Load(object sender, EventArgs e)
		{
		}

		/// <summary>
		/// Version text shown in the dialog. A hand-maintained literal, meant to be kept in sync by
		/// hand with the version in <see cref="Form1"/>'s title bar ("Wave Dude 2D   V0.29", set in
		/// <c>Form1.Designer.cs</c>), since <c>AssemblyInfo.cs</c>'s <c>AssemblyVersion</c> is left at
		/// the generic 1.0.0.0 and does not track it. It currently lags behind the title bar.
		/// </summary>
		private const string DisplayVersion = "0.27";
	}
}

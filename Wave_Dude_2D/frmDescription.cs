using System;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Small modal dialog for editing a project's free-text description. <see cref="Form1"/>
	/// opens it from the "Description" button, pre-fills <see cref="DescriptionText"/> and reads
	/// it back when the dialog closes with <see cref="DialogResult.OK"/>.
	/// </summary>
	public partial class frmDescription : Form
	{
		/// <summary>Creates the dialog with an empty description.</summary>
		public frmDescription()
		{
			this.InitializeComponent();
		}

		/// <summary>Gets or sets the description text shown in the text box.</summary>
		public string DescriptionText
		{
			get
			{
				return this.TextBox1.Text;
			}
			set
			{
				this.TextBox1.Text = value;
			}
		}

		/// <summary>Closes the dialog with <see cref="DialogResult.OK"/>; the edited text stays in <see cref="DescriptionText"/>.</summary>
		private void btnSaveDescription_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.OK;
			base.Close();
		}

		/// <summary>Closes the dialog with <see cref="DialogResult.Cancel"/>; the caller discards the edit.</summary>
		private void btnCancelDescription_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.Cancel;
			base.Close();
		}
	}
}

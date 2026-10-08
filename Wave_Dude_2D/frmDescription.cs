using System;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Small modal dialog that lets the user type or edit a free-form description of the current
	/// project. Opened modally from <see cref="Form1.btnDescription_Click"/>, which sets
	/// <see cref="DescriptionText"/> before <c>ShowDialog</c> and reads it back (and saves it to the
	/// project) only when the dialog returns <see cref="DialogResult.OK"/>
	/// (<see cref="btnSaveDescription_Click"/>); <see cref="btnCancelDescription_Click"/> discards
	/// any typing.
	/// </summary>
	public partial class frmDescription : Form
	{
		/// <summary>Creates the dialog with an empty description.</summary>
		public frmDescription()
		{
			InitializeComponent();
		}

		/// <summary>The text being edited (the text box contents). <see cref="Form1"/> sets this before showing the dialog and reads it back after a <see cref="DialogResult.OK"/>.</summary>
		public string DescriptionText
		{
			get => TextBox1.Text;
			set => TextBox1.Text = value;
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

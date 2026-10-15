using System.Windows.Controls;
using System.Windows.Input;
using WaveDudeNext.Model;

namespace WaveDudeNext.WPF.Views;

/// <summary>Editor for the <see cref="Project"/> root: version, seed and description.</summary>
public partial class ProjectView : UserControl
{
	/// <summary>Draws a new random seed for the project in the data context.</summary>
	public static readonly RoutedUICommand Reseed = new("New seed", nameof(Reseed), typeof(ProjectView));

	public ProjectView()
	{
		InitializeComponent();
	}

	private void OnReseed(object sender, ExecutedRoutedEventArgs e) => (DataContext as Project)?.Reseed();
}

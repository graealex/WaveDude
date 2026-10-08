using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WaveDude.WPF.Panes;

/// <summary>
/// The Properties pane: shows the editor view that matches the type of
/// <see cref="SelectedObject"/>, mapped by the data templates in the XAML, or a hint when
/// nothing is selected.
/// </summary>
public partial class PropertiesPane : UserControl
{
	/// <summary>Identifies the <see cref="SelectedObject"/> property.</summary>
	public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
		nameof(SelectedObject), typeof(object), typeof(PropertiesPane));

	public PropertiesPane()
	{
		InitializeComponent();
	}

	/// <summary>The model object to edit, or null for the empty hint.</summary>
	public object? SelectedObject
	{
		get => GetValue(SelectedObjectProperty);
		set => SetValue(SelectedObjectProperty, value);
	}

	/// <summary>Puts the caret into the first text box of the current view once it is built, selecting its text.</summary>
	public void FocusFirstEditor() =>
		Dispatcher.InvokeAsync(() =>
		{
			if (FirstDescendant<TextBox>(this) is { } box)
			{
				box.Focus();
				box.SelectAll();
			}
		}, DispatcherPriority.Background);

	private static T? FirstDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		int count = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
			{
				return match;
			}
			if (FirstDescendant<T>(child) is { } nested)
			{
				return nested;
			}
		}
		return null;
	}
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WaveDudeNext.Model;
using WaveDudeNext.Rendering;
using WaveDudeNext.WPF.Panes;
using WaveDudeNext.WPF.Rendering;

namespace WaveDudeNext.WPF.Views;

/// <summary>
/// Editor of one project colour scheme (2026-10-13): name, kind, category, the gradient, and one row per
/// stop with its position (0..1), a swatch and the colour as <c>#RRGGBB</c>; stops can be added (in the
/// widest gap) and removed, and a sampled 255-colour table can be reduced to the author's nine. Edits
/// reach the preview through the project watcher.
/// </summary>
public partial class ColorSchemeView : UserControl
{
	private bool _applyingBase;

	public ColorSchemeView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => FillBasedOn();
		Loaded += (_, _) => FillBasedOn();
	}

	private ColorScheme? Scheme => DataContext as ColorScheme;

	/// <summary>The "based on" choices: the shared schemes (user folder, built-in) and the project's other schemes.</summary>
	private void FillBasedOn()
	{
		_applyingBase = true;
		try
		{
			var choices = new List<ColorScheme>();
			Project? project = FindPane()?.Project;
			if (project != null)
			{
				choices.AddRange(project.ColorSchemes.Where(s => !ReferenceEquals(s, Scheme)));
			}
			choices.AddRange(ColorSchemeCatalog.Instance.Schemes);
			basedOn.ItemsSource = choices;
			basedOn.SelectedItem = null;
		}
		finally
		{
			_applyingBase = false;
		}
	}

	private PropertiesPane? FindPane()
	{
		for (DependencyObject? d = this; d != null; d = VisualTreeHelper.GetParent(d))
		{
			if (d is PropertiesPane pane)
			{
				return pane;
			}
		}
		return null;
	}

	/// <summary>Copies the chosen scheme into this one (stops, kind, category, description); the choice stays shown until the next pick.</summary>
	private void OnBasedOnChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_applyingBase || Scheme is not { } scheme || basedOn.SelectedItem is not ColorScheme chosen)
		{
			return;
		}
		scheme.SetStops(chosen.Stops);
		scheme.Kind = chosen.Kind;
		scheme.Description = "copy of scheme '" + chosen.Name + "'";
	}

	private void CanReduce(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Scheme is { IsStopScheme: false };

	private void OnReduce(object sender, ExecutedRoutedEventArgs e) => Scheme?.ReduceToStops();

	private void CanAddStop(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Scheme != null;

	private void OnAddStop(object sender, ExecutedRoutedEventArgs e) => Scheme?.AddStop();

	private void CanRemoveStop(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Scheme is { } scheme && e.Parameter is ColorStop && scheme.Stops.Count > 1;

	private void OnRemoveStop(object sender, ExecutedRoutedEventArgs e)
	{
		if (e.Parameter is ColorStop stop)
		{
			Scheme?.RemoveStop(stop);
		}
	}
}

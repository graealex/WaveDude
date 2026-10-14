using System.Windows;
using WaveDude.Rendering;
using System.Windows.Controls;

namespace WaveDude.WPF.Views;

/// <summary>Editor for one <see cref="WaveDude.Model.DisplayLayer"/>; used twice by <see cref="DisplayView"/>.</summary>
public partial class DisplayLayerView : UserControl
{
	/// <summary>Identifies the <see cref="Kind"/> property.</summary>
	public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
		nameof(Kind), typeof(ColorSchemeKind), typeof(DisplayLayerView), new PropertyMetadata(ColorSchemeKind.Unspecified));

	/// <summary>Which output this view edits (set by the display view), so the scheme picker offers the matching schemes.</summary>
	public ColorSchemeKind Kind
	{
		get => (ColorSchemeKind)GetValue(KindProperty);
		set => SetValue(KindProperty, value);
	}

	public DisplayLayerView()
	{
		InitializeComponent();
	}
}

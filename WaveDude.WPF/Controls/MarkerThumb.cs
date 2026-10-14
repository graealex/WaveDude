using System.Windows;
using System.Windows.Controls.Primitives;

namespace WaveDude.WPF.Controls;

/// <summary>What a <see cref="MarkerThumb"/> stands for.</summary>
public enum MarkerRole
{
	/// <summary>The position of a source (its centre for a composite source).</summary>
	Source,
	/// <summary>The rotation handle of a composite source: sits on the array's row axis and turns it (2026-10-14).</summary>
	SourceAngle,
	/// <summary>Point A of a probe (the point of a point probe, the start of a line probe).</summary>
	ProbeA,
	/// <summary>Point B of a line probe.</summary>
	ProbeB,
}

/// <summary>
/// One draggable handle of the <see cref="SceneOverlay"/>: a <see cref="Thumb"/> that knows which model
/// object and which of its points it moves. Focusable, so the arrow keys can move it too; the overlay
/// does the geometry and the model updates. UI thread.
/// </summary>
public sealed class MarkerThumb : Thumb
{
	/// <summary>Identifies the <see cref="IsSelectedMarker"/> property.</summary>
	public static readonly DependencyProperty IsSelectedMarkerProperty = DependencyProperty.Register(
		nameof(IsSelectedMarker), typeof(bool), typeof(MarkerThumb), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="StrokeThickness"/> property.</summary>
	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
		nameof(StrokeThickness), typeof(double), typeof(MarkerThumb),
		new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => d.SetValue(HaloThicknessKey, (double)e.NewValue + HaloWidth)));

	private static readonly DependencyPropertyKey HaloThicknessKey = DependencyProperty.RegisterReadOnly(
		nameof(HaloThickness), typeof(double), typeof(MarkerThumb), new FrameworkPropertyMetadata(1.5 + HaloWidth, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="HaloThickness"/> property.</summary>
	public static readonly DependencyProperty HaloThicknessProperty = HaloThicknessKey.DependencyProperty;

	/// <summary>How far the dark halo sticks out on each side of a stroke, in pixels, so the glyph reads on any picture.</summary>
	public const double HaloWidth = 2.0;

	/// <param name="item">The source or probe.</param>
	/// <param name="role">Which point of it.</param>
	public MarkerThumb(object item, MarkerRole role)
	{
		Item = item;
		Role = role;
		Focusable = true;
	}

	/// <summary>The source or probe this handle moves.</summary>
	public object Item { get; }

	/// <summary>Which point of the item.</summary>
	public MarkerRole Role { get; }

	/// <summary>Whether the item is the one selected in the project; the style highlights it.</summary>
	public bool IsSelectedMarker
	{
		get => (bool)GetValue(IsSelectedMarkerProperty);
		set => SetValue(IsSelectedMarkerProperty, value);
	}

	/// <summary>Outline width of the glyph in pixels; the style thickens it on hover and focus.</summary>
	public double StrokeThickness
	{
		get => (double)GetValue(StrokeThicknessProperty);
		set => SetValue(StrokeThicknessProperty, value);
	}

	/// <summary>Width of the dark outline drawn under the glyph: <see cref="StrokeThickness"/> plus <see cref="HaloWidth"/> on each side.</summary>
	public double HaloThickness => (double)GetValue(HaloThicknessProperty);

	/// <summary>Model coordinate of the point when the current drag started (canvas pixels).</summary>
	internal int DragStartX { get; set; }

	/// <summary>Model coordinate of the point when the current drag started (canvas pixels).</summary>
	internal int DragStartY { get; set; }

	/// <summary>Mouse movement since the drag started, in canvas pixels (fractional, so slow drags add up).</summary>
	internal double DragDeltaX { get; set; }

	/// <summary>Mouse movement since the drag started, in canvas pixels.</summary>
	internal double DragDeltaY { get; set; }
}

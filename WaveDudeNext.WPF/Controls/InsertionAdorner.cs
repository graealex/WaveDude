using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WaveDudeNext.WPF.Controls;

/// <summary>
/// Drop feedback for reordering: a horizontal line above or below the adorned element, or an
/// outline around it when dropping appends to a folder.
/// </summary>
public sealed class InsertionAdorner : Adorner
{
	private readonly Pen _pen;

	/// <param name="adorned">The row the feedback is drawn over.</param>
	/// <param name="brush">Colour of the line or outline.</param>
	/// <param name="placement">Where to draw relative to the row.</param>
	public InsertionAdorner(UIElement adorned, Brush brush, InsertionPlacement placement) : base(adorned)
	{
		_pen = new Pen(brush, 2);
		_pen.Freeze();
		Placement = placement;
		IsHitTestVisible = false;
	}

	/// <summary>Where the feedback is drawn.</summary>
	public InsertionPlacement Placement { get; }

	/// <inheritdoc/>
	protected override void OnRender(DrawingContext drawingContext)
	{
		Size size = AdornedElement.RenderSize;
		switch (Placement)
		{
			case InsertionPlacement.Before:
				drawingContext.DrawLine(_pen, new Point(0, 1), new Point(size.Width, 1));
				break;
			case InsertionPlacement.After:
				drawingContext.DrawLine(_pen, new Point(0, size.Height - 1), new Point(size.Width, size.Height - 1));
				break;
			default:
				drawingContext.DrawRectangle(null, _pen, new Rect(1, 1, Math.Max(0, size.Width - 2), Math.Max(0, size.Height - 2)));
				break;
		}
	}
}

/// <summary>Position of the drop feedback relative to the adorned row.</summary>
public enum InsertionPlacement
{
	/// <summary>Line along the top edge: insert before this item.</summary>
	Before,
	/// <summary>Line along the bottom edge: insert after this item.</summary>
	After,
	/// <summary>Outline: append to this folder.</summary>
	Into,
}

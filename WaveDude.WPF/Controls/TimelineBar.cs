using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;

namespace WaveDude.WPF.Controls;

/// <summary>
/// The time axis of a run, in frames: a tall scrub bar with tick labels, a playhead with its
/// frame number, an in/out range drawn as a band between two brackets, and the frame under the
/// mouse. Click or drag scrubs; dragging a bracket asks for a new in or out point through
/// <see cref="SetRangeStartCommand"/> / <see cref="SetRangeEndCommand"/> (the range itself is
/// owned by the model, so the control never writes <see cref="RangeStart"/> or
/// <see cref="RangeEnd"/>). Keyboard: Left/Right one frame, PageUp/PageDown ten, Home/End to
/// the in/out points, the wheel one frame. Drawn in <see cref="OnRender"/>; colours come from
/// the brush properties so a theme style can set them.
/// </summary>
public sealed class TimelineBar : FrameworkElement
{
	/// <summary>Height of the label row above the bar, in pixels.</summary>
	public const double LabelRowHeight = 14.0;

	/// <summary>Default height of the whole control, in pixels.</summary>
	public const double DefaultHeight = 36.0;

	/// <summary>Pixels a tick label needs at least, which picks the labelled tick interval.</summary>
	private const double MinLabelSpacing = 64.0;

	/// <summary>Half-width of the hit area of a range bracket, in pixels.</summary>
	private const double BracketHitHalfWidth = 6.0;

	private static readonly int[] TickCandidates = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000];
	private static readonly Typeface LabelTypeface = new("Segoe UI");

	/// <summary>Identifies the <see cref="Maximum"/> property: the last frame index.</summary>
	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
		nameof(Maximum), typeof(int), typeof(TimelineBar),
		new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnMaximumChanged));

	/// <summary>Identifies the <see cref="Value"/> property: the frame shown.</summary>
	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
		nameof(Value), typeof(int), typeof(TimelineBar),
		new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
			OnValueChanged, CoerceValue));

	/// <summary>Identifies the <see cref="RangeStart"/> property: the in point, in frames.</summary>
	public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(
		nameof(RangeStart), typeof(int), typeof(TimelineBar), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="RangeEnd"/> property: the out point, in frames.</summary>
	public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(
		nameof(RangeEnd), typeof(int), typeof(TimelineBar), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="SetRangeStartCommand"/> property.</summary>
	public static readonly DependencyProperty SetRangeStartCommandProperty = DependencyProperty.Register(
		nameof(SetRangeStartCommand), typeof(ICommand), typeof(TimelineBar));

	/// <summary>Identifies the <see cref="SetRangeEndCommand"/> property.</summary>
	public static readonly DependencyProperty SetRangeEndCommandProperty = DependencyProperty.Register(
		nameof(SetRangeEndCommand), typeof(ICommand), typeof(TimelineBar));

	/// <summary>Identifies the <see cref="Background"/> property: fill of the bar.</summary>
	public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
		nameof(Background), typeof(Brush), typeof(TimelineBar), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="Foreground"/> property: playhead and its label.</summary>
	public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
		nameof(Foreground), typeof(Brush), typeof(TimelineBar), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="TickBrush"/> property: ticks, bar border and hover line.</summary>
	public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(
		nameof(TickBrush), typeof(Brush), typeof(TimelineBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="LabelBrush"/> property: tick labels and the hover label.</summary>
	public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
		nameof(LabelBrush), typeof(Brush), typeof(TimelineBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="AccentBrush"/> property: range band, brackets and keyboard focus.</summary>
	public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
		nameof(AccentBrush), typeof(Brush), typeof(TimelineBar), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

	private enum DragMode { None, Scrub, RangeStart, RangeEnd }

	private DragMode _drag;
	private int? _hoverFrame;

	public TimelineBar()
	{
		Focusable = true;
		FocusVisualStyle = null;
		SnapsToDevicePixels = true;
		Height = DefaultHeight;
	}

	/// <summary>Last frame index; 0 means a single frame and nothing to scrub.</summary>
	public int Maximum
	{
		get => (int)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	/// <summary>Frame shown, 0..<see cref="Maximum"/>.</summary>
	public int Value
	{
		get => (int)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	/// <summary>In point of the range, in frames.</summary>
	public int RangeStart
	{
		get => (int)GetValue(RangeStartProperty);
		set => SetValue(RangeStartProperty, value);
	}

	/// <summary>Out point of the range, in frames.</summary>
	public int RangeEnd
	{
		get => (int)GetValue(RangeEndProperty);
		set => SetValue(RangeEndProperty, value);
	}

	/// <summary>Executed with the frame (int) when the in bracket is dragged.</summary>
	public ICommand? SetRangeStartCommand
	{
		get => (ICommand?)GetValue(SetRangeStartCommandProperty);
		set => SetValue(SetRangeStartCommandProperty, value);
	}

	/// <summary>Executed with the frame (int) when the out bracket is dragged.</summary>
	public ICommand? SetRangeEndCommand
	{
		get => (ICommand?)GetValue(SetRangeEndCommandProperty);
		set => SetValue(SetRangeEndCommandProperty, value);
	}

	/// <summary>Fill of the bar.</summary>
	public Brush Background
	{
		get => (Brush)GetValue(BackgroundProperty);
		set => SetValue(BackgroundProperty, value);
	}

	/// <summary>Playhead and its frame label.</summary>
	public Brush Foreground
	{
		get => (Brush)GetValue(ForegroundProperty);
		set => SetValue(ForegroundProperty, value);
	}

	/// <summary>Ticks, the bar's border and the hover line.</summary>
	public Brush TickBrush
	{
		get => (Brush)GetValue(TickBrushProperty);
		set => SetValue(TickBrushProperty, value);
	}

	/// <summary>Tick labels and the hover label.</summary>
	public Brush LabelBrush
	{
		get => (Brush)GetValue(LabelBrushProperty);
		set => SetValue(LabelBrushProperty, value);
	}

	/// <summary>Range band and brackets, keyboard focus border.</summary>
	public Brush AccentBrush
	{
		get => (Brush)GetValue(AccentBrushProperty);
		set => SetValue(AccentBrushProperty, value);
	}

	private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => d.CoerceValue(ValueProperty);

	private static object CoerceValue(DependencyObject d, object value) => Math.Clamp((int)value, 0, ((TimelineBar)d).Maximum);

	private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (UIElementAutomationPeer.FromElement((TimelineBar)d) is TimelineBarAutomationPeer peer)
		{
			peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)(int)e.OldValue, (double)(int)e.NewValue);
		}
	}

	/// <inheritdoc/>
	protected override AutomationPeer OnCreateAutomationPeer() => new TimelineBarAutomationPeer(this);

	/// <inheritdoc/>
	protected override Size MeasureOverride(Size availableSize) =>
		new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, DefaultHeight);

	// ---------------------------------------------------------------- geometry

	/// <summary>Usable track: one pixel inset at both ends so the playhead at 0 and at Maximum is visible.</summary>
	private double TrackLeft => 1.0;
	private double TrackWidth => Math.Max(1.0, ActualWidth - 2.0);
	private double BarTop => LabelRowHeight;
	private double BarHeight => Math.Max(1.0, ActualHeight - LabelRowHeight);

	/// <summary>Pixel x of a frame's centre line.</summary>
	private double XOf(int frame) => Maximum > 0 ? TrackLeft + frame * TrackWidth / Maximum : TrackLeft;

	/// <summary>Frame nearest to a pixel x, clamped.</summary>
	private int FrameAt(double x) =>
		Maximum > 0 ? Math.Clamp((int)Math.Round((x - TrackLeft) / TrackWidth * Maximum), 0, Maximum) : 0;

	/// <summary>Which bracket, if any, a pixel x is on; the nearer one wins when both are close.</summary>
	private DragMode BracketAt(double x)
	{
		double dStart = Math.Abs(x - XOf(RangeStart));
		double dEnd = Math.Abs(x - XOf(RangeEnd));
		if (dStart <= BracketHitHalfWidth && dStart <= dEnd)
		{
			return DragMode.RangeStart;
		}
		return dEnd <= BracketHitHalfWidth ? DragMode.RangeEnd : DragMode.None;
	}

	// ---------------------------------------------------------------- rendering

	/// <inheritdoc/>
	protected override void OnRender(DrawingContext dc)
	{
		double w = ActualWidth;
		double h = ActualHeight;
		if (w <= 2 || h <= LabelRowHeight)
		{
			return;
		}
		var pen = new Pen(TickBrush, 1.0);
		var bar = new Rect(0.5, BarTop + 0.5, w - 1.0, BarHeight - 1.0);
		dc.DrawRectangle(Background, pen, bar);

		// the in/out range: two brackets, plus a translucent band once it is narrower than the run
		double x0 = Math.Round(XOf(RangeStart)) + 0.5;
		double x1 = Math.Round(XOf(RangeEnd)) + 0.5;
		if (x1 > x0 && (RangeStart > 0 || RangeEnd < Maximum))
		{
			dc.PushOpacity(0.25);
			dc.DrawRectangle(AccentBrush, null, new Rect(x0, bar.Top + 1, x1 - x0, bar.Height - 2));
			dc.Pop();
		}
		DrawBracket(dc, x0, bar, open: true);
		DrawBracket(dc, x1, bar, open: false);

		// ticks and labels; the labelled interval is the smallest candidate that leaves room for its text
		if (Maximum > 0)
		{
			double pxPerFrame = TrackWidth / Maximum;
			int major = TickCandidates[^1];
			foreach (int candidate in TickCandidates)
			{
				if (candidate * pxPerFrame >= MinLabelSpacing)
				{
					major = candidate;
					break;
				}
			}
			int minor = major >= 10 ? major / 5 : major >= 2 ? 1 : 0;
			if (minor > 0)
			{
				for (int f = 0; f <= Maximum; f += minor)
				{
					if (f % major != 0)
					{
						double x = Math.Round(XOf(f)) + 0.5;
						dc.DrawLine(pen, new Point(x, bar.Top), new Point(x, bar.Top + 3));
					}
				}
			}
			for (int f = 0; f <= Maximum; f += major)
			{
				double x = Math.Round(XOf(f)) + 0.5;
				dc.DrawLine(pen, new Point(x, bar.Top), new Point(x, bar.Top + 6));
				FormattedText label = Label(f.ToString(CultureInfo.CurrentCulture), LabelBrush);
				double lx = Math.Min(x + 2, w - label.Width - 1);
				if (lx + label.Width <= Math.Round(XOf(Value)) - 4 || lx >= Math.Round(XOf(Value)) + 4 + PlayheadLabelWidth())
				{
					dc.DrawText(label, new Point(lx, 0));
				}
			}
		}

		// the frame under the mouse
		if (_hoverFrame is int hover && _drag == DragMode.None && hover != Value)
		{
			double x = Math.Round(XOf(hover)) + 0.5;
			dc.PushOpacity(0.5);
			dc.DrawLine(pen, new Point(x, bar.Top), new Point(x, bar.Bottom));
			dc.Pop();
		}

		// the playhead: a line through the bar, a handle on top, the frame number next to it
		double px = Math.Round(XOf(Value)) + 0.5;
		dc.DrawLine(new Pen(Foreground, 1.0), new Point(px, bar.Top), new Point(px, bar.Bottom));
		var handle = new StreamGeometry();
		using (StreamGeometryContext g = handle.Open())
		{
			g.BeginFigure(new Point(px - 5, bar.Top - 6), true, true);
			g.LineTo(new Point(px + 5, bar.Top - 6), false, false);
			g.LineTo(new Point(px, bar.Top), false, false);
		}
		dc.DrawGeometry(Foreground, null, handle);
		FormattedText playhead = Label(Value.ToString(CultureInfo.CurrentCulture), Foreground, bold: true);
		double tx = px + 8 + playhead.Width <= w ? px + 8 : px - 8 - playhead.Width;
		dc.DrawText(playhead, new Point(tx, 0));

		if (IsKeyboardFocused)
		{
			dc.DrawRectangle(null, new Pen(AccentBrush, 1.0), new Rect(0.5, 0.5, w - 1, h - 1));
		}
	}

	private double PlayheadLabelWidth() => Label(Value.ToString(CultureInfo.CurrentCulture), Foreground, bold: true).Width + 8;

	/// <summary>A bracket at x: a 2 px vertical bar with feet pointing into the range.</summary>
	private void DrawBracket(DrawingContext dc, double x, Rect bar, bool open)
	{
		double foot = open ? 4 : -4;
		double left = open ? x - 0.5 : x - 1.5;
		dc.DrawRectangle(AccentBrush, null, new Rect(left, bar.Top, 2, bar.Height));
		dc.DrawRectangle(AccentBrush, null, new Rect(Math.Min(left, left + foot), bar.Top, Math.Abs(foot) + 2, 2));
		dc.DrawRectangle(AccentBrush, null, new Rect(Math.Min(left, left + foot), bar.Bottom - 2, Math.Abs(foot) + 2, 2));
	}

	private FormattedText Label(string text, Brush brush, bool bold = false) =>
		new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
			bold ? new Typeface(LabelTypeface.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal) : LabelTypeface,
			11.0, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

	// ---------------------------------------------------------------- mouse

	/// <inheritdoc/>
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		Focus();
		double x = e.GetPosition(this).X;
		_drag = BracketAt(x) switch
		{
			DragMode.RangeStart => DragMode.RangeStart,
			DragMode.RangeEnd => DragMode.RangeEnd,
			_ => DragMode.Scrub,
		};
		ApplyDrag(x);
		CaptureMouse();
		e.Handled = true;
	}

	/// <inheritdoc/>
	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		double x = e.GetPosition(this).X;
		if (_drag != DragMode.None)
		{
			ApplyDrag(x);
			return;
		}
		_hoverFrame = FrameAt(x);
		Cursor = BracketAt(x) == DragMode.None ? Cursors.Arrow : Cursors.SizeWE;
		InvalidateVisual();
	}

	/// <inheritdoc/>
	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (_drag != DragMode.None)
		{
			_drag = DragMode.None;
			ReleaseMouseCapture();
			InvalidateVisual();
			e.Handled = true;
		}
	}

	/// <inheritdoc/>
	protected override void OnMouseLeave(MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		_hoverFrame = null;
		InvalidateVisual();
	}

	/// <inheritdoc/>
	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		Value += e.Delta > 0 ? 1 : -1;
		e.Handled = true;
	}

	private void ApplyDrag(double x)
	{
		int frame = FrameAt(x);
		switch (_drag)
		{
			case DragMode.Scrub:
				Value = frame;
				break;
			case DragMode.RangeStart:
				Execute(SetRangeStartCommand, frame);
				break;
			case DragMode.RangeEnd:
				Execute(SetRangeEndCommand, frame);
				break;
		}
	}

	private void Execute(ICommand? command, int frame)
	{
		if (command is RoutedCommand routed)
		{
			if (routed.CanExecute(frame, this))
			{
				routed.Execute(frame, this);
			}
		}
		else if (command?.CanExecute(frame) == true)
		{
			command.Execute(frame);
		}
	}

	// ---------------------------------------------------------------- keyboard

	/// <inheritdoc/>
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		int? target = e.Key switch
		{
			Key.Left => Value - 1,
			Key.Right => Value + 1,
			Key.PageDown => Value - 10,
			Key.PageUp => Value + 10,
			Key.Home => RangeStart,
			Key.End => RangeEnd,
			_ => null,
		};
		if (target is int frame)
		{
			Value = frame;
			e.Handled = true;
		}
	}

	/// <inheritdoc/>
	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnGotKeyboardFocus(e);
		InvalidateVisual();
	}

	/// <inheritdoc/>
	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnLostKeyboardFocus(e);
		InvalidateVisual();
	}
}

/// <summary>Exposes a <see cref="TimelineBar"/> to UI Automation as a slider with a range value.</summary>
public sealed class TimelineBarAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
	public TimelineBarAutomationPeer(TimelineBar owner) : base(owner)
	{
	}

	private TimelineBar Bar => (TimelineBar)Owner;

	/// <inheritdoc/>
	protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

	/// <inheritdoc/>
	protected override string GetClassNameCore() => nameof(TimelineBar);

	/// <inheritdoc/>
	protected override bool IsContentElementCore() => true;

	/// <inheritdoc/>
	protected override bool IsControlElementCore() => true;

	/// <inheritdoc/>
	public override object? GetPattern(PatternInterface patternInterface) =>
		patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

	/// <inheritdoc/>
	public bool IsReadOnly => false;

	/// <inheritdoc/>
	public double Value => Bar.Value;

	/// <inheritdoc/>
	public double Minimum => 0;

	/// <inheritdoc/>
	public double Maximum => Bar.Maximum;

	/// <inheritdoc/>
	public double SmallChange => 1;

	/// <inheritdoc/>
	public double LargeChange => 10;

	/// <inheritdoc/>
	public void SetValue(double value) => Bar.Value = (int)Math.Round(value);
}

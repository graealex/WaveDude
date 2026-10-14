using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WaveDude.Model;
using WaveDude.Probes;
using WaveDude.Runs;

namespace WaveDude.WPF.Controls;

/// <summary>
/// The graph boxes of the probes on one picture (the probes of this <see cref="Layer"/>), drawn over the
/// frame as the author's app did: a translucent dark box with axes, the curve and a title strip with the
/// live readout, placed where the probe's <see cref="Probe.BoxX"/>/<see cref="Probe.BoxY"/> say (canvas
/// pixels, mapped with the picture's uniform scale like <see cref="SceneOverlay"/>). The graph is as live as
/// the picture: while the picture shows a scratch frame (<see cref="Scratch"/>: no run, a stale one, or one
/// that has not got there) the data is that frame's recording; otherwise it is the run's recording as it
/// stood at <see cref="Frame"/> (the series cut off at that step, the profile of that frame), redrawn on
/// every probe batch. With only the probes edited since the run, the picture keeps the stored frame and the
/// pane hands this overlay the scratch frame computed for the position all the same. A stale run's data is shown while the pane shows its stored frame: the graph follows
/// the picture, whatever is on it, instead of dropping to an empty axis until the scratch frame arrives
/// (that was a flicker on every scrub step, 2026-10-14). Each box is a draggable <see cref="MarkerThumb"/> (moves the box; the arrow keys nudge it) with a
/// second thumb at its bottom-right corner that resizes it, snapping to the fixed sizes
/// (<see cref="ProbeGraph.Snap"/>) or making it <see cref="ProbeBoxSize.Custom"/>. Grabbing or focusing a box
/// selects its probe. The geometry comes from <see cref="ProbeGraph"/>; this class only draws. UI thread.
/// </summary>
public sealed class ProbeGraphOverlay : Panel
{
	/// <summary>Identifies the <see cref="Project"/> property.</summary>
	public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register(
		nameof(Project), typeof(Project), typeof(ProbeGraphOverlay), new PropertyMetadata(null, OnProjectChanged));

	/// <summary>Identifies the <see cref="Run"/> property.</summary>
	public static readonly DependencyProperty RunProperty = DependencyProperty.Register(
		nameof(Run), typeof(SimulationRun), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRunChanged));

	/// <summary>Identifies the <see cref="Scratch"/> property.</summary>
	public static readonly DependencyProperty ScratchProperty = DependencyProperty.Register(
		nameof(Scratch), typeof(ScratchResult), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="Frame"/> property.</summary>
	public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
		nameof(Frame), typeof(int), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="Layer"/> property.</summary>
	public static readonly DependencyProperty LayerProperty = DependencyProperty.Register(
		nameof(Layer), typeof(ProbeLayer), typeof(ProbeGraphOverlay), new PropertyMetadata(ProbeLayer.Field, (d, _) => ((ProbeGraphOverlay)d).Rebuild()));

	/// <summary>Identifies the <see cref="CanvasWidth"/> property.</summary>
	public static readonly DependencyProperty CanvasWidthProperty = DependencyProperty.Register(
		nameof(CanvasWidth), typeof(int), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="CanvasHeight"/> property.</summary>
	public static readonly DependencyProperty CanvasHeightProperty = DependencyProperty.Register(
		nameof(CanvasHeight), typeof(int), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="SelectedItem"/> property.</summary>
	public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
		nameof(SelectedItem), typeof(object), typeof(ProbeGraphOverlay),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ProbeGraphOverlay)d).UpdateSelection()));

	/// <summary>Identifies the <see cref="BoxStyle"/> property.</summary>
	public static readonly DependencyProperty BoxStyleProperty = DependencyProperty.Register(
		nameof(BoxStyle), typeof(Style), typeof(ProbeGraphOverlay), new PropertyMetadata(null, (d, _) => ((ProbeGraphOverlay)d).Rebuild()));

	/// <summary>Identifies the <see cref="CornerStyle"/> property.</summary>
	public static readonly DependencyProperty CornerStyleProperty = DependencyProperty.Register(
		nameof(CornerStyle), typeof(Style), typeof(ProbeGraphOverlay), new PropertyMetadata(null, (d, _) => ((ProbeGraphOverlay)d).Rebuild()));

	/// <summary>Identifies the <see cref="AccentBrush"/> property.</summary>
	public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
		nameof(AccentBrush), typeof(Brush), typeof(ProbeGraphOverlay), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Pixels a focused box moves or grows per arrow key, and with Shift.</summary>
	private const int KeyStep = 1;
	private const int KeyStepFast = 10;

	/// <summary>Screen distance within which a dragged anchor (the box's, or the probe's point A) snaps the box onto the probe and attaches it.</summary>
	public const double SnapRadius = 14.0;

	/// <summary>Screen distance an attached box must be dragged from the probe before it comes off: wider than <see cref="SnapRadius"/>, so a drag near the edge does not attach and detach in turn (hysteresis, 2026-10-14).</summary>
	public const double SnapRelease = 28.0;

	// the author's colours: the box sits on the picture, not on the window, so they do not follow the theme
	private static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromArgb(235, 14, 16, 22)));
	private static readonly Pen BorderPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(170, 200, 200, 210))), 1.0));
	private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255))), 1.0));
	private static readonly Pen ZeroPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))), 1.0));
	private static readonly Pen FramePen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(130, 200, 200, 210))), 1.0));
	private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromArgb(225, 225, 225, 230)));
	private static readonly Brush TitleBrush = Freeze(new SolidColorBrush(Color.FromArgb(240, 240, 240, 245)));
	private static readonly Brush CurveBrush = Freeze(new SolidColorBrush(Color.FromArgb(255, 255, 208, 64)));
	private static readonly Typeface GraphTypeface = new("Segoe UI");

	/// <summary>Font height of the labels and the title in screen pixels: the handle labels' size, whatever the box size.</summary>
	private const double GraphFontSize = 11.0;

	/// <summary>The text metrics handed to the layout, built per DPI.</summary>
	private ProbeGraphStyle? _style;
	private double _stylePixelsPerDip;

	private readonly HashSet<INotifyPropertyChanged> _subscribed = [];

	public ProbeGraphOverlay()
	{
		// no background: clicks between the boxes fall through to the picture (and the pane's focus handling)
		IsHitTestVisible = true;
		Focusable = false;
	}

	/// <summary>The project whose probes are shown; null shows nothing.</summary>
	public Project? Project
	{
		get => (Project?)GetValue(ProjectProperty);
		set => SetValue(ProjectProperty, value);
	}

	/// <summary>The run whose recordings are drawn; null (or a stale run) draws the boxes without data.</summary>
	public SimulationRun? Run
	{
		get => (SimulationRun?)GetValue(RunProperty);
		set => SetValue(RunProperty, value);
	}

	/// <summary>The scratch frame the picture shows, or null; its recordings (the project as it is now) take precedence over the run's.</summary>
	public ScratchResult? Scratch
	{
		get => (ScratchResult?)GetValue(ScratchProperty);
		set => SetValue(ScratchProperty, value);
	}

	/// <summary>The frame the picture shows; the graphs show the run's recording as it stood there.</summary>
	public int Frame
	{
		get => (int)GetValue(FrameProperty);
		set => SetValue(FrameProperty, value);
	}

	/// <summary>Which picture this overlay sits on; only the probes of that layer are drawn.</summary>
	public ProbeLayer Layer
	{
		get => (ProbeLayer)GetValue(LayerProperty);
		set => SetValue(LayerProperty, value);
	}

	/// <summary>Canvas width in pixels, the x range of the model coordinates.</summary>
	public int CanvasWidth
	{
		get => (int)GetValue(CanvasWidthProperty);
		set => SetValue(CanvasWidthProperty, value);
	}

	/// <summary>Canvas height in pixels.</summary>
	public int CanvasHeight
	{
		get => (int)GetValue(CanvasHeightProperty);
		set => SetValue(CanvasHeightProperty, value);
	}

	/// <summary>The item selected in the project (its box gets the accent border); set when a box is grabbed or focused.</summary>
	public object? SelectedItem
	{
		get => GetValue(SelectedItemProperty);
		set => SetValue(SelectedItemProperty, value);
	}

	/// <summary>Style of the thumb covering a box (a <see cref="MarkerThumb"/> style; transparent, shows focus).</summary>
	public Style? BoxStyle
	{
		get => (Style?)GetValue(BoxStyleProperty);
		set => SetValue(BoxStyleProperty, value);
	}

	/// <summary>Style of the resize grip at a box's bottom-right corner.</summary>
	public Style? CornerStyle
	{
		get => (Style?)GetValue(CornerStyleProperty);
		set => SetValue(CornerStyleProperty, value);
	}

	/// <summary>Border of the selected probe's box.</summary>
	public Brush AccentBrush
	{
		get => (Brush)GetValue(AccentBrushProperty);
		set => SetValue(AccentBrushProperty, value);
	}

	// ---------------------------------------------------------------- model and run wiring

	private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var overlay = (ProbeGraphOverlay)d;
		if (e.OldValue is Project old)
		{
			old.Probes.CollectionChanged -= overlay.OnCollectionChanged;
		}
		if (e.NewValue is Project project)
		{
			project.Probes.CollectionChanged += overlay.OnCollectionChanged;
		}
		overlay.Rebuild();
	}

	private static void OnRunChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var overlay = (ProbeGraphOverlay)d;
		if (e.OldValue is SimulationRun old)
		{
			old.ProbesUpdated -= overlay.OnProbesUpdated;
		}
		if (e.NewValue is SimulationRun run)
		{
			run.ProbesUpdated += overlay.OnProbesUpdated;
		}
	}

	private void OnProbesUpdated(object? sender, ProbeUpdate e) => InvalidateVisual();

	private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

	/// <summary>Recreates the thumbs for the active probes of this layer and re-subscribes to every probe.</summary>
	private void Rebuild()
	{
		foreach (INotifyPropertyChanged item in _subscribed)
		{
			item.PropertyChanged -= OnProbePropertyChanged;
		}
		_subscribed.Clear();
		Children.Clear();
		if (Project is { } project)
		{
			foreach (Probe probe in project.Probes)
			{
				if (_subscribed.Add(probe))
				{
					probe.PropertyChanged += OnProbePropertyChanged;
				}
				if (!IsShown(probe))
				{
					continue;
				}
				AddThumb(probe, MarkerRole.ProbeBox, BoxStyle, "TipProbeBoxFormat", "ProbeBox:" + probe.Name);
				AddThumb(probe, MarkerRole.ProbeBoxCorner, CornerStyle, "TipProbeBoxCornerFormat", "ProbeBoxCorner:" + probe.Name);
			}
		}
		UpdateSelection();
		InvalidateArrange();
		InvalidateVisual();
	}

	private bool IsShown(Probe probe) => probe.Active && probe.Layer == Layer;

	private void AddThumb(Probe probe, MarkerRole role, Style? style, string tipKey, string automationId)
	{
		var thumb = new MarkerThumb(probe, role) { Style = style };
		thumb.ToolTip = TryFindResource(tipKey) is string format ? string.Format(CultureInfo.CurrentUICulture, format, probe.Name) : probe.Name;
		AutomationProperties.SetName(thumb, probe.Name);
		AutomationProperties.SetAutomationId(thumb, automationId);
		thumb.DragStarted += OnDragStarted;
		thumb.DragDelta += OnDragDelta;
		thumb.GotKeyboardFocus += (_, _) => SelectedItem = probe;
		Children.Add(thumb);
	}

	private void OnProbePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Probe.Active) or nameof(Probe.Layer) or nameof(Probe.Name))
		{
			Rebuild();
			return;
		}
		InvalidateArrange();
		InvalidateVisual();
	}

	private void UpdateSelection()
	{
		object? selected = SelectedItem;
		foreach (MarkerThumb thumb in Children)
		{
			thumb.IsSelectedMarker = ReferenceEquals(thumb.Item, selected);
		}
		InvalidateVisual();
	}

	// ---------------------------------------------------------------- geometry

	/// <summary>Pixels per canvas pixel for an element of <paramref name="size"/>: the picture's uniform scale (see <see cref="SceneOverlay"/> for why the size is passed in).</summary>
	private double ScaleFor(Size size) => Math.Min(size.Width / Math.Max(1, CanvasWidth), size.Height / Math.Max(1, CanvasHeight));

	/// <summary>The box of <paramref name="probe"/> in this element's coordinates, for an element of <paramref name="size"/>; the picture is centred in it.</summary>
	private Rect BoxOf(Probe probe, Size size)
	{
		double scale = ScaleFor(size);
		double originX = (size.Width - CanvasWidth * scale) / 2;
		double originY = (size.Height - CanvasHeight * scale) / 2;
		BoxRect box = ProbeGraph.BoxInCanvas(probe, CanvasWidth, CanvasHeight);
		// whole view pixels, so the 1-pixel lines of the box stay crisp
		return new Rect(Math.Round(originX + box.X * scale), Math.Round(originY + box.Y * scale), Math.Max(1, Math.Round(box.Width * scale)), Math.Max(1, Math.Round(box.Height * scale)));
	}

	/// <inheritdoc/>
	protected override Size MeasureOverride(Size availableSize)
	{
		foreach (UIElement child in Children)
		{
			child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		}
		return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
	}

	/// <inheritdoc/>
	protected override Size ArrangeOverride(Size finalSize)
	{
		foreach (MarkerThumb thumb in Children)
		{
			Rect box = BoxOf((Probe)thumb.Item, finalSize);
			if (thumb.Role == MarkerRole.ProbeBoxCorner)
			{
				Size grip = thumb.DesiredSize;
				thumb.Arrange(new Rect(box.Right - grip.Width, box.Bottom - grip.Height, grip.Width, grip.Height));
			}
			else
			{
				thumb.Arrange(box);
			}
		}
		return finalSize;
	}

	// ---------------------------------------------------------------- drawing

	/// <summary>
	/// The recording of <paramref name="probe"/> as the picture shows it (the scratch frame's, else the run's as
	/// it stood at <see cref="Frame"/>), and the run length for the step axis; empty data without either.
	/// </summary>
	private ProbeGraphData DataFor(Probe probe, out int totalSteps)
	{
		if (Scratch is { } scratch)
		{
			totalSteps = Math.Max(1, scratch.Spec.Steps);
			// a scratch trace holds one profile, the frame's own, at index 0
			return TraceOf(scratch.Spec.Probes, scratch.Traces, probe) is { } scratchTrace
				? ProbeGraphData.At(scratchTrace, scratch.Frame * scratch.Spec.VisualizeEvery, 0)
				: default;
		}
		SimulationRun? run = Run;
		totalSteps = Math.Max(1, run?.Spec.Steps ?? Project?.Simulation.Steps ?? 1);
		if (run == null)
		{
			return default;
		}
		return TraceOf(run.Spec.Probes, run.Traces, probe) is { } trace
			? ProbeGraphData.At(trace, Frame * run.Spec.VisualizeEvery, Frame)
			: default;
	}

	/// <summary>The trace of the model probe among a spec's probes (matched by <see cref="ProbeSpec.Origin"/>), or null when it was not recorded.</summary>
	private static ProbeTrace? TraceOf(ImmutableArray<ProbeSpec> specs, ProbeTrace[] traces, Probe probe)
	{
		for (int i = 0; i < specs.Length && i < traces.Length; i++)
		{
			if (ReferenceEquals(specs[i].Origin, probe))
			{
				return traces[i];
			}
		}
		return null;
	}

	/// <inheritdoc/>
	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (Project is not { } project || ActualWidth <= 0 || ActualHeight <= 0)
		{
			return;
		}
		double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		ProbeGraphStyle style = StyleFor(pixelsPerDip);
		object? selected = SelectedItem;
		foreach (Probe probe in project.Probes)
		{
			if (!IsShown(probe))
			{
				continue;
			}
			Rect box = BoxOf(probe, RenderSize);
			ProbeGraphData data = DataFor(probe, out int totalSteps);
			ProbeGraphLayout layout = ProbeGraph.Layout(probe, data, totalSteps, (float)box.Width, (float)box.Height, style, CultureInfo.CurrentCulture);
			if (!layout.IsVisible)
			{
				continue;
			}
			dc.PushTransform(new TranslateTransform(box.X, box.Y));
			DrawBox(dc, layout, ReferenceEquals(probe, selected), pixelsPerDip);
			dc.Pop();
		}
	}

	/// <summary>The text metrics of the fixed graph font at this DPI: the line height and the measured label widths.</summary>
	private ProbeGraphStyle StyleFor(double pixelsPerDip)
	{
		if (_style == null || _stylePixelsPerDip != pixelsPerDip)
		{
			_stylePixelsPerDip = pixelsPerDip;
			float lineHeight = (float)Text("0", GraphFontSize, LabelBrush, pixelsPerDip).Height;
			_style = new ProbeGraphStyle((float)GraphFontSize, lineHeight, text => (float)Text(text, GraphFontSize, LabelBrush, pixelsPerDip).Width);
		}
		return _style;
	}

	/// <summary>Draws one box from its layout, with the origin at the box's top-left corner.</summary>
	private void DrawBox(DrawingContext dc, ProbeGraphLayout layout, bool isSelected, double pixelsPerDip)
	{
		double w = layout.Width;
		double h = layout.Height;
		dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));
		dc.DrawRectangle(null, isSelected ? new Pen(AccentBrush, 1.0) : BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));
		double fontPx = layout.FontSize;
		if (layout.HasPlot)
		{
			double plotLeft = layout.PlotX;
			double plotTop = layout.PlotY;
			double plotRight = layout.PlotX + layout.PlotWidth;
			double plotBottom = layout.PlotY + layout.PlotHeight;
			// value axis: grid lines (the zero line brighter) with right-aligned labels in the left margin
			foreach (GraphTick tick in layout.YTicks)
			{
				double y = Math.Round(tick.Position) + 0.5;
				dc.DrawLine(tick.IsZero ? ZeroPen : GridPen, new Point(plotLeft, y), new Point(plotRight, y));
				if (tick.Label.Length == 0)
				{
					continue;
				}
				// right-aligned by position: a MaxTextWidth equal to the measured width made WPF wrap the label onto a
				// second line, which the one-line limit dropped (the labels vanished, 2026-10-14)
				FormattedText label = Text(tick.Label, fontPx, LabelBrush, pixelsPerDip);
				double lx = Math.Max(layout.LabelLeft, plotLeft - ProbeGraph.LabelGap - label.Width);
				dc.DrawText(label, new Point(lx, Math.Clamp(tick.Position - label.Height / 2, plotTop - label.Height / 2, plotBottom - label.Height / 2)));
			}
			// step axis (point probe) or position on the line (line probe): labels centred below the plot, kept inside the box
			foreach (GraphTick tick in layout.XTicks)
			{
				double x = Math.Round(tick.Position) + 0.5;
				dc.DrawLine(GridPen, new Point(x, plotTop), new Point(x, plotBottom));
				if (tick.Label.Length == 0)
				{
					continue;
				}
				FormattedText label = Text(tick.Label, fontPx, LabelBrush, pixelsPerDip);
				double lx = Math.Clamp(tick.Position - label.Width / 2, ProbeGraph.EdgePad, Math.Max(ProbeGraph.EdgePad, w - label.Width - ProbeGraph.EdgePad));
				dc.DrawText(label, new Point(lx, plotBottom + 2));
			}
			dc.DrawRectangle(null, FramePen, new Rect(plotLeft + 0.5, plotTop + 0.5, layout.PlotWidth, layout.PlotHeight));
			DrawCurve(dc, layout);
		}
		// the title strip: the readout right-aligned in its own colour, the title in the remaining width
		double readoutWidth = 0;
		if (layout.Readout.Length > 0)
		{
			FormattedText readout = Text(layout.Readout, fontPx, CurveBrush, pixelsPerDip);
			readoutWidth = readout.Width;
			dc.DrawText(readout, new Point(w - readoutWidth - 8, layout.TitleTop));
		}
		FormattedText title = Text(layout.Title, fontPx, TitleBrush, pixelsPerDip);
		title.MaxTextWidth = Math.Max(10, w - readoutWidth - 16);
		title.Trimming = TextTrimming.CharacterEllipsis;
		dc.DrawText(title, new Point(ProbeGraph.EdgePad + 3, layout.TitleTop));
	}

	/// <summary>Strokes the curve clipped to the plot area; a single point is a small dot.</summary>
	private static void DrawCurve(DrawingContext dc, ProbeGraphLayout layout)
	{
		if (layout.Curve.Length == 0)
		{
			return;
		}
		if (layout.Curve.Length == 1)
		{
			dc.DrawEllipse(CurveBrush, null, new Point(layout.Curve[0].X, layout.Curve[0].Y), 2, 2);
			return;
		}
		var geometry = new StreamGeometry();
		using (StreamGeometryContext context = geometry.Open())
		{
			context.BeginFigure(new Point(layout.Curve[0].X, layout.Curve[0].Y), false, false);
			for (int i = 1; i < layout.Curve.Length; i++)
			{
				context.LineTo(new Point(layout.Curve[i].X, layout.Curve[i].Y), true, false);
			}
		}
		geometry.Freeze();
		dc.PushClip(new RectangleGeometry(new Rect(layout.PlotX, layout.PlotY, layout.PlotWidth, layout.PlotHeight)));
		dc.DrawGeometry(null, new Pen(CurveBrush, layout.LineWidth) { LineJoin = PenLineJoin.Round }, geometry);
		dc.Pop();
	}

	private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip) =>
		new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, GraphTypeface, size, brush, pixelsPerDip) { MaxLineCount = 1 };

	private static T Freeze<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}

	// ---------------------------------------------------------------- dragging and keys

	private void OnDragStarted(object sender, DragStartedEventArgs e)
	{
		var thumb = (MarkerThumb)sender;
		var probe = (Probe)thumb.Item;
		if (thumb.Role == MarkerRole.ProbeBoxCorner)
		{
			BoxSize size = ProbeGraph.BoxSizeOf(probe);
			(thumb.DragStartX, thumb.DragStartY) = (size.Width, size.Height);
		}
		else
		{
			(thumb.DragStartX, thumb.DragStartY) = (probe.BoxX, probe.BoxY);
		}
		thumb.DragDeltaX = 0;
		thumb.DragDeltaY = 0;
		SelectedItem = probe;
		thumb.Focus();
	}

	private void OnDragDelta(object sender, DragDeltaEventArgs e)
	{
		var thumb = (MarkerThumb)sender;
		double scale = ScaleFor(RenderSize);
		if (scale <= 0)
		{
			return;
		}
		thumb.DragDeltaX += e.HorizontalChange / scale;
		thumb.DragDeltaY += e.VerticalChange / scale;
		int x = thumb.DragStartX + (int)Math.Round(thumb.DragDeltaX);
		int y = thumb.DragStartY + (int)Math.Round(thumb.DragDeltaY);
		var probe = (Probe)thumb.Item;
		if (thumb.Role == MarkerRole.ProbeBoxCorner)
		{
			// the fixed sizes are snap points of the drag
			(ProbeBoxSize size, BoxSize box) = ProbeGraph.Snap(x, y);
			SetBoxSize(probe, size, box);
		}
		else
		{
			SetBoxPosition(probe, x, y, snap: true);
		}
	}

	/// <summary>
	/// Moves the box, kept inside the canvas. With <paramref name="snap"/> (a drag, not a keyboard nudge) a box
	/// whose anchor comes within <see cref="SnapRadius"/> screen pixels of the probe's point A lands on it and is
	/// attached from then on (<see cref="Probe.IsBoxAttached"/>); an attached box stays on the probe until it is
	/// dragged more than <see cref="SnapRelease"/> away. One radius, not a snap per axis, with hysteresis (the
	/// user's rule, 2026-10-14).
	/// </summary>
	private void SetBoxPosition(Probe probe, int x, int y, bool snap)
	{
		if (snap)
		{
			double scale = ScaleFor(RenderSize);
			double dx = (x - probe.AX) * scale;
			double dy = (y - probe.AY) * scale;
			double limit = probe.IsBoxAttached ? SnapRelease : SnapRadius;
			if (dx * dx + dy * dy <= limit * limit)
			{
				x = probe.AX;
				y = probe.AY;
			}
		}
		BoxRect box = ProbeGraph.BoxInCanvas(probe, CanvasWidth, CanvasHeight);
		probe.BoxX = Math.Clamp(x, 0, Math.Max(0, CanvasWidth - box.Width));
		probe.BoxY = Math.Clamp(y, 0, Math.Max(0, CanvasHeight - box.Height));
	}

	/// <summary>Applies a box size: a fixed size as it is, a custom one with its width and height.</summary>
	private static void SetBoxSize(Probe probe, ProbeBoxSize size, BoxSize box)
	{
		if (size == ProbeBoxSize.Custom)
		{
			probe.BoxWidth = box.Width;
			probe.BoxHeight = box.Height;
		}
		probe.BoxSize = size;
	}

	/// <summary>The arrow keys move a focused box by one canvas pixel (ten with Shift); on the corner grip they resize it, to a custom size.</summary>
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Handled || e.OriginalSource is not MarkerThumb thumb)
		{
			return;
		}
		int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? KeyStepFast : KeyStep;
		(int dx, int dy) = e.Key switch
		{
			Key.Left => (-step, 0),
			Key.Right => (step, 0),
			Key.Up => (0, -step),
			Key.Down => (0, step),
			_ => (0, 0),
		};
		if (dx == 0 && dy == 0)
		{
			return;
		}
		var probe = (Probe)thumb.Item;
		if (thumb.Role == MarkerRole.ProbeBoxCorner)
		{
			BoxSize size = ProbeGraph.BoxSizeOf(probe);
			SetBoxSize(probe, ProbeBoxSize.Custom, new BoxSize(Math.Clamp(size.Width + dx, Probe.MinBoxWidth, Probe.MaxBoxSide), Math.Clamp(size.Height + dy, Probe.MinBoxHeight, Probe.MaxBoxSide)));
		}
		else
		{
			SetBoxPosition(probe, probe.BoxX + dx, probe.BoxY + dy, snap: false);
		}
		e.Handled = true;
	}
}

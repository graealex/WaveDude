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

namespace WaveDude.WPF.Controls;

/// <summary>
/// The sources and probes of a <see cref="Project"/> drawn over a picture of the canvas, as draggable
/// handles (<see cref="MarkerThumb"/>): a source's position, a point probe's point, a line probe's two
/// ends with the line between them, plus the emitters of a composite source as dots and the item names
/// as labels. Dragging a handle, or moving it with the arrow keys while it has keyboard focus, writes the
/// new canvas coordinates to the model; grabbing or focusing a handle makes its item the
/// <see cref="SelectedItem"/>. The overlay fills the same rectangle as the picture (an
/// <see cref="Image"/> with <see cref="Stretch.Uniform"/>), so it maps canvas pixels to its own
/// coordinates with the same uniform scale and centring. UI thread; listens to the model's change
/// notifications.
/// </summary>
public sealed class SceneOverlay : Panel
{
	/// <summary>Identifies the <see cref="Project"/> property.</summary>
	public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register(
		nameof(Project), typeof(Project), typeof(SceneOverlay), new PropertyMetadata(null, OnProjectChanged));

	/// <summary>Identifies the <see cref="CanvasWidth"/> property.</summary>
	public static readonly DependencyProperty CanvasWidthProperty = DependencyProperty.Register(
		nameof(CanvasWidth), typeof(int), typeof(SceneOverlay), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="CanvasHeight"/> property.</summary>
	public static readonly DependencyProperty CanvasHeightProperty = DependencyProperty.Register(
		nameof(CanvasHeight), typeof(int), typeof(SceneOverlay), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="SelectedItem"/> property.</summary>
	public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
		nameof(SelectedItem), typeof(object), typeof(SceneOverlay),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SceneOverlay)d).UpdateSelection()));

	/// <summary>Identifies the <see cref="SourceStyle"/> property.</summary>
	public static readonly DependencyProperty SourceStyleProperty = DependencyProperty.Register(
		nameof(SourceStyle), typeof(Style), typeof(SceneOverlay), new PropertyMetadata(null, (d, _) => ((SceneOverlay)d).Rebuild()));

	/// <summary>Identifies the <see cref="ProbeStyle"/> property.</summary>
	public static readonly DependencyProperty ProbeStyleProperty = DependencyProperty.Register(
		nameof(ProbeStyle), typeof(Style), typeof(SceneOverlay), new PropertyMetadata(null, (d, _) => ((SceneOverlay)d).Rebuild()));

	/// <summary>Identifies the <see cref="LineBrush"/> property.</summary>
	public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
		nameof(LineBrush), typeof(Brush), typeof(SceneOverlay), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="LabelBrush"/> property.</summary>
	public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
		nameof(LabelBrush), typeof(Brush), typeof(SceneOverlay), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Identifies the <see cref="AccentBrush"/> property.</summary>
	public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
		nameof(AccentBrush), typeof(Brush), typeof(SceneOverlay), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>Pixels a focused handle moves per arrow key, and with Shift.</summary>
	private const int KeyStep = 1;
	private const int KeyStepFast = 10;
	private const double LabelOffset = 13.0;
	private const double ElementDotRadius = 2.5;
	private static readonly Brush HaloBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0)));
	private static readonly Typeface LabelTypeface = new("Segoe UI");

	private readonly HashSet<INotifyPropertyChanged> _subscribed = [];
	private bool _regenerating;

	public SceneOverlay()
	{
		Background = Brushes.Transparent;
		IsHitTestVisible = true;
		Focusable = false;
	}

	/// <summary>The project whose sources and probes are shown; null shows nothing.</summary>
	public Project? Project
	{
		get => (Project?)GetValue(ProjectProperty);
		set => SetValue(ProjectProperty, value);
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

	/// <summary>The source or probe selected in the project (highlighted); set when a handle is grabbed or focused.</summary>
	public object? SelectedItem
	{
		get => GetValue(SelectedItemProperty);
		set => SetValue(SelectedItemProperty, value);
	}

	/// <summary>Style of the source handles (a <see cref="MarkerThumb"/> style).</summary>
	public Style? SourceStyle
	{
		get => (Style?)GetValue(SourceStyleProperty);
		set => SetValue(SourceStyleProperty, value);
	}

	/// <summary>Style of the probe handles (a <see cref="MarkerThumb"/> style).</summary>
	public Style? ProbeStyle
	{
		get => (Style?)GetValue(ProbeStyleProperty);
		set => SetValue(ProbeStyleProperty, value);
	}

	/// <summary>Line of a line probe and the emitter dots of a composite source.</summary>
	public Brush LineBrush
	{
		get => (Brush)GetValue(LineBrushProperty);
		set => SetValue(LineBrushProperty, value);
	}

	/// <summary>The item names next to the handles.</summary>
	public Brush LabelBrush
	{
		get => (Brush)GetValue(LabelBrushProperty);
		set => SetValue(LabelBrushProperty, value);
	}

	/// <summary>Line and label of the selected item.</summary>
	public Brush AccentBrush
	{
		get => (Brush)GetValue(AccentBrushProperty);
		set => SetValue(AccentBrushProperty, value);
	}

	// ---------------------------------------------------------------- model wiring

	private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var overlay = (SceneOverlay)d;
		if (e.OldValue is Project old)
		{
			old.Sources.CollectionChanged -= overlay.OnCollectionChanged;
			old.Probes.CollectionChanged -= overlay.OnCollectionChanged;
		}
		if (e.NewValue is Project project)
		{
			project.Sources.CollectionChanged += overlay.OnCollectionChanged;
			project.Probes.CollectionChanged += overlay.OnCollectionChanged;
		}
		overlay.Rebuild();
	}

	private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

	/// <summary>Recreates the handles from the project's lists and re-subscribes to every item.</summary>
	private void Rebuild()
	{
		foreach (INotifyPropertyChanged item in _subscribed)
		{
			item.PropertyChanged -= OnItemPropertyChanged;
		}
		_subscribed.Clear();
		Children.Clear();
		if (Project is not { } project)
		{
			InvalidateVisual();
			return;
		}
		foreach (Source source in project.Sources)
		{
			Subscribe(source);
			if (source.Array != null)
			{
				Subscribe(source.Array);
			}
			Regenerate(source);
			AddThumb(source, MarkerRole.Source, SourceStyle, source.Name, "Source:" + source.Name);
		}
		foreach (Probe probe in project.Probes)
		{
			Subscribe(probe);
			AddThumb(probe, MarkerRole.ProbeA, ProbeStyle, probe.Name, "Probe:" + probe.Name + ":A");
			if (probe.Kind == ProbeKind.Line)
			{
				AddThumb(probe, MarkerRole.ProbeB, ProbeStyle, probe.Name + " B", "Probe:" + probe.Name + ":B");
			}
		}
		UpdateSelection();
		InvalidateVisual();
	}

	private void Subscribe(INotifyPropertyChanged item)
	{
		if (_subscribed.Add(item))
		{
			item.PropertyChanged += OnItemPropertyChanged;
		}
	}

	private void AddThumb(object item, MarkerRole role, Style? style, string name, string automationId)
	{
		var thumb = new MarkerThumb(item, role) { Style = style };
		thumb.ToolTip = TryFindResource("TipMarkerFormat") is string format ? string.Format(CultureInfo.CurrentUICulture, format, name) : name;
		AutomationProperties.SetName(thumb, name);
		AutomationProperties.SetAutomationId(thumb, automationId);
		thumb.DragStarted += OnDragStarted;
		thumb.DragDelta += OnDragDelta;
		thumb.GotKeyboardFocus += (_, _) => SelectedItem = item;
		UpdateOpacity(thumb);
		Children.Add(thumb);
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (sender)
		{
			case Probe when e.PropertyName == nameof(Probe.Kind):
			case Source when e.PropertyName == nameof(Source.Array):
				Rebuild();
				return;
			case Source source:
				Regenerate(source);
				break;
		}
		if (e.PropertyName is nameof(Source.Name) or nameof(Source.Enabled) or nameof(Probe.Active))
		{
			Rebuild();
			return;
		}
		InvalidateArrange();
		InvalidateVisual();
	}

	/// <summary>Keeps a composite source's emitter dots current while it is edited (they are only regenerated for a run otherwise).</summary>
	private void Regenerate(Source source)
	{
		if (_regenerating || Project is not { } project || source.Array == null)
		{
			return;
		}
		_regenerating = true;
		try
		{
			source.GenerateElements(project.Seed);
		}
		finally
		{
			_regenerating = false;
		}
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

	private static void UpdateOpacity(MarkerThumb thumb) =>
		thumb.Opacity = thumb.Item switch { Source { Enabled: false } => 0.45, Probe { Active: false } => 0.45, _ => 1.0 };

	// ---------------------------------------------------------------- geometry

	/// <summary>Pixels per canvas pixel: the picture's uniform scale inside this element.</summary>
	private double Scale => Math.Min(ActualWidth / Math.Max(1, CanvasWidth), ActualHeight / Math.Max(1, CanvasHeight));

	/// <summary>Where canvas pixel (0, 0) starts: the picture is centred in the element.</summary>
	private Point Origin => new((ActualWidth - CanvasWidth * Scale) / 2, (ActualHeight - CanvasHeight * Scale) / 2);

	/// <summary>The centre of canvas pixel (x, y) in this element's coordinates.</summary>
	private Point ToView(int x, int y)
	{
		double scale = Scale;
		Point origin = Origin;
		return new Point(origin.X + (x + 0.5) * scale, origin.Y + (y + 0.5) * scale);
	}

	private static (int X, int Y) PointOf(MarkerThumb thumb) => (thumb.Item, thumb.Role) switch
	{
		(Source source, _) => (source.X, source.Y),
		(Probe probe, MarkerRole.ProbeB) => (probe.BX, probe.BY),
		(Probe probe, _) => (probe.AX, probe.AY),
		_ => (0, 0),
	};

	private void SetPoint(MarkerThumb thumb, int x, int y)
	{
		x = Math.Clamp(x, 0, Math.Max(0, CanvasWidth - 1));
		y = Math.Clamp(y, 0, Math.Max(0, CanvasHeight - 1));
		switch (thumb.Item, thumb.Role)
		{
			case (Source source, _):
				source.X = x;
				source.Y = y;
				break;
			case (Probe probe, MarkerRole.ProbeB):
				probe.BX = x;
				probe.BY = y;
				break;
			case (Probe probe, _):
				probe.AX = x;
				probe.AY = y;
				break;
		}
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
			(int x, int y) = PointOf(thumb);
			Point centre = ToView(x, y);
			Size size = thumb.DesiredSize;
			thumb.Arrange(new Rect(centre.X - size.Width / 2, centre.Y - size.Height / 2, size.Width, size.Height));
		}
		return finalSize;
	}

	/// <inheritdoc/>
	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (Project is not { } project || ActualWidth <= 0 || ActualHeight <= 0)
		{
			return;
		}
		object? selected = SelectedItem;
		foreach (Probe probe in project.Probes)
		{
			if (probe.Kind != ProbeKind.Line)
			{
				continue;
			}
			bool isSelected = ReferenceEquals(probe, selected);
			Point a = ToView(probe.AX, probe.AY);
			Point b = ToView(probe.BX, probe.BY);
			// a dark line under the dashed light one, so the line reads on any picture
			dc.DrawLine(new Pen(HaloBrush, isSelected ? 3.5 : 3.0), a, b);
			dc.DrawLine(new Pen(isSelected ? AccentBrush : LineBrush, isSelected ? 1.5 : 1.0) { DashStyle = DashStyles.Dash }, a, b);
		}
		foreach (Source source in project.Sources)
		{
			if (source.Array == null)
			{
				continue;
			}
			Brush dots = ReferenceEquals(source, selected) ? AccentBrush : LineBrush;
			var halo = new Pen(HaloBrush, 1.5);
			foreach (SourceElement element in source.Elements)
			{
				dc.DrawEllipse(dots, halo, ToView(element.X, element.Y), ElementDotRadius, ElementDotRadius);
			}
		}
		foreach (MarkerThumb thumb in Children)
		{
			if (thumb.Role == MarkerRole.ProbeB)
			{
				continue;
			}
			(int x, int y) = PointOf(thumb);
			Point centre = ToView(x, y);
			string name = thumb.Item is Source source ? source.Name : ((Probe)thumb.Item).Name;
			var label = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelTypeface, 11, thumb.IsSelectedMarker ? AccentBrush : LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			double lx = Math.Min(centre.X + LabelOffset, Math.Max(0, ActualWidth - label.Width));
			double ly = Math.Clamp(centre.Y - label.Height / 2, 0, Math.Max(0, ActualHeight - label.Height));
			var origin = new Point(lx, ly);
			// the text's outline in dark first (a halo), then the text
			dc.DrawGeometry(null, new Pen(HaloBrush, 3.0) { LineJoin = PenLineJoin.Round }, label.BuildGeometry(origin));
			dc.DrawText(label, origin);
		}
	}

	// ---------------------------------------------------------------- dragging and keys

	private void OnDragStarted(object sender, DragStartedEventArgs e)
	{
		var thumb = (MarkerThumb)sender;
		(thumb.DragStartX, thumb.DragStartY) = PointOf(thumb);
		thumb.DragDeltaX = 0;
		thumb.DragDeltaY = 0;
		SelectedItem = thumb.Item;
		thumb.Focus();
	}

	private void OnDragDelta(object sender, DragDeltaEventArgs e)
	{
		var thumb = (MarkerThumb)sender;
		double scale = Scale;
		if (scale <= 0)
		{
			return;
		}
		thumb.DragDeltaX += e.HorizontalChange / scale;
		thumb.DragDeltaY += e.VerticalChange / scale;
		SetPoint(thumb, thumb.DragStartX + (int)Math.Round(thumb.DragDeltaX), thumb.DragStartY + (int)Math.Round(thumb.DragDeltaY));
	}

	private static Brush Freeze(Brush brush)
	{
		brush.Freeze();
		return brush;
	}

	/// <summary>The arrow keys move the focused handle by one canvas pixel, ten with Shift.</summary>
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
		(int x, int y) = PointOf(thumb);
		SetPoint(thumb, x + dx, y + dy);
		e.Handled = true;
	}
}

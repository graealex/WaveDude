using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WaveDude.Model;
using WaveDude.WPF.Commands;
using WaveDude.WPF.Converters;

namespace WaveDude.WPF.Panes;

/// <summary>
/// The document in the centre: the field and the intensity picture, side by side or stacked,
/// with a transport bar to scrub, play and pause through the frames of a run at a chosen
/// speed. Dummy stage: the pictures are fixed placeholders and playing only advances
/// <see cref="Position"/>; the render pipeline that will feed <see cref="FieldImage"/> and
/// <see cref="IntensityImage"/> per frame does not exist yet. <see cref="FrameCount"/> follows
/// the project's step count and visualisation interval through a binding, and so do the
/// <see cref="InPoint"/>/<see cref="OutPoint"/> of the export range (<c>Display.ExportFrom/To</c>,
/// in steps; the <see cref="PreviewCommands"/> write them). Playback runs between the two,
/// looping when <see cref="IsLooping"/>.
/// </summary>
/// <remarks>
/// Playback runs on a <see cref="DispatcherTimer"/> on the UI thread: a speed of 1 advances
/// <see cref="BaseFrameRate"/> frames per second of wall-clock time, measured with a stopwatch
/// so that a slow tick does not slow the clock. Playing at the last frame restarts at 0.
/// Play, Pause and Space (toggle) are the built-in <see cref="MediaCommands"/>.
/// Keyboard: every transport control is a tab stop (play, pause, scrubber, speed, split, and
/// the splitter between the pictures). The split radios are one stop that lands on the checked
/// one; Left/Right there check the other. Arrows move the splitter; Enter (or a double-click)
/// resets it to the middle. Space toggles playback from anywhere in the pane except the speed
/// text box, and a click on the pictures focuses the pane so Space works after it. Executing
/// Play or Pause from the keyboard moves focus to the other button, because the one just used
/// is about to be disabled.
/// </remarks>
public partial class PreviewPane : UserControl
{
	/// <summary>Frames per second that a playback <see cref="Speed"/> of 1 corresponds to; a placeholder until the pipeline defines the frame clock.</summary>
	public const double BaseFrameRate = 30.0;

	/// <summary>Smallest accepted <see cref="Speed"/>.</summary>
	public const double MinSpeed = 0.01;

	/// <summary>Largest accepted <see cref="Speed"/>.</summary>
	public const double MaxSpeed = 100.0;

	/// <summary>Speed factors offered in the drop-down; any other value can be typed.</summary>
	public static IReadOnlyList<double> SpeedPresets { get; } = [0.25, 0.5, 1.0, 1.5, 2.0, 4.0];

	private static readonly BitmapSource PlaceholderField = CreatePlaceholder(intensity: false);
	private static readonly BitmapSource PlaceholderIntensity = CreatePlaceholder(intensity: true);

	/// <summary>Identifies the <see cref="Project"/> property.</summary>
	public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register(
		nameof(Project), typeof(Project), typeof(PreviewPane));

	/// <summary>Identifies the <see cref="FrameCount"/> property.</summary>
	public static readonly DependencyProperty FrameCountProperty = DependencyProperty.Register(
		nameof(FrameCount), typeof(int), typeof(PreviewPane),
		new PropertyMetadata(1, OnFrameCountChanged, (_, value) => Math.Max(1, (int)value)));

	private static readonly DependencyPropertyKey LastFrameKey = DependencyProperty.RegisterReadOnly(
		nameof(LastFrame), typeof(int), typeof(PreviewPane), new PropertyMetadata(0));

	/// <summary>Identifies the <see cref="LastFrame"/> property.</summary>
	public static readonly DependencyProperty LastFrameProperty = LastFrameKey.DependencyProperty;

	/// <summary>Identifies the <see cref="Position"/> property.</summary>
	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
		nameof(Position), typeof(int), typeof(PreviewPane),
		new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoercePosition));

	/// <summary>Identifies the <see cref="IsPlaying"/> property.</summary>
	public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
		nameof(IsPlaying), typeof(bool), typeof(PreviewPane), new PropertyMetadata(false, OnIsPlayingChanged));

	/// <summary>Identifies the <see cref="Speed"/> property.</summary>
	public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
		nameof(Speed), typeof(double), typeof(PreviewPane),
		new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceSpeed));

	/// <summary>Identifies the <see cref="InPoint"/> property.</summary>
	public static readonly DependencyProperty InPointProperty = DependencyProperty.Register(
		nameof(InPoint), typeof(int), typeof(PreviewPane), new PropertyMetadata(0));

	/// <summary>Identifies the <see cref="OutPoint"/> property.</summary>
	public static readonly DependencyProperty OutPointProperty = DependencyProperty.Register(
		nameof(OutPoint), typeof(int), typeof(PreviewPane), new PropertyMetadata(0));

	/// <summary>Identifies the <see cref="IsLooping"/> property.</summary>
	public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
		nameof(IsLooping), typeof(bool), typeof(PreviewPane), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	/// <summary>Identifies the <see cref="Orientation"/> property.</summary>
	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
		nameof(Orientation), typeof(Orientation), typeof(PreviewPane),
		new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	/// <summary>Identifies the <see cref="FieldImage"/> property.</summary>
	public static readonly DependencyProperty FieldImageProperty = DependencyProperty.Register(
		nameof(FieldImage), typeof(ImageSource), typeof(PreviewPane), new PropertyMetadata(PlaceholderField));

	/// <summary>Identifies the <see cref="IntensityImage"/> property.</summary>
	public static readonly DependencyProperty IntensityImageProperty = DependencyProperty.Register(
		nameof(IntensityImage), typeof(ImageSource), typeof(PreviewPane), new PropertyMetadata(PlaceholderIntensity));

	private readonly DispatcherTimer _timer;
	private readonly Stopwatch _clock = new();

	/// <summary>Fractional frame advance carried from one tick to the next, so slow speeds still move.</summary>
	private double _carry;

	public PreviewPane()
	{
		InitializeComponent();

		// frames = ceil(steps / visualize every); re-evaluated whenever the project or its settings change
		SetBinding(FrameCountProperty, new MultiBinding
		{
			Converter = new FrameCountConverter(),
			Bindings =
			{
				new Binding($"{nameof(Project)}.{nameof(Model.Project.Simulation)}.{nameof(Simulation.Steps)}") { Source = this },
				new Binding($"{nameof(Project)}.{nameof(Model.Project.Simulation)}.{nameof(Simulation.VisualizeEvery)}") { Source = this },
			},
		});

		// in/out points in frames from the model's steps; "end" makes an ExportTo of 0 the last frame
		SetBinding(InPointProperty, RangeBinding(nameof(Display.ExportFrom), null));
		SetBinding(OutPointProperty, RangeBinding(nameof(Display.ExportTo), "end"));

		_timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1.0 / 60.0) };
		_timer.Tick += OnTick;
		Unloaded += (_, _) => IsPlaying = false;
	}

	private MultiBinding RangeBinding(string stepProperty, string? parameter) => new()
	{
		Converter = new StepToFrameConverter(),
		ConverterParameter = parameter,
		Mode = BindingMode.OneWay,
		Bindings =
		{
			new Binding($"{nameof(Project)}.{nameof(Model.Project.Display)}.{stepProperty}") { Source = this },
			new Binding($"{nameof(Project)}.{nameof(Model.Project.Simulation)}.{nameof(Simulation.VisualizeEvery)}") { Source = this },
			new Binding(nameof(LastFrame)) { Source = this },
		},
	};

	/// <summary>The project whose run is shown; null shows the placeholders with a single frame.</summary>
	public Project? Project
	{
		get => (Project?)GetValue(ProjectProperty);
		set => SetValue(ProjectProperty, value);
	}

	/// <summary>Number of frames in the run, at least 1. Bound to the project's steps and visualisation interval.</summary>
	public int FrameCount
	{
		get => (int)GetValue(FrameCountProperty);
		set => SetValue(FrameCountProperty, value);
	}

	/// <summary>Index of the last frame, <see cref="FrameCount"/> - 1; the scrubber's maximum.</summary>
	public int LastFrame
	{
		get => (int)GetValue(LastFrameProperty);
		private set => SetValue(LastFrameKey, value);
	}

	/// <summary>Frame shown, 0 to <see cref="LastFrame"/>. Moved by the scrubber and by playback.</summary>
	public int Position
	{
		get => (int)GetValue(PositionProperty);
		set => SetValue(PositionProperty, value);
	}

	/// <summary>Whether playback is running. Setting it starts or stops the frame clock.</summary>
	public bool IsPlaying
	{
		get => (bool)GetValue(IsPlayingProperty);
		set => SetValue(IsPlayingProperty, value);
	}

	/// <summary>Playback speed factor, <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>; 1 is <see cref="BaseFrameRate"/> frames per second.</summary>
	public double Speed
	{
		get => (double)GetValue(SpeedProperty);
		set => SetValue(SpeedProperty, value);
	}

	/// <summary>First frame of the export/loop range; 0 while no in point is set. Follows the model.</summary>
	public int InPoint
	{
		get => (int)GetValue(InPointProperty);
		private set => SetValue(InPointProperty, value);
	}

	/// <summary>Last frame of the export/loop range; <see cref="LastFrame"/> while no out point is set. Follows the model.</summary>
	public int OutPoint
	{
		get => (int)GetValue(OutPointProperty);
		private set => SetValue(OutPointProperty, value);
	}

	/// <summary>Whether playback wraps from the out point back to the in point instead of stopping.</summary>
	public bool IsLooping
	{
		get => (bool)GetValue(IsLoopingProperty);
		set => SetValue(IsLoopingProperty, value);
	}

	/// <summary>How the two pictures are split: <see cref="Orientation.Horizontal"/> side by side, <see cref="Orientation.Vertical"/> field above intensity.</summary>
	public Orientation Orientation
	{
		get => (Orientation)GetValue(OrientationProperty);
		set => SetValue(OrientationProperty, value);
	}

	/// <summary>Picture of the field at <see cref="Position"/>; a placeholder until the render pipeline sets it.</summary>
	public ImageSource? FieldImage
	{
		get => (ImageSource?)GetValue(FieldImageProperty);
		set => SetValue(FieldImageProperty, value);
	}

	/// <summary>Picture of the intensity at <see cref="Position"/>; a placeholder until the render pipeline sets it.</summary>
	public ImageSource? IntensityImage
	{
		get => (ImageSource?)GetValue(IntensityImageProperty);
		set => SetValue(IntensityImageProperty, value);
	}

	private static void OnFrameCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var pane = (PreviewPane)d;
		pane.LastFrame = (int)e.NewValue - 1;
		pane.CoerceValue(PositionProperty);
		CommandManager.InvalidateRequerySuggested();
	}

	private static object CoercePosition(DependencyObject d, object value) =>
		Math.Clamp((int)value, 0, ((PreviewPane)d).LastFrame);

	private static object CoerceSpeed(DependencyObject d, object value)
	{
		double speed = (double)value;
		return double.IsNaN(speed) ? 1.0 : Math.Clamp(speed, MinSpeed, MaxSpeed);
	}

	private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var pane = (PreviewPane)d;
		if ((bool)e.NewValue)
		{
			if (pane.Position >= pane.OutPoint)
			{
				pane.Position = pane.InPoint; // play the range again from its start
			}
			pane._carry = 0.0;
			pane._clock.Restart();
			pane._timer.Start();
		}
		else
		{
			pane._timer.Stop();
			pane._clock.Stop();
		}
		CommandManager.InvalidateRequerySuggested();
		pane.MoveFocusToCounterpart((bool)e.NewValue);
	}

	/// <summary>
	/// When the Play or Pause button has keyboard focus while its command just executed, moves
	/// focus to the other button, since CanExecute is about to disable this one and a disabled
	/// element cannot keep focus. Deferred behind the command re-query so the target is enabled.
	/// </summary>
	private void MoveFocusToCounterpart(bool playing)
	{
		if (playing && playButton.IsKeyboardFocused)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Background, () => pauseButton.Focus());
		}
		else if (!playing && pauseButton.IsKeyboardFocused)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Background, () => playButton.Focus());
		}
	}

	/// <summary>
	/// Advances <see cref="Position"/> by the frames elapsed since the previous tick. At the out
	/// point playback stops, or wraps to the in point when <see cref="IsLooping"/>.
	/// </summary>
	private void OnTick(object? sender, EventArgs e)
	{
		double seconds = _clock.Elapsed.TotalSeconds;
		_clock.Restart();
		_carry += seconds * BaseFrameRate * Speed;
		int frames = (int)Math.Floor(_carry);
		if (frames == 0)
		{
			return;
		}
		_carry -= frames;
		int next = Position + frames;
		int end = OutPoint;
		if (next < end)
		{
			Position = next;
		}
		else if (IsLooping && end > InPoint)
		{
			Position = InPoint + (next - end) % (end - InPoint);
		}
		else
		{
			Position = end;
			IsPlaying = false;
		}
	}

	private void CanPlay(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !IsPlaying && LastFrame > 0;

	private void OnPlay(object sender, ExecutedRoutedEventArgs e) => IsPlaying = true;

	private void CanPause(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = IsPlaying;

	private void OnPause(object sender, ExecutedRoutedEventArgs e) => IsPlaying = false;

	private void CanTogglePlayPause(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = LastFrame > 0;

	private void OnTogglePlayPause(object sender, ExecutedRoutedEventArgs e) => IsPlaying = !IsPlaying;

	// ---------------------------------------------------------------- transport commands

	/// <summary>The letter and punctuation gestures must not fire while the user types in a text box.</summary>
	private void CanTransport(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Project != null && e.OriginalSource is not TextBoxBase;

	private void OnStepBack(object sender, ExecutedRoutedEventArgs e) => Position -= 1;

	private void OnStepForward(object sender, ExecutedRoutedEventArgs e) => Position += 1;

	private void OnGoToStart(object sender, ExecutedRoutedEventArgs e) => Position = InPoint;

	private void OnGoToEnd(object sender, ExecutedRoutedEventArgs e) => Position = OutPoint;

	private void CanEditRange(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Project != null && LastFrame > 0 && e.OriginalSource is not TextBoxBase;

	/// <summary>The frame a range command was given, or the frame shown.</summary>
	private int FrameOf(ExecutedRoutedEventArgs e) => e.Parameter is int frame ? Math.Clamp(frame, 0, LastFrame) : Position;

	/// <summary>
	/// Sets the in point, in steps, on the model. An in point past the out point clears the out
	/// point (0 = end of the run), so the range stays ordered.
	/// </summary>
	private void OnSetInPoint(object sender, ExecutedRoutedEventArgs e)
	{
		Display display = Project!.Display;
		int step = FrameOf(e) * Project.Simulation.VisualizeEvery;
		if (display.ExportTo != 0 && display.ExportTo <= step)
		{
			display.ExportTo = 0;
		}
		display.ExportFrom = step;
	}

	/// <summary>
	/// Sets the out point on the model; the last frame means "to the end" and is stored as 0. An
	/// out point before the in point clears the in point.
	/// </summary>
	private void OnSetOutPoint(object sender, ExecutedRoutedEventArgs e)
	{
		Display display = Project!.Display;
		int frame = FrameOf(e);
		int step = frame >= LastFrame ? 0 : frame * Project.Simulation.VisualizeEvery;
		if (step != 0 && display.ExportFrom >= step)
		{
			display.ExportFrom = 0;
		}
		display.ExportTo = step;
	}

	private void CanClearRange(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Project is { Display: { } display } && (display.ExportFrom != 0 || display.ExportTo != 0) && e.OriginalSource is not TextBoxBase;

	private void OnClearRange(object sender, ExecutedRoutedEventArgs e)
	{
		Project!.Display.ExportFrom = 0;
		Project.Display.ExportTo = 0;
	}

	private void OnToggleLoop(object sender, ExecutedRoutedEventArgs e) => IsLooping = !IsLooping;

	/// <summary>Space inside the speed text box is neither a digit nor the play/pause toggle: swallowed.</summary>
	private void OnSpeedBoxPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}

	/// <summary>A click on the pictures gives the pane keyboard focus so that Space toggles playback.</summary>
	private void OnPicturesMouseDown(object sender, MouseButtonEventArgs e) => Focus();

	/// <summary>Puts the splitter back in the middle: both pictures get an equal star share of the split dimension.</summary>
	private void ResetSplit()
	{
		var half = new GridLength(1, GridUnitType.Star);
		if (Orientation == Orientation.Horizontal)
		{
			pictures.ColumnDefinitions[0].Width = half;
			pictures.ColumnDefinitions[2].Width = half;
		}
		else
		{
			pictures.RowDefinitions[0].Height = half;
			pictures.RowDefinitions[2].Height = half;
		}
	}

	private void OnSplitterMouseDoubleClick(object sender, MouseButtonEventArgs e) => ResetSplit();

	/// <summary>Enter on the focused splitter is the keyboard's double-click; the arrow keys move it (built in).</summary>
	private void OnSplitterKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			ResetSplit();
			e.Handled = true;
		}
	}

	/// <summary>
	/// The arrow keys inside the split group move to the neighbouring radio and check it, as a
	/// Win32 radio group does (WPF's own directional navigation only moves focus, and skips
	/// elements that are not tab stops). Only the checked radio is a tab stop, so Tab lands on
	/// the current choice. SetCurrentValue keeps the IsChecked binding alive.
	/// </summary>
	private void OnSplitGroupKeyDown(object sender, KeyEventArgs e)
	{
		int step = e.Key switch { Key.Right or Key.Down => 1, Key.Left or Key.Up => -1, _ => 0 };
		if (step == 0 || e.OriginalSource is not RadioButton current)
		{
			return;
		}
		var radios = splitGroup.Children.OfType<RadioButton>().ToList();
		RadioButton next = radios[(radios.IndexOf(current) + step + radios.Count) % radios.Count];
		next.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
		next.Focus();
		e.Handled = true;
	}

	/// <summary>
	/// A 16:9 stand-in picture: decaying ripples from a source near the top-left corner in the
	/// field's red/blue look, or the same source's glow in the intensity's blue look. Frozen so
	/// it can serve as a dependency property default.
	/// </summary>
	private static BitmapSource CreatePlaceholder(bool intensity)
	{
		const int width = 480;
		const int height = 270;
		const double sourceX = 36.0;
		const double sourceY = 36.0;
		var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
		var pixels = new int[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				double dx = x - sourceX;
				double dy = y - sourceY;
				double r = Math.Sqrt(dx * dx + dy * dy);
				int pixel;
				if (intensity)
				{
					double v = Math.Exp(-r / 150.0);
					pixel = (16 << 16) | ((int)(150 * v) << 8) | (int)(255 * v);
				}
				else
				{
					double v = Math.Sin(r * 0.7) * Math.Exp(-r / 200.0);
					int red = v > 0 ? (int)(255 * v) : 0;
					int blue = v < 0 ? (int)(-255 * v) : 0;
					pixel = (red << 16) | blue;
				}
				pixels[y * width + x] = pixel;
			}
		}
		bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
		bitmap.Freeze();
		return bitmap;
	}
}

using System.ComponentModel;
using System.Xml.Serialization;

namespace WaveDude.Model;

/// <summary>Output a probe samples and is drawn on.</summary>
public enum ProbeLayer
{
	/// <summary>The wave field.</summary>
	Field,
	/// <summary>The accumulated intensity.</summary>
	Intensity,
}

/// <summary>Quantity a probe records.</summary>
public enum ProbeMeasurement
{
	/// <summary>Field value at the probe; field layer only.</summary>
	Amplitude,
	/// <summary>Averaged intensity at the probe; intensity layer only.</summary>
	Intensity,
	/// <summary>Intensity summed over the run so far; intensity layer only.</summary>
	IntegratedIntensity,
}

/// <summary>Point probe (position A) or line probe (A to B).</summary>
public enum ProbeKind
{
	/// <summary>Samples one pixel and plots it over time.</summary>
	Point,
	/// <summary>Samples along a line and plots the current profile.</summary>
	Line,
}

/// <summary>Nominal size of the graph box drawn over the output.</summary>
public enum ProbeBoxSize
{
	/// <summary>Smallest box.</summary>
	Tiny,
	/// <summary>Small box.</summary>
	Small,
	/// <summary>Default box.</summary>
	Medium,
	/// <summary>Large box.</summary>
	Large,
	/// <summary>Largest box.</summary>
	ExtraLarge,
}

/// <summary>How the step axis of a point probe's time series is chosen.</summary>
public enum ProbeScaleMode
{
	/// <summary>A sliding window of <see cref="Probe.Window"/> steps.</summary>
	Running,
	/// <summary>The whole run.</summary>
	Full,
}

/// <summary>
/// A measurement point or line on the canvas and the graph that shows its values. Positions are
/// canvas pixels with the origin at the top left.
/// </summary>
public sealed class Probe : ObservableObject
{
	/// <summary>Default <see cref="Window"/> in steps.</summary>
	public const int DefaultWindow = 500;
	/// <summary>Smallest accepted <see cref="Window"/>.</summary>
	public const int MinWindow = 10;
	/// <summary>Largest accepted <see cref="Window"/>.</summary>
	public const int MaxWindow = 1_000_000;

	private string _name = "";
	private bool _active = true;
	private ProbeLayer _layer = ProbeLayer.Field;
	private ProbeMeasurement _measurement = ProbeMeasurement.Amplitude;
	private ProbeKind _kind = ProbeKind.Point;
	private int _aX;
	private int _aY;
	private int _bX;
	private int _bY;
	private ProbeBoxSize _boxSize = ProbeBoxSize.Medium;
	private int _boxX;
	private int _boxY;
	private ProbeScaleMode _scaleMode = ProbeScaleMode.Running;
	private bool _autoScaleX = true;
	private bool _autoScaleY = true;
	private double _yMin = -1.0;
	private double _yMax = 1.0;
	private int _window = DefaultWindow;
	private int _startAfter;

	/// <summary>Name shown in the project tree and in the graph title.</summary>
	[XmlAttribute]
	[Category("General")]
	[Description("Name shown in the project tree and in the graph title.")]
	public string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	/// <summary>Whether the probe is sampled and drawn.</summary>
	[XmlAttribute]
	[DefaultValue(true)]
	[Category("General")]
	[Description("Sample and draw this probe.")]
	public bool Active
	{
		get => _active;
		set => SetProperty(ref _active, value);
	}

	/// <summary>Output the probe samples and is drawn on.</summary>
	[XmlAttribute]
	[DefaultValue(ProbeLayer.Field)]
	[Category("Measurement")]
	[Description("Output the probe samples and is drawn on.")]
	public ProbeLayer Layer
	{
		get => _layer;
		set => SetProperty(ref _layer, value);
	}

	/// <summary>Recorded quantity; must fit <see cref="Layer"/>, which <see cref="Normalize"/> enforces.</summary>
	[XmlAttribute]
	[DefaultValue(ProbeMeasurement.Amplitude)]
	[Category("Measurement")]
	[Description("Recorded quantity. The field only offers amplitude; the intensity output offers intensity or integrated intensity.")]
	public ProbeMeasurement Measurement
	{
		get => _measurement;
		set => SetProperty(ref _measurement, value);
	}

	/// <summary>A single point, or a line from A to B.</summary>
	[XmlAttribute]
	[DefaultValue(ProbeKind.Point)]
	[Category("Measurement")]
	[Description("A single point, or a line from A to B.")]
	public ProbeKind Kind
	{
		get => _kind;
		set => SetProperty(ref _kind, value);
	}

	/// <summary>X of the probe pixel, or of the start of the line, in canvas pixels.</summary>
	[XmlAttribute]
	[Category("Position")]
	[DisplayName("A x")]
	[Description("X of the probe pixel, or of the start of the line, in canvas pixels.")]
	public int AX
	{
		get => _aX;
		set => SetProperty(ref _aX, value);
	}

	/// <summary>Y of the probe pixel, or of the start of the line, in canvas pixels.</summary>
	[XmlAttribute]
	[Category("Position")]
	[DisplayName("A y")]
	[Description("Y of the probe pixel, or of the start of the line, in canvas pixels.")]
	public int AY
	{
		get => _aY;
		set => SetProperty(ref _aY, value);
	}

	/// <summary>X of the end of the line in canvas pixels; line probes only, omitted from the file otherwise.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Position")]
	[DisplayName("B x")]
	[Description("X of the end of the line in canvas pixels; line probes only.")]
	public int BX
	{
		get => _bX;
		set => SetProperty(ref _bX, value);
	}

	/// <summary>Y of the end of the line in canvas pixels; line probes only, omitted from the file otherwise.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Position")]
	[DisplayName("B y")]
	[Description("Y of the end of the line in canvas pixels; line probes only.")]
	public int BY
	{
		get => _bY;
		set => SetProperty(ref _bY, value);
	}

	/// <summary>Nominal size of the graph box drawn over the output.</summary>
	[XmlAttribute]
	[DefaultValue(ProbeBoxSize.Medium)]
	[Category("Graph")]
	[DisplayName("Box size")]
	[Description("Nominal size of the graph box drawn over the output.")]
	public ProbeBoxSize BoxSize
	{
		get => _boxSize;
		set => SetProperty(ref _boxSize, value);
	}

	/// <summary>Left edge of the graph box in canvas pixels.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box x")]
	[Description("Left edge of the graph box in canvas pixels.")]
	public int BoxX
	{
		get => _boxX;
		set => SetProperty(ref _boxX, value);
	}

	/// <summary>Top edge of the graph box in canvas pixels.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box y")]
	[Description("Top edge of the graph box in canvas pixels.")]
	public int BoxY
	{
		get => _boxY;
		set => SetProperty(ref _boxY, value);
	}

	/// <summary>Sliding window of the last steps, or the whole run; point probes only.</summary>
	[XmlAttribute]
	[DefaultValue(ProbeScaleMode.Running)]
	[Category("Axes")]
	[DisplayName("Step axis")]
	[Description("Sliding window of the last steps, or the whole run; point probes only.")]
	public ProbeScaleMode ScaleMode
	{
		get => _scaleMode;
		set => SetProperty(ref _scaleMode, value);
	}

	/// <summary>Whether the step axis follows the samples recorded so far; point probes only.</summary>
	[XmlAttribute]
	[DefaultValue(true)]
	[Category("Axes")]
	[DisplayName("Auto-scale steps")]
	[Description("Let the step axis follow the samples recorded so far; point probes only.")]
	public bool AutoScaleX
	{
		get => _autoScaleX;
		set => SetProperty(ref _autoScaleX, value);
	}

	/// <summary>Whether the value axis is fitted to the data instead of <see cref="YMin"/>..<see cref="YMax"/>.</summary>
	[XmlAttribute]
	[DefaultValue(true)]
	[Category("Axes")]
	[DisplayName("Auto-scale values")]
	[Description("Fit the value axis to the data instead of the fixed minimum and maximum.")]
	public bool AutoScaleY
	{
		get => _autoScaleY;
		set => SetProperty(ref _autoScaleY, value);
	}

	/// <summary>Lower bound of the value axis when it is not auto-scaled.</summary>
	[XmlAttribute]
	[DefaultValue(-1.0)]
	[Category("Axes")]
	[DisplayName("Value minimum")]
	[Description("Lower bound of the value axis when it is not auto-scaled.")]
	public double YMin
	{
		get => _yMin;
		set => SetProperty(ref _yMin, value);
	}

	/// <summary>Upper bound of the value axis when it is not auto-scaled; must exceed <see cref="YMin"/>.</summary>
	[XmlAttribute]
	[DefaultValue(1.0)]
	[Category("Axes")]
	[DisplayName("Value maximum")]
	[Description("Upper bound of the value axis when it is not auto-scaled.")]
	public double YMax
	{
		get => _yMax;
		set => SetProperty(ref _yMax, value);
	}

	/// <summary>Width of the sliding step window for <see cref="ProbeScaleMode.Running"/>, in steps.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultWindow)]
	[Category("Axes")]
	[DisplayName("Window")]
	[Description("Width of the sliding step window, in steps.")]
	public int Window
	{
		get => _window;
		set => SetProperty(ref _window, value);
	}

	/// <summary>Step at which recording starts; 0 records from the first step.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("General")]
	[DisplayName("Start after step")]
	[Description("Step at which recording starts; 0 records from the first step.")]
	public int StartAfter
	{
		get => _startAfter;
		set => SetProperty(ref _startAfter, value);
	}

	/// <summary>Tells <see cref="XmlSerializer"/> to write B only for line probes.</summary>
	public bool ShouldSerializeBX() => Kind == ProbeKind.Line;

	/// <summary>Tells <see cref="XmlSerializer"/> to write B only for line probes.</summary>
	public bool ShouldSerializeBY() => Kind == ProbeKind.Line;

	/// <summary>
	/// Forces a consistent state: the field only allows <see cref="ProbeMeasurement.Amplitude"/>,
	/// the intensity output never does; an empty or inverted Y range becomes -1..1; the window
	/// and start step are clamped.
	/// </summary>
	public void Normalize()
	{
		Name ??= "";
		if (Layer == ProbeLayer.Field)
		{
			Measurement = ProbeMeasurement.Amplitude;
		}
		else if (Measurement == ProbeMeasurement.Amplitude)
		{
			Measurement = ProbeMeasurement.Intensity;
		}
		if (YMax <= YMin)
		{
			YMin = -1.0;
			YMax = 1.0;
		}
		Window = Math.Clamp(Window, MinWindow, MaxWindow);
		StartAfter = Math.Max(0, StartAfter);
	}

	/// <summary>A copy with the same settings and no change subscribers.</summary>
	public Probe Clone() => ShallowClone<Probe>();

	/// <summary>The name, for display in the project tree and property grids.</summary>
	public override string ToString() => Name;
}

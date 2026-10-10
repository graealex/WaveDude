using System.ComponentModel;
using System.Xml.Serialization;

namespace WaveDude.Model;

/// <summary>Solver parameters and run length.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Simulation : ObservableObject
{
	/// <summary>Default <see cref="Steps"/>.</summary>
	public const int DefaultSteps = 2500;
	/// <summary>Largest accepted <see cref="Steps"/>.</summary>
	public const int MaxSteps = 10_000_000;
	/// <summary>Default <see cref="VisualizeEvery"/>.</summary>
	public const int DefaultVisualizeEvery = 2;
	/// <summary>Largest accepted <see cref="VisualizeEvery"/>.</summary>
	public const int MaxVisualizeEvery = 1000;
	/// <summary>Default <see cref="WaveSpeed"/> in pixels per step.</summary>
	public const double DefaultWaveSpeed = 1.0;
	/// <summary>Default <see cref="TimeDecay"/>.</summary>
	public const double DefaultTimeDecay = 10.0;
	/// <summary>Default <see cref="SourceFrequencyScale"/>.</summary>
	public const double DefaultSourceFrequencyScale = 2.0;
	/// <summary>Default <see cref="IntensityScale"/>.</summary>
	public const double DefaultIntensityScale = 1.0;

	private int _steps = DefaultSteps;
	private int _visualizeEvery = DefaultVisualizeEvery;
	private bool _fourthOrderLaplacian = true;
	private double _waveSpeed = DefaultWaveSpeed;
	private double _timeDecay = DefaultTimeDecay;
	private double _sourceFrequencyScale = DefaultSourceFrequencyScale;
	private double _intensityScale = DefaultIntensityScale;

	/// <summary>Total number of simulation steps.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultSteps)]
	[Category("Run")]
	[Description("Total number of simulation steps.")]
	public int Steps
	{
		get => _steps;
		set => SetProperty(ref _steps, value);
	}

	/// <summary>Render a frame, export it and sample the probes every this many steps.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultVisualizeEvery)]
	[Category("Run")]
	[DisplayName("Visualize every")]
	[Description("Render a frame, export it and sample the probes every this many steps.")]
	public int VisualizeEvery
	{
		get => _visualizeEvery;
		set => SetProperty(ref _visualizeEvery, value);
	}

	/// <summary>Use the 4th-order Laplacian stencil with sub-stepping instead of the 9-point one: more accurate, slower.</summary>
	[XmlAttribute]
	[DefaultValue(true)]
	[Category("Solver")]
	[DisplayName("4th-order Laplacian")]
	[Description("Use the 4th-order stencil with sub-stepping instead of the 9-point stencil. More accurate, slower.")]
	public bool FourthOrderLaplacian
	{
		get => _fourthOrderLaplacian;
		set => SetProperty(ref _fourthOrderLaplacian, value);
	}

	/// <summary>Nominal wave speed in pixels per step; only used to estimate wavelengths in the editor.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultWaveSpeed)]
	[Category("Solver")]
	[DisplayName("Wave speed")]
	[Description("Nominal wave speed in pixels per step; only used to estimate wavelengths in the editor.")]
	public double WaveSpeed
	{
		get => _waveSpeed;
		set => SetProperty(ref _waveSpeed, value);
	}

	/// <summary>Averaging window factor of the intensity: the engine averages over this × 100 / lowest source level steps.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultTimeDecay)]
	[Category("Solver")]
	[DisplayName("Intensity time decay")]
	[Description("Averaging window factor of the intensity: the engine averages over this × 100 / lowest source level steps.")]
	public double TimeDecay
	{
		get => _timeDecay;
		set => SetProperty(ref _timeDecay, value);
	}

	/// <summary>Multiplier of the level-to-frequency mapping: ω = level / 255 × 0.5 × scale, in radians per step.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultSourceFrequencyScale)]
	[Category("Solver")]
	[DisplayName("Source frequency scale")]
	[Description("Multiplier of the level-to-frequency mapping: ω = level / 255 × 0.5 × scale, in radians per step.")]
	public double SourceFrequencyScale
	{
		get => _sourceFrequencyScale;
		set => SetProperty(ref _sourceFrequencyScale, value);
	}

	/// <summary>Multiplier applied to the intensity values the engine produces.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultIntensityScale)]
	[Category("Solver")]
	[DisplayName("Intensity scale")]
	[Description("Multiplier applied to the intensity values the engine produces.")]
	public double IntensityScale
	{
		get => _intensityScale;
		set => SetProperty(ref _intensityScale, value);
	}

	/// <summary>True when every value is at its default, so the element can be left out of the file.</summary>
	[XmlIgnore]
	[Browsable(false)]
	public bool IsDefault =>
		Steps == DefaultSteps && VisualizeEvery == DefaultVisualizeEvery && FourthOrderLaplacian
		&& WaveSpeed == DefaultWaveSpeed && TimeDecay == DefaultTimeDecay
		&& SourceFrequencyScale == DefaultSourceFrequencyScale && IntensityScale == DefaultIntensityScale;

	/// <summary>Clamps every value into its valid range.</summary>
	public void Normalize()
	{
		Steps = Math.Clamp(Steps, 1, MaxSteps);
		VisualizeEvery = Math.Clamp(VisualizeEvery, 1, MaxVisualizeEvery);
		WaveSpeed = Math.Clamp(WaveSpeed, 1e-6, 1e6);
		TimeDecay = Math.Clamp(TimeDecay, 1.0, 100.0);
		SourceFrequencyScale = Math.Clamp(SourceFrequencyScale, 0.0, 1000.0);
		IntensityScale = Math.Clamp(IntensityScale, 1e-7, 1e6);
	}

	/// <summary>Step count and engine, for display in a property grid.</summary>
	public override string ToString() => $"{Steps} steps";
}

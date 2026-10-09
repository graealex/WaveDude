using System.ComponentModel;
using System.Xml.Serialization;

namespace WaveDude.Model;

/// <summary>How the two outputs, field and intensity, are rendered, previewed and exported.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Display : ObservableObject
{
	/// <summary>Colour scheme name used for the field when none is given.</summary>
	public const string DefaultFieldColorScheme = "Default_Field";
	/// <summary>Colour scheme name used for the intensity when none is given.</summary>
	public const string DefaultIntensityColorScheme = "Default_intensity";
	/// <summary>Black level of the field layer in a new project.</summary>
	public const double DefaultFieldBlackLevel = -0.05;

	private DisplayLayer _field = new() { ColorScheme = DefaultFieldColorScheme, BlackLevel = DefaultFieldBlackLevel };
	private DisplayLayer _intensity = new() { ColorScheme = DefaultIntensityColorScheme };
	private Video _video = new();
	private int _exportFrom;
	private int _exportTo;

	/// <summary>Rendering of the wave field.</summary>
	[XmlElement]
	[Category("Outputs")]
	[Description("Rendering of the wave field.")]
	public DisplayLayer Field
	{
		get => _field;
		set => SetProperty(ref _field, value);
	}

	/// <summary>Rendering of the accumulated intensity.</summary>
	[XmlElement]
	[Category("Outputs")]
	[Description("Rendering of the accumulated intensity.")]
	public DisplayLayer Intensity
	{
		get => _intensity;
		set => SetProperty(ref _intensity, value);
	}

	/// <summary>Encoder settings of exported videos.</summary>
	[XmlElement]
	[Category("Export")]
	[Description("Encoder settings of exported videos.")]
	public Video Video
	{
		get => _video;
		set => SetProperty(ref _video, value);
	}

	/// <summary>First simulation step of the exported range (and of loop playback); 0 is the start of the run.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Export")]
	[DisplayName("From step")]
	[Description("First simulation step of the exported range; 0 is the start of the run.")]
	public int ExportFrom
	{
		get => _exportFrom;
		set => SetProperty(ref _exportFrom, value);
	}

	/// <summary>Last simulation step of the exported range (and of loop playback); 0 means the end of the run.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Export")]
	[DisplayName("To step")]
	[Description("Last simulation step of the exported range; 0 means the end of the run.")]
	public int ExportTo
	{
		get => _exportTo;
		set => SetProperty(ref _exportTo, value);
	}

	/// <summary>Tells <see cref="XmlSerializer"/> to leave all-default video settings out of the file.</summary>
	public bool ShouldSerializeVideo() => Video.Fps != Video.DefaultFps || Video.Quality != Video.DefaultQuality;

	/// <summary>Clamps every value into its valid range.</summary>
	public void Normalize()
	{
		Field.Normalize(DefaultFieldColorScheme);
		Intensity.Normalize(DefaultIntensityColorScheme);
		Video.Normalize();
		ExportFrom = Math.Max(0, ExportFrom);
		ExportTo = Math.Max(0, ExportTo);
		if (ExportTo != 0 && ExportTo < ExportFrom)
		{
			ExportTo = 0;
		}
	}

	/// <summary>The two colour scheme names, for display in a property grid.</summary>
	public override string ToString() => $"{Field.ColorScheme} / {Intensity.ColorScheme}";
}

/// <summary>Rendering of one output (field or intensity).</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class DisplayLayer : ObservableObject
{
	/// <summary>Default <see cref="Brightness"/>.</summary>
	public const double DefaultBrightness = 30.0;

	private string _colorScheme = "";
	private double _brightness = DefaultBrightness;
	private double _blackLevel;
	private bool _preview = true;
	private bool _export;

	/// <summary>Name of the colour scheme mapped onto the values.</summary>
	[XmlAttribute]
	[Category("Rendering")]
	[DisplayName("Colour scheme")]
	[Description("Name of the colour scheme mapped onto the values.")]
	public string ColorScheme
	{
		get => _colorScheme;
		set => SetProperty(ref _colorScheme, value);
	}

	/// <summary>Gain applied to the values before the colour lookup.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultBrightness)]
	[Category("Rendering")]
	[Description("Gain applied to the values before the colour lookup.")]
	public double Brightness
	{
		get => _brightness;
		set => SetProperty(ref _brightness, value);
	}

	/// <summary>Offset applied before the colour lookup; values at or below it map to the first colour.</summary>
	[XmlAttribute]
	[DefaultValue(0.0)]
	[Category("Rendering")]
	[DisplayName("Black level")]
	[Description("Offset applied before the colour lookup; values at or below it map to the first colour.")]
	public double BlackLevel
	{
		get => _blackLevel;
		set => SetProperty(ref _blackLevel, value);
	}

	/// <summary>Whether the output is shown in the live preview.</summary>
	[XmlAttribute]
	[DefaultValue(true)]
	[Category("Output")]
	[Description("Show this output in the live preview.")]
	public bool Preview
	{
		get => _preview;
		set => SetProperty(ref _preview, value);
	}

	/// <summary>Whether the output's frames are written to a video file.</summary>
	[XmlAttribute]
	[DefaultValue(false)]
	[Category("Output")]
	[Description("Write this output's frames to a video file.")]
	public bool Export
	{
		get => _export;
		set => SetProperty(ref _export, value);
	}

	/// <summary>Clamps every value into its valid range; an empty scheme name falls back to <paramref name="defaultScheme"/>.</summary>
	public void Normalize(string defaultScheme)
	{
		if (string.IsNullOrWhiteSpace(ColorScheme))
		{
			ColorScheme = defaultScheme;
		}
		Brightness = Math.Clamp(Brightness, 1e-4, 1e5);
		BlackLevel = Math.Clamp(BlackLevel, -10.0, 1.0);
	}

	/// <summary>The colour scheme name, for display in a property grid.</summary>
	public override string ToString() => ColorScheme;
}

/// <summary>Encoder settings of exported videos.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Video : ObservableObject
{
	/// <summary>Default <see cref="Fps"/>.</summary>
	public const int DefaultFps = 60;
	/// <summary>Largest accepted <see cref="Fps"/>.</summary>
	public const int MaxFps = 240;
	/// <summary>Default <see cref="Quality"/>.</summary>
	public const int DefaultQuality = 70;

	private int _fps = DefaultFps;
	private int _quality = DefaultQuality;

	/// <summary>Frames per second of the exported video.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultFps)]
	[Category("Video")]
	[DisplayName("Frame rate")]
	[Description("Frames per second of the exported video.")]
	public int Fps
	{
		get => _fps;
		set => SetProperty(ref _fps, value);
	}

	/// <summary>Encoder quality, 1 to 100.</summary>
	[XmlAttribute]
	[DefaultValue(DefaultQuality)]
	[Category("Video")]
	[Description("Encoder quality, 1 to 100.")]
	public int Quality
	{
		get => _quality;
		set => SetProperty(ref _quality, value);
	}

	/// <summary>Clamps every value into its valid range.</summary>
	public void Normalize()
	{
		Fps = Math.Clamp(Fps, 1, MaxFps);
		Quality = Math.Clamp(Quality, 1, 100);
	}

	/// <summary>Frame rate and quality, for display in a property grid.</summary>
	public override string ToString() => $"{Fps} fps, quality {Quality}";
}

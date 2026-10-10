using NLog;
using WaveDude.Rendering;

namespace WaveDude.WPF.Rendering;

/// <summary>
/// The colour schemes: the user's own from <see cref="ColorSchemeStore.UserFolder"/> first (a user scheme
/// with a built-in name shadows it), then the ones built into the library, loaded once, and a cache of the
/// palettes built from them for the display settings in use. UI thread only; the cached <c>int[]</c>
/// palettes are immutable and may be handed to rendering threads.
/// </summary>
public sealed class ColorSchemeCatalog
{
	private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

	/// <summary>The single catalog of the application.</summary>
	public static ColorSchemeCatalog Instance { get; } = new();

	private readonly Dictionary<(string Name, double BlackLevel), int[]?> _luts = new();
	private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
	private List<ColorScheme>? _schemes;

	private ColorSchemeCatalog()
	{
	}

	/// <summary>Reads the user's folder and the built-in resources; each unreadable one is logged as a warning. Called at start-up, or lazily on first use.</summary>
	public void Load()
	{
		var errors = new List<string>();
		List<ColorScheme> user = ColorSchemeStore.LoadFolder(ColorSchemeStore.UserFolder, errors);
		List<ColorScheme> builtIn = ColorSchemeStore.LoadBuiltIn(errors);
		_schemes = [.. user, .. builtIn];
		foreach (string error in errors)
		{
			Logger.Warn("Colour scheme not loaded: {Error}", error);
		}
		Logger.Info("{BuiltIn} built-in colour schemes, {User} from '{Folder}'", builtIn.Count, user.Count, ColorSchemeStore.UserFolder);
		_luts.Clear();
	}

	/// <summary>Every scheme that loaded, in file name order.</summary>
	public IReadOnlyList<ColorScheme> Schemes
	{
		get
		{
			if (_schemes == null)
			{
				Load();
			}
			return _schemes!;
		}
	}

	/// <summary>
	/// The palette of the scheme called <paramref name="name"/> with the given black level, cached; null
	/// (greyscale) when no such scheme exists, which is logged once per name.
	/// </summary>
	public int[]? LutFor(string name, double blackLevel)
	{
		(string, double) key = (name, blackLevel);
		if (_luts.TryGetValue(key, out int[]? lut))
		{
			return lut;
		}
		ColorScheme? scheme = ColorSchemeStore.Find(Schemes, name);
		if (scheme == null)
		{
			if (_warned.Add(name))
			{
				Logger.Warn("Colour scheme '{Name}' not found; showing greyscale", name);
			}
			lut = null;
		}
		else
		{
			lut = ColorLut.Build(scheme, blackLevel: blackLevel).Argb;
		}
		_luts[key] = lut;
		return lut;
	}
}

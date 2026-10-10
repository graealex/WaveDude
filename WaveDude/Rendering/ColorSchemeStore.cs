using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace WaveDude.Rendering;

/// <summary>
/// Reads and writes <see cref="ColorScheme"/> objects as JSON files (one file per scheme, e.g. in the
/// scheme folder).
/// </summary>
/// <remarks>
/// File format, the same as the files exported from Python (e.g. in its <c>color_schemes</c> folder): an
/// object with "name", "origin", optional "category" and "scheme_type" ("field" or "intensity"),
/// "bits_per_channel" (always 8), "entry_count" (<see cref="ColorScheme.EntryCount"/>) and a "colors"
/// list of <c>[r,g,b]</c> triplets, plus, only for schemes made in the stop editor, a "stops" list of
/// <see cref="ColorScheme.StopCount"/> triplets. Reading uses <see cref="JsonDocument"/>; writing emits
/// the text by hand so the file stays human-readable and byte-identical to the shipped files. The shipped
/// schemes are embedded in this assembly (<see cref="LoadBuiltIn"/>; the repository's <c>ColorSchemeList</c>
/// folder is their source); the user's own schemes live in <see cref="UserFolder"/>. Called only from the
/// UI thread (start-up and a scheme editor).
/// </remarks>
public static class ColorSchemeStore
{
	/// <summary>Prefix of the embedded scheme resources (see the csproj's <c>LogicalName</c>).</summary>
	private const string ResourcePrefix = "ColorSchemes/";

	/// <summary>The user's own scheme folder, <c>%LOCALAPPDATA%\WaveDude\ColorSchemes</c>; created on first use by <see cref="Save"/>, may not exist.</summary>
	public static string UserFolder { get; } =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WaveDude", "ColorSchemes");

	/// <summary>
	/// The schemes shipped with the program, read from this assembly's resources in name order. A resource that
	/// does not parse is reported in <paramref name="errors"/> (a build problem, not a user one) and skipped.
	/// </summary>
	/// <param name="errors">Receives one line per unreadable resource; may be null to discard them.</param>
	public static List<ColorScheme> LoadBuiltIn(List<string>? errors)
	{
		var schemes = new List<ColorScheme>();
		Assembly assembly = typeof(ColorSchemeStore).Assembly;
		var names = assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).ToList();
		names.Sort(StringComparer.OrdinalIgnoreCase);
		foreach (string name in names)
		{
			try
			{
				using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidDataException("The resource stream is missing.");
				using var reader = new StreamReader(stream, Encoding.UTF8);
				ColorScheme scheme = Parse(reader.ReadToEnd(), Path.GetFileNameWithoutExtension(name[ResourcePrefix.Length..]));
				scheme.IsBuiltIn = true;
				schemes.Add(scheme);
			}
			catch (Exception ex) when (ex is InvalidDataException or JsonException)
			{
				errors?.Add(name + ": " + ex.Message);
			}
		}
		return schemes;
	}

	/// <summary>
	/// Reads one scheme file. The table comes from "colors" (resampled to 255 entries) when that list has at
	/// least two entries, otherwise it is interpolated from "stops"; a "stops" list is attached as editable
	/// stops only if it has exactly <see cref="ColorScheme.StopCount"/> entries (otherwise it is ignored).
	/// "name" falls back to the file name, "scheme_type" is normalised (see <see cref="NormalizeSchemeType"/>),
	/// and <see cref="ColorScheme.FilePath"/> is set to <paramref name="filePath"/>.
	/// </summary>
	/// <param name="filePath">Full path of the JSON file.</param>
	/// <exception cref="InvalidDataException">The file is not a JSON object, has no usable color list or contains malformed triplets.</exception>
	/// <exception cref="JsonException">The file is not valid JSON.</exception>
	public static ColorScheme Load(string filePath)
	{
		ColorScheme scheme = Parse(File.ReadAllText(filePath, Encoding.UTF8), Path.GetFileNameWithoutExtension(filePath));
		scheme.FilePath = filePath;
		return scheme;
	}

	/// <summary>Parses scheme JSON; <paramref name="fallbackName"/> stands in for a missing "name".</summary>
	/// <exception cref="InvalidDataException">Not a JSON object, no usable color list or malformed triplets.</exception>
	/// <exception cref="JsonException">Not valid JSON.</exception>
	public static ColorScheme Parse(string json, string fallbackName)
	{
		using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("Not a color scheme file (a JSON object was expected).");
		}
		string schemeName = GetString(root, "name", fallbackName);
		List<Rgb>? colors = root.TryGetProperty("colors", out JsonElement colorsNode) ? ParseColorList(colorsNode) : null;
		List<Rgb>? stops = root.TryGetProperty("stops", out JsonElement stopsNode) ? ParseColorList(stopsNode) : null;
		if (stops != null && stops.Count != ColorScheme.StopCount)
		{
			stops = null;
		}
		ColorScheme scheme;
		if (colors != null && colors.Count >= 2)
		{
			scheme = new ColorScheme(schemeName, colors);
		}
		else if (stops != null)
		{
			scheme = ColorScheme.FromStops(schemeName, stops);
		}
		else
		{
			throw new InvalidDataException("The file contains no usable 'colors' list.");
		}
		if (stops != null)
		{
			scheme.AttachStops(stops);
		}
		scheme.Origin = GetString(root, "origin", scheme.Origin);
		scheme.Category = GetString(root, "category", scheme.Category);
		scheme.SchemeType = NormalizeSchemeType(GetString(root, "scheme_type", scheme.SchemeType));
		return scheme;
	}

	/// <summary>
	/// Loads every <c>*.json</c> file in <paramref name="folder"/>, in case-insensitive file name order.
	/// Files that fail to load are skipped and reported as "file: message" lines in <paramref name="errors"/>.
	/// </summary>
	/// <param name="folder">Folder to scan; an empty or missing folder yields an empty list.</param>
	/// <param name="errors">Receives one line per unreadable file; may be null to discard them.</param>
	/// <returns>The schemes that loaded successfully.</returns>
	public static List<ColorScheme> LoadFolder(string folder, List<string>? errors)
	{
		var schemes = new List<ColorScheme>();
		if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
		{
			return schemes;
		}
		var files = new List<string>(Directory.GetFiles(folder, "*.json"));
		files.Sort(StringComparer.OrdinalIgnoreCase);
		foreach (string file in files)
		{
			try
			{
				schemes.Add(Load(file));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
			{
				errors?.Add(Path.GetFileName(file) + ": " + ex.Message);
			}
		}
		return schemes;
	}

	/// <summary>The scheme called <paramref name="name"/> (case-insensitive, so a file's "name" matches the model's stored name regardless of letter case), or null.</summary>
	public static ColorScheme? Find(IEnumerable<ColorScheme> schemes, string name) =>
		schemes.FirstOrDefault(scheme => string.Equals(scheme.Name, name, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Writes <paramref name="scheme"/> as a UTF-8 (no BOM) JSON file, eight color triplets per line, with
	/// "category" / "scheme_type" omitted when empty and "stops" present only for stop schemes. Creates the
	/// target directory, writes to <c>&lt;filePath&gt;.tmp</c> first and then replaces the target, and finally
	/// sets <see cref="ColorScheme.FilePath"/> on the scheme.
	/// </summary>
	/// <param name="scheme">Scheme to write.</param>
	/// <param name="filePath">Destination file path (usually from <see cref="PathFor"/>).</param>
	public static void Save(ColorScheme scheme, string filePath)
	{
		var json = new StringBuilder();
		json.AppendLine("{");
		json.AppendLine("  \"name\": " + Quote(scheme.Name) + ",");
		json.AppendLine("  \"origin\": " + Quote(scheme.Origin) + ",");
		if (!string.IsNullOrEmpty(scheme.Category))
		{
			json.AppendLine("  \"category\": " + Quote(scheme.Category) + ",");
		}
		if (!string.IsNullOrEmpty(scheme.SchemeType))
		{
			json.AppendLine("  \"scheme_type\": " + Quote(scheme.SchemeType) + ",");
		}
		json.AppendLine("  \"bits_per_channel\": 8,");
		json.AppendLine("  \"entry_count\": " + ColorScheme.EntryCount.ToString(CultureInfo.InvariantCulture) + ",");
		if (scheme.Stops is { } stops)
		{
			json.AppendLine("  \"stops\": [" + string.Join(", ", stops) + "],");
		}
		Rgb[] entries = scheme.Entries;
		json.AppendLine("  \"colors\": [");
		for (int rowStart = 0; rowStart < entries.Length; rowStart += 8)
		{
			int rowEnd = Math.Min(rowStart + 7, entries.Length - 1);
			json.Append("    ");
			json.Append(string.Join(", ", entries[rowStart..(rowEnd + 1)]));
			if (rowEnd < entries.Length - 1)
			{
				json.Append(',');
			}
			json.AppendLine();
		}
		json.AppendLine("  ]");
		json.AppendLine("}");
		string? directoryName = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		// write to a temporary file first so a failed save never leaves a half-written scheme behind
		string tempPath = filePath + ".tmp";
		File.WriteAllText(tempPath, json.ToString(), new UTF8Encoding(false));
		File.Move(tempPath, filePath, overwrite: true);
		scheme.FilePath = filePath;
	}

	/// <summary>
	/// Turns a scheme name into a file name (without extension): invalid file name characters become
	/// '_', surrounding whitespace and trailing dots are removed.
	/// </summary>
	/// <returns>The sanitised name, or "color_scheme" when nothing usable is left.</returns>
	public static string SafeFileName(string schemeName)
	{
		if (string.IsNullOrEmpty(schemeName))
		{
			return "color_scheme";
		}
		char[] invalidChars = Path.GetInvalidFileNameChars();
		var safeName = new StringBuilder();
		foreach (char c in schemeName.Trim())
		{
			safeName.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
		}
		string result = safeName.ToString().Trim().TrimEnd('.');
		return result.Length == 0 ? "color_scheme" : result;
	}

	/// <summary>The file path a scheme named <paramref name="schemeName"/> is stored at in <paramref name="folder"/>: <see cref="SafeFileName"/> plus ".json".</summary>
	public static string PathFor(string folder, string schemeName) => Path.Combine(folder, SafeFileName(schemeName) + ".json");

	/// <summary>Maps a raw "scheme_type" value to <see cref="ColorScheme.SchemeTypeField"/> or <see cref="ColorScheme.SchemeTypeIntensity"/> (trimmed, case-insensitive); anything else becomes "".</summary>
	private static string NormalizeSchemeType(string value)
	{
		string normalized = value.Trim().ToLowerInvariant();
		return normalized is ColorScheme.SchemeTypeField or ColorScheme.SchemeTypeIntensity ? normalized : "";
	}

	/// <summary>
	/// Reads <paramref name="key"/> from the JSON object as a string (numbers and booleans are converted with
	/// the invariant culture), or <paramref name="fallback"/> when the key is missing or null.
	/// </summary>
	private static string GetString(JsonElement root, string key, string fallback)
	{
		if (!root.TryGetProperty(key, out JsonElement value))
		{
			return fallback;
		}
		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? fallback,
			JsonValueKind.Number => value.GetDouble().ToString(CultureInfo.InvariantCulture),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			_ => fallback,
		};
	}

	/// <summary>
	/// Converts a JSON list of <c>[r,g,b]</c> lists into colors. Each inner list needs at least three
	/// numbers (extra values are ignored); non-integers are rounded and every channel is clamped to 0..255.
	/// </summary>
	/// <param name="node">The value of "colors" or "stops".</param>
	/// <exception cref="InvalidDataException">The node or one of its items is not a list, a triplet is too short or a channel is not a number.</exception>
	private static List<Rgb> ParseColorList(JsonElement node)
	{
		if (node.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidDataException("Expected a list of [r,g,b] triplets.");
		}
		var colors = new List<Rgb>();
		foreach (JsonElement item in node.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Array)
			{
				throw new InvalidDataException("Expected a list of [r,g,b] triplets.");
			}
			var values = new List<int>();
			foreach (JsonElement channel in item.EnumerateArray())
			{
				if (channel.ValueKind != JsonValueKind.Number)
				{
					throw new InvalidDataException("A color channel must be a number.");
				}
				values.Add((int)Math.Round(channel.GetDouble(), MidpointRounding.AwayFromZero));
			}
			if (values.Count < 3)
			{
				throw new InvalidDataException("A color needs three values [r,g,b].");
			}
			colors.Add(Rgb.Clamped(values[0], values[1], values[2]));
		}
		return colors;
	}

	/// <summary>Returns <paramref name="text"/> as a double-quoted JSON string literal; escapes quotes, backslashes and control characters, leaves non-ASCII as UTF-8.</summary>
	private static string Quote(string text)
	{
		var quoted = new StringBuilder();
		quoted.Append('"');
		foreach (char c in text)
		{
			switch (c)
			{
				case '"':
					quoted.Append("\\\"");
					break;
				case '\\':
					quoted.Append("\\\\");
					break;
				case '\t':
					quoted.Append("\\t");
					break;
				case '\n':
					quoted.Append("\\n");
					break;
				case '\r':
					quoted.Append("\\r");
					break;
				default:
					if (c < ' ')
					{
						quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						quoted.Append(c);
					}
					break;
			}
		}
		quoted.Append('"');
		return quoted.ToString();
	}

}

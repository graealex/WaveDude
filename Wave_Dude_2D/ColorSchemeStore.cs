using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Reads and writes <see cref="ColorScheme"/> objects as JSON files in the scheme folder.
	/// </summary>
	/// <remarks>
	/// Static-only helper (private constructor). File layout: an object with "name", "origin", optional
	/// "category" and "scheme_type", "bits_per_channel" (always 8), "entry_count" (255), an optional "stops"
	/// list of 9 <c>[r,g,b]</c> triplets and a "colors" list of triplets. Reading uses
	/// <see cref="JavaScriptSerializer"/>; writing emits the text by hand so the file stays human-readable.
	/// Called only from the UI thread (color scheme tab and start-up).
	/// </remarks>
	public sealed class ColorSchemeStore
	{
		/// <summary>Prevents instantiation; all members are static.</summary>
		private ColorSchemeStore()
		{
		}

		/// <summary>
		/// Returns the ColorSchemeList folder next to the executable, or the one next to the project
		/// file when running from the build output. The folder is created if it does not exist.
		/// </summary>
		/// <returns>
		/// The folder path. It walks up to five directory levels from the base directory looking for a
		/// <c>*.vbproj</c> (the original VB.NET project file) and prefers a <see cref="SchemeFolderName"/>
		/// folder beside it. Any I/O error is swallowed and the default path is returned uncreated.
		/// </returns>
		public static string FindDefaultFolder()
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string folder = Path.Combine(baseDirectory, SchemeFolderName);
			try
			{
				DirectoryInfo dir = new DirectoryInfo(baseDirectory);
				for (int level = 0; level <= 4 && dir != null; level++)
				{
					if (dir.GetFiles("*.vbproj").Length > 0)
					{
						string candidate = Path.Combine(dir.FullName, SchemeFolderName);
						if (Directory.Exists(candidate))
						{
							folder = candidate;
						}
						break;
					}
					dir = dir.Parent;
				}
				if (!Directory.Exists(folder))
				{
					Directory.CreateDirectory(folder);
				}
			}
			catch
			{
			}
			return folder;
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
		public static ColorScheme Load(string filePath)
		{
			string json = File.ReadAllText(filePath, Encoding.UTF8);
			Dictionary<string, object> root = new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			}.DeserializeObject(json) as Dictionary<string, object>;
			if (root == null)
			{
				throw new InvalidDataException("Not a color scheme file (a JSON object was expected).");
			}
			string name = ColorSchemeStore.GetString(root, "name", Path.GetFileNameWithoutExtension(filePath));
			List<Color> colors = null;
			if (root.ContainsKey("colors"))
			{
				colors = ColorSchemeStore.ParseColorList(root["colors"]);
			}
			List<Color> stops = null;
			if (root.ContainsKey("stops"))
			{
				stops = ColorSchemeStore.ParseColorList(root["stops"]);
			}
			if (stops != null && stops.Count != ColorScheme.StopCount)
			{
				stops = null;
			}
			ColorScheme scheme;
			if (colors != null && colors.Count >= 2)
			{
				scheme = new ColorScheme(name, colors);
			}
			else if (stops != null)
			{
				scheme = ColorScheme.FromStops(name, stops);
			}
			else
			{
				throw new InvalidDataException("The file contains no usable 'colors' list.");
			}
			if (stops != null)
			{
				scheme.AttachStops(stops);
			}
			scheme.Origin = ColorSchemeStore.GetString(root, "origin", scheme.Origin);
			scheme.Category = ColorSchemeStore.GetString(root, "category", scheme.Category);
			scheme.SchemeType = ColorSchemeStore.NormalizeSchemeType(ColorSchemeStore.GetString(root, "scheme_type", scheme.SchemeType));
			scheme.FilePath = filePath;
			return scheme;
		}

		/// <summary>
		/// Loads every <c>*.json</c> file in <paramref name="folder"/>, in case-insensitive file name order.
		/// Files that fail to load are skipped and reported as "file: message" lines in <paramref name="errors"/>.
		/// </summary>
		/// <param name="folder">Folder to scan; a null, empty or missing folder yields an empty list.</param>
		/// <param name="errors">Receives one line per unreadable file; may be null to discard them.</param>
		/// <returns>The schemes that loaded successfully (never null).</returns>
		public static List<ColorScheme> LoadFolder(string folder, List<string> errors)
		{
			List<ColorScheme> schemes = new List<ColorScheme>();
			if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
			{
				return schemes;
			}
			List<string> files = new List<string>(Directory.GetFiles(folder, "*.json"));
			files.Sort(StringComparer.OrdinalIgnoreCase);
			foreach (string file in files)
			{
				try
				{
					schemes.Add(ColorSchemeStore.Load(file));
				}
				catch (Exception ex)
				{
					if (errors != null)
					{
						errors.Add(Path.GetFileName(file) + ": " + ex.Message);
					}
				}
			}
			return schemes;
		}

		/// <summary>
		/// Writes <paramref name="scheme"/> as a UTF-8 (no BOM) JSON file, eight color triplets per line, with
		/// "category" / "scheme_type" omitted when empty and "stops" present only for stop schemes. Creates the
		/// target directory, writes to <c>&lt;filePath&gt;.tmp</c> first and then replaces the target, and finally
		/// sets <see cref="ColorScheme.FilePath"/> on the scheme.
		/// </summary>
		/// <param name="scheme">Scheme to write.</param>
		/// <param name="filePath">Destination file path (usually from <see cref="PathFor"/>).</param>
		/// <exception cref="ArgumentNullException"><paramref name="scheme"/> is null.</exception>
		public static void Save(ColorScheme scheme, string filePath)
		{
			if (scheme == null)
			{
				throw new ArgumentNullException("scheme");
			}
			StringBuilder json = new StringBuilder();
			json.AppendLine("{");
			json.AppendLine("  \"name\": " + ColorSchemeStore.Quote(scheme.Name) + ",");
			json.AppendLine("  \"origin\": " + ColorSchemeStore.Quote(scheme.Origin) + ",");
			if (!string.IsNullOrEmpty(scheme.Category))
			{
				json.AppendLine("  \"category\": " + ColorSchemeStore.Quote(scheme.Category) + ",");
			}
			if (!string.IsNullOrEmpty(scheme.SchemeType))
			{
				json.AppendLine("  \"scheme_type\": " + ColorSchemeStore.Quote(scheme.SchemeType) + ",");
			}
			json.AppendLine("  \"bits_per_channel\": 8,");
			json.AppendLine("  \"entry_count\": " + ColorScheme.EntryCount.ToString(CultureInfo.InvariantCulture) + ",");
			if (scheme.HasStops)
			{
				List<string> stopTriplets = new List<string>();
				foreach (Color stop in scheme.Stops)
				{
					stopTriplets.Add(ColorSchemeStore.FormatRgb(stop));
				}
				json.AppendLine("  \"stops\": [" + string.Join(", ", stopTriplets) + "],");
			}
			Color[] entries = scheme.Entries;
			json.AppendLine("  \"colors\": [");
			for (int rowStart = 0; rowStart < entries.Length; rowStart += 8)
			{
				int rowEnd = Math.Min(rowStart + 7, entries.Length - 1);
				List<string> rowTriplets = new List<string>();
				for (int k = rowStart; k <= rowEnd; k++)
				{
					rowTriplets.Add(ColorSchemeStore.FormatRgb(entries[k]));
				}
				json.Append("    ");
				json.Append(string.Join(", ", rowTriplets));
				if (rowEnd < entries.Length - 1)
				{
					json.Append(",");
				}
				json.AppendLine();
			}
			json.AppendLine("  ]");
			json.AppendLine("}");
			string directoryName = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string tempPath = filePath + ".tmp";
			File.WriteAllText(tempPath, json.ToString(), new UTF8Encoding(false));
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
			File.Move(tempPath, filePath);
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
			StringBuilder safeName = new StringBuilder();
			foreach (char c in schemeName.Trim())
			{
				safeName.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
			}
			string result = safeName.ToString().Trim().TrimEnd('.');
			return result.Length == 0 ? "color_scheme" : result;
		}

		/// <summary>The file path a scheme named <paramref name="schemeName"/> is stored at in <paramref name="folder"/>: <see cref="SafeFileName"/> plus ".json".</summary>
		public static string PathFor(string folder, string schemeName)
		{
			return Path.Combine(folder, ColorSchemeStore.SafeFileName(schemeName) + ".json");
		}

		/// <summary>Maps a raw "scheme_type" value to <see cref="ColorScheme.SchemeTypeField"/> or <see cref="ColorScheme.SchemeTypeIntensity"/> (trimmed, case-insensitive); anything else, including null, becomes "".</summary>
		private static string NormalizeSchemeType(string value)
		{
			if (value == null)
			{
				return "";
			}
			string normalized = value.Trim().ToLowerInvariant();
			if (normalized == ColorScheme.SchemeTypeField || normalized == ColorScheme.SchemeTypeIntensity)
			{
				return normalized;
			}
			return "";
		}

		/// <summary>
		/// Reads <paramref name="key"/> from the JSON object as a string (non-string scalars are converted with
		/// the invariant culture), or <paramref name="fallback"/> when the key is missing or null.
		/// </summary>
		private static string GetString(Dictionary<string, object> root, string key, string fallback)
		{
			object value;
			if (root.TryGetValue(key, out value) && value != null)
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture);
			}
			return fallback;
		}

		/// <summary>
		/// Converts a deserialised JSON list of <c>[r,g,b]</c> lists into colors. Each inner list needs at least
		/// three numbers (extra values are ignored); non-integers are rounded and every channel is clamped to
		/// 0..255. Strings are rejected even though they enumerate as characters.
		/// </summary>
		/// <param name="node">The value of "colors" or "stops" as returned by <see cref="JavaScriptSerializer"/>.</param>
		/// <exception cref="InvalidDataException">The node or one of its items is not a list, or a triplet is too short.</exception>
		private static List<Color> ParseColorList(object node)
		{
			IEnumerable items = node as IEnumerable;
			if (items == null || node is string)
			{
				throw new InvalidDataException("Expected a list of [r,g,b] triplets.");
			}
			List<Color> colors = new List<Color>();
			foreach (object item in items)
			{
				IEnumerable channels = item as IEnumerable;
				if (channels == null || item is string)
				{
					throw new InvalidDataException("Expected a list of [r,g,b] triplets.");
				}
				List<int> values = new List<int>();
				foreach (object channel in channels)
				{
					values.Add(Convert.ToInt32(channel, CultureInfo.InvariantCulture));
				}
				if (values.Count < 3)
				{
					throw new InvalidDataException("A color needs three values [r,g,b].");
				}
				colors.Add(Color.FromArgb(ColorSchemeStore.Clamp255(values[0]), ColorSchemeStore.Clamp255(values[1]), ColorSchemeStore.Clamp255(values[2])));
			}
			return colors;
		}

		/// <summary>Clamps a channel value to 0..255.</summary>
		private static int Clamp255(int v)
		{
			if (v < 0)
			{
				return 0;
			}
			if (v > 255)
			{
				return 255;
			}
			return v;
		}

		/// <summary>Formats a color as the JSON triplet <c>[r,g,b]</c> (no spaces, alpha dropped).</summary>
		private static string FormatRgb(Color c)
		{
			return string.Format(CultureInfo.InvariantCulture, "[{0},{1},{2}]", c.R, c.G, c.B);
		}

		/// <summary>Returns <paramref name="text"/> as a double-quoted JSON string literal (null becomes <c>""</c>); escapes quotes, backslashes and control characters, leaves non-ASCII as UTF-8.</summary>
		private static string Quote(string text)
		{
			if (text == null)
			{
				return "\"\"";
			}
			StringBuilder quoted = new StringBuilder();
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

		/// <summary>Name of the scheme folder looked for next to the executable / project: "ColorSchemeList".</summary>
		public const string SchemeFolderName = "ColorSchemeList";
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The contents of a project's configuration.json: a flat dictionary of top-level values
	/// (format version, project name, created/modified timestamps, description; always present) plus named
	/// sub-dictionaries ("sections") that <see cref="SceneSettings"/>, <see cref="SimulationSettings"/>,
	/// <see cref="ProbeSettings"/> and <see cref="SourceSettings"/> read and write through
	/// <see cref="GetSection"/>. Every tab that gets functionality later stores its own settings as a section
	/// the same way, for example:
	/// <code>
	/// var section = project.Config.GetSection("probes");
	/// section["count"] = 3;
	/// project.Save();
	/// </code>
	/// Forward compatibility: keys that this version of the program does not know are kept and written back
	/// unchanged, so a file written by a newer version is not damaged by an older one (it is refused when its
	/// format_version is newer). Owned by a <see cref="Project"/>; created by <see cref="ProjectStore"/>.
	/// Reading uses <see cref="JavaScriptSerializer"/>; writing is done by hand so the file stays
	/// stable, indented and human-readable. Values are stored as the serializer produces them
	/// (string, int, long, decimal, double, bool, nested dictionaries and object arrays), so the
	/// typed getters convert with the invariant culture.
	/// </summary>
	public sealed class ProjectConfig
	{
		/// <summary>Creates an empty configuration for a new project, stamping the current format version and the UTC created/modified time.</summary>
		public ProjectConfig(string projectName)
		{
			string now = ProjectConfig.Timestamp(DateTime.UtcNow);
			_values = new Dictionary<string, object>();
			_values[KeyFormatVersion] = CurrentFormatVersion;
			_values[KeyProjectName] = projectName;
			_values[KeyCreated] = now;
			_values[KeyModified] = now;
		}

		/// <summary>Wraps a deserialised dictionary (used by <see cref="Load"/>); adds the format version if the file had none.</summary>
		private ProjectConfig(Dictionary<string, object> values)
		{
			_values = values;
			if (!_values.ContainsKey(KeyFormatVersion))
			{
				_values[KeyFormatVersion] = CurrentFormatVersion;
			}
		}

		/// <summary>Format version stored in the file; <see cref="CurrentFormatVersion"/> when missing or unreadable.</summary>
		public int FormatVersion => GetInteger(KeyFormatVersion, CurrentFormatVersion);

		/// <summary>Gets or sets the project name ("" when missing). <see cref="Project.Save"/> overwrites it with the folder name before saving.</summary>
		public string ProjectName
		{
			get => GetString(KeyProjectName, "");
			set => _values[KeyProjectName] = value;
		}

		/// <summary>UTC creation timestamp as "yyyy-MM-ddTHH:mm:ssZ", or "" when missing.</summary>
		public string Created => GetString(KeyCreated, "");

		/// <summary>UTC timestamp of the last <see cref="Save"/> as "yyyy-MM-ddTHH:mm:ssZ", or "" when missing.</summary>
		public string Modified => GetString(KeyModified, "");

		/// <summary>Gets or sets the free-text project description (Form1's <c>btnDescription</c> / <see cref="frmDescription"/>); "" when none was set.</summary>
		public string Description
		{
			get => GetString(KeyDescription, "");
			set => _values[KeyDescription] = value;
		}

		/// <summary>True when a top-level value or section named <paramref name="key"/> exists (even if its value is null).</summary>
		public bool ContainsKey(string key)
		{
			return _values.ContainsKey(key);
		}

		/// <summary>Top-level value <paramref name="key"/> converted to a string with the invariant culture, or <paramref name="fallback"/> when missing or null.</summary>
		public string GetString(string key, string fallback)
		{
			object value;
			if (_values.TryGetValue(key, out value) && value != null)
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture);
			}
			return fallback;
		}

		/// <summary>Top-level value <paramref name="key"/> converted to an int with the invariant culture, or <paramref name="fallback"/> when missing, null, not numeric or out of range.</summary>
		public int GetInteger(string key, int fallback)
		{
			object value;
			if (_values.TryGetValue(key, out value) && value != null)
			{
				try
				{
					return Convert.ToInt32(value, CultureInfo.InvariantCulture);
				}
				catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
				{
					return fallback;
				}
			}
			return fallback;
		}

		/// <summary>Sets (or replaces) the top-level value <paramref name="key"/>; a dictionary value becomes a section. Allowed values: string, bool, numbers, null, a <c>Dictionary&lt;string, object&gt;</c>, or a list or array of these.</summary>
		public void SetValue(string key, object value)
		{
			_values[key] = value;
		}

		/// <summary>
		/// Returns the named sub-dictionary, creating an empty one (and storing it) if it is missing
		/// or the existing value is not a dictionary. Callers edit the returned dictionary in place.
		/// </summary>
		public Dictionary<string, object> GetSection(string name)
		{
			object existing;
			if (_values.TryGetValue(name, out existing))
			{
				Dictionary<string, object> section = existing as Dictionary<string, object>;
				if (section != null)
				{
					return section;
				}
			}
			Dictionary<string, object> created = new Dictionary<string, object>();
			_values[name] = created;
			return created;
		}

		/// <summary>Reads and parses a configuration.json (UTF-8, any size).</summary>
		/// <exception cref="InvalidDataException">The file is not valid JSON, its root is not an object, or its format version is newer than <see cref="CurrentFormatVersion"/>.</exception>
		public static ProjectConfig Load(string filePath)
		{
			string json = File.ReadAllText(filePath, Encoding.UTF8);
			JavaScriptSerializer serializer = new JavaScriptSerializer();
			serializer.MaxJsonLength = int.MaxValue;
			Dictionary<string, object> values = null;
			try
			{
				values = serializer.DeserializeObject(json) as Dictionary<string, object>;
			}
			catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
			{
				throw new InvalidDataException("The file " + Path.GetFileName(filePath) + " is not valid JSON: " + ex.Message, ex);
			}
			if (values == null)
			{
				throw new InvalidDataException("The file " + Path.GetFileName(filePath) + " is not a project configuration (a JSON object was expected).");
			}
			ProjectConfig config = new ProjectConfig(values);
			if (config.FormatVersion > CurrentFormatVersion)
			{
				throw new InvalidDataException("The file " + Path.GetFileName(filePath) + " was made by a newer version of the program (format " + config.FormatVersion.ToString(CultureInfo.InvariantCulture) + ").");
			}
			return config;
		}

		/// <summary>
		/// Writes the configuration to <paramref name="filePath"/> as indented JSON (UTF-8 without BOM),
		/// first stamping the current format version and the UTC "modified" time. The text goes to
		/// <c>filePath + ".tmp"</c> and is then moved over the old file, so a crash mid-write cannot
		/// leave a truncated configuration. Missing parent folders are created.
		/// </summary>
		public void Save(string filePath)
		{
			_values[KeyFormatVersion] = CurrentFormatVersion;
			_values[KeyModified] = ProjectConfig.Timestamp(DateTime.UtcNow);
			StringBuilder sb = new StringBuilder();
			ProjectConfig.WriteObject(sb, _values, 0);
			sb.AppendLine();
			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			// write to a temporary file first so a failed save never leaves a half-written configuration behind
			string tempPath = filePath + ".tmp";
			File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
			File.Move(tempPath, filePath);
		}

		/// <summary>Formats a UTC time as "yyyy-MM-ddTHH:mm:ssZ" (the value is not converted; pass UTC).</summary>
		private static string Timestamp(DateTime value)
		{
			return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Appends one JSON value. Dictionaries and non-string enumerables recurse into
		/// <see cref="WriteObject"/> / <see cref="WriteArray"/>; numbers use the invariant culture
		/// (doubles with round-trip "R" format, NaN/infinity become <c>null</c>); DateTime is written
		/// as a UTC timestamp string; other objects are written as their quoted string form; unknown
		/// type codes become <c>null</c>. <paramref name="level"/> is the nesting depth, used only to
		/// indent nested containers.
		/// </summary>
		private static void WriteValue(StringBuilder sb, object value, int level)
		{
			if (value == null)
			{
				sb.Append("null");
				return;
			}
			switch (Type.GetTypeCode(value.GetType()))
			{
			case TypeCode.Object:
			{
				IDictionary dict = value as IDictionary;
				IEnumerable list = value as IEnumerable;
				if (dict != null)
				{
					ProjectConfig.WriteObject(sb, dict, level);
				}
				else if (list != null)
				{
					ProjectConfig.WriteArray(sb, list, level);
				}
				else
				{
					sb.Append(ProjectConfig.Quote(Convert.ToString(value, CultureInfo.InvariantCulture)));
				}
				break;
			}
			case TypeCode.Boolean:
				sb.Append(Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "true" : "false");
				break;
			case TypeCode.Char:
			case TypeCode.String:
				sb.Append(ProjectConfig.Quote(Convert.ToString(value, CultureInfo.InvariantCulture)));
				break;
			case TypeCode.SByte:
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.UInt32:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Decimal:
				sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
				break;
			case TypeCode.Single:
			case TypeCode.Double:
			{
				double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
				if (double.IsNaN(number) || double.IsInfinity(number))
				{
					sb.Append("null");
				}
				else
				{
					sb.Append(number.ToString("R", CultureInfo.InvariantCulture));
				}
				break;
			}
			case TypeCode.DateTime:
				sb.Append(ProjectConfig.Quote(ProjectConfig.Timestamp(Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToUniversalTime())));
				break;
			default:
				sb.Append("null");
				break;
			}
		}

		/// <summary>Appends a dictionary as a JSON object, one <c>"key": value</c> pair per line indented by <paramref name="level"/> + 1; an empty dictionary becomes <c>{}</c>. Keys keep the dictionary's enumeration order.</summary>
		private static void WriteObject(StringBuilder sb, IDictionary dict, int level)
		{
			if (dict.Count == 0)
			{
				sb.Append("{}");
				return;
			}
			sb.AppendLine("{");
			int written = 0;
			foreach (DictionaryEntry entry in dict)
			{
				written++;
				sb.Append(ProjectConfig.Indent(level + 1));
				sb.Append(ProjectConfig.Quote(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)));
				sb.Append(": ");
				ProjectConfig.WriteValue(sb, entry.Value, level + 1);
				if (written < dict.Count)
				{
					sb.Append(",");
				}
				sb.AppendLine();
			}
			sb.Append(ProjectConfig.Indent(level));
			sb.Append("}");
		}

		/// <summary>
		/// Appends an enumerable as a JSON array. Arrays of scalars only are written on one line
		/// (<c>[1, 2, 3]</c>); arrays containing a dictionary or a nested non-string enumerable put
		/// one element per line. Empty arrays become <c>[]</c>.
		/// </summary>
		private static void WriteArray(StringBuilder sb, IEnumerable list, int level)
		{
			List<object> items = new List<object>();
			bool allScalar = true;
			foreach (object item in list)
			{
				items.Add(item);
				if (item != null && (item is IDictionary || (item is IEnumerable && !(item is string))))
				{
					allScalar = false;
				}
			}
			if (items.Count == 0)
			{
				sb.Append("[]");
			}
			else if (allScalar)
			{
				sb.Append("[");
				for (int i = 0; i < items.Count; i++)
				{
					if (i > 0)
					{
						sb.Append(", ");
					}
					ProjectConfig.WriteValue(sb, items[i], level + 1);
				}
				sb.Append("]");
			}
			else
			{
				sb.AppendLine("[");
				for (int i = 0; i < items.Count; i++)
				{
					sb.Append(ProjectConfig.Indent(level + 1));
					ProjectConfig.WriteValue(sb, items[i], level + 1);
					if (i < items.Count - 1)
					{
						sb.Append(",");
					}
					sb.AppendLine();
				}
				sb.Append(ProjectConfig.Indent(level));
				sb.Append("]");
			}
		}

		/// <summary>Two spaces of indentation per nesting level.</summary>
		private static string Indent(int level)
		{
			return new string(' ', level * 2);
		}

		/// <summary>
		/// Returns <paramref name="text"/> as a double-quoted JSON string literal, escaping tab, LF,
		/// CR, quote and backslash with their short forms and any other control character as
		/// <c>\uXXXX</c>. Non-ASCII characters are written unescaped (the file is UTF-8). Null
		/// becomes <c>""</c>.
		/// </summary>
		private static string Quote(string text)
		{
			if (text == null)
			{
				return "\"\"";
			}
			StringBuilder sb = new StringBuilder();
			sb.Append('"');
			foreach (char c in text)
			{
				switch (c)
				{
				case '\t':
					sb.Append("\\t");
					break;
				case '\n':
					sb.Append("\\n");
					break;
				case '\r':
					sb.Append("\\r");
					break;
				case '"':
					sb.Append("\\\"");
					break;
				case '\\':
					sb.Append("\\\\");
					break;
				default:
					if (c < ' ')
					{
						sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						sb.Append(c);
					}
					break;
				}
			}
			sb.Append('"');
			return sb.ToString();
		}

		/// <summary>File name of the configuration inside a project folder.</summary>
		public const string FileName = "configuration.json";

		/// <summary>File name of the pre-JSON configuration; an empty one marks a legacy project folder that <see cref="ProjectStore.Open"/> upgrades.</summary>
		public const string LegacyFileName = "Configuration.txt";

		/// <summary>Version of the file layout that this build writes; files with a higher version are refused by <see cref="Load"/>. Increase it when a change makes old files incompatible.</summary>
		public const int CurrentFormatVersion = 1;

		/// <summary>JSON key of the format version (int).</summary>
		private const string KeyFormatVersion = "format_version";

		/// <summary>JSON key of the project name (string).</summary>
		private const string KeyProjectName = "project_name";

		/// <summary>JSON key of the creation timestamp (string, UTC).</summary>
		private const string KeyCreated = "created";

		/// <summary>JSON key of the last-saved timestamp (string, UTC).</summary>
		private const string KeyModified = "modified";

		/// <summary>JSON key of the description (string).</summary>
		private const string KeyDescription = "description";

		/// <summary>The top-level JSON object: scalar values and section dictionaries keyed by name. Never null; shared by reference with the sections handed out by <see cref="GetSection"/>. Insertion ordered in practice, so the file keeps a stable, readable order.</summary>
		private readonly Dictionary<string, object> _values;
	}
}

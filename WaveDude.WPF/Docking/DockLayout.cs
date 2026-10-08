using System.IO;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace WaveDude.WPF.Docking;

/// <summary>
/// Serializable snapshot of a <see cref="DockHost"/> layout tree. Items are referenced by
/// <see cref="DockItem.Id"/>; unknown ids are ignored on load. Persisted with
/// <see cref="XmlSerializer"/>.
/// </summary>
[XmlRoot("DockLayout")]
public sealed class DockLayout
{
	private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	private static readonly XmlSerializer Serializer = new(typeof(DockLayout));

	/// <summary>Root of the tree: a split, a group, or the document node.</summary>
	[XmlElement("Split", typeof(SplitLayout))]
	[XmlElement("Group", typeof(GroupLayout))]
	[XmlElement("Document", typeof(DocumentLayout))]
	public NodeLayout? Root { get; set; }

	/// <summary>Writes the layout as indented XML.</summary>
	public void Save(Stream stream)
	{
		using var writer = XmlWriter.Create(stream, new XmlWriterSettings
		{
			Indent = true,
			IndentChars = "\t",
			Encoding = new System.Text.UTF8Encoding(false),
		});
		Serializer.Serialize(writer, this, new XmlSerializerNamespaces([XmlQualifiedName.Empty]));
	}

	/// <summary>Reads a layout written by <see cref="Save"/>. Returns null on malformed input.</summary>
	public static DockLayout? Load(Stream stream)
	{
		try
		{
			using var reader = XmlReader.Create(stream);
			return Serializer.Deserialize(reader) as DockLayout;
		}
		catch (InvalidOperationException ex)
		{
			Logger.Warn(ex, "The layout file does not hold a dock layout");
			return null;
		}
		catch (XmlException ex)
		{
			Logger.Warn(ex, "The layout file is not well-formed XML");
			return null;
		}
	}

	public abstract class NodeLayout
	{
		[XmlAttribute]
		public double Length { get; set; } = 1.0;
	}

	public sealed class SplitLayout : NodeLayout
	{
		[XmlAttribute]
		public Orientation Orientation { get; set; }

		[XmlElement("Split", typeof(SplitLayout))]
		[XmlElement("Group", typeof(GroupLayout))]
		[XmlElement("Document", typeof(DocumentLayout))]
		public List<NodeLayout> Children { get; set; } = new();
	}

	public sealed class GroupLayout : NodeLayout
	{
		[XmlAttribute]
		public string? Active { get; set; }

		[XmlElement("Item")]
		public List<string> Items { get; set; } = new();
	}

	public sealed class DocumentLayout : NodeLayout
	{
	}
}

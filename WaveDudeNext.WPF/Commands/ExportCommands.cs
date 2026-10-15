using System.Windows.Input;

namespace WaveDudeNext.WPF.Commands;

/// <summary>
/// The export commands, bound on the main window (handlers in <c>MainWindow.Export.cs</c>) and offered in
/// File > Export and on the pictures' context menus. Each opens the Save dialog for the format and name,
/// then renders the export range of the current run (started first when there is none or it is stale)
/// with the display settings as they are; the frame commands write the frame the preview shows, from the
/// same run, as one PNG or JPEG.
/// </summary>
public static class ExportCommands
{
	/// <summary>Exports the field pictures.</summary>
	public static readonly RoutedUICommand Field = new("Export field", nameof(Field), typeof(ExportCommands));

	/// <summary>Exports the intensity pictures.</summary>
	public static readonly RoutedUICommand Intensity = new("Export intensity", nameof(Intensity), typeof(ExportCommands));

	/// <summary>Exports both, as two outputs named <c>_field</c> and <c>_intensity</c> after the chosen name.</summary>
	public static readonly RoutedUICommand Both = new("Export both", nameof(Both), typeof(ExportCommands));

	/// <summary>Exports the field frame the preview shows as one image.</summary>
	public static readonly RoutedUICommand FieldFrame = new("Export field frame", nameof(FieldFrame), typeof(ExportCommands));

	/// <summary>Exports the intensity frame the preview shows as one image.</summary>
	public static readonly RoutedUICommand IntensityFrame = new("Export intensity frame", nameof(IntensityFrame), typeof(ExportCommands));
}

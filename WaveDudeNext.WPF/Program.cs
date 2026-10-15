namespace WaveDudeNext.WPF;

/// <summary>
/// Process entry point. Exists so that logging is configured before the generated
/// <see cref="App.Main"/> parses <c>App.xaml</c>; a failure in there is then still on record.
/// </summary>
internal static class Program
{
	[STAThread]
	private static void Main()
	{
		App.SetupLoggingConfig();
		App.Main();
	}
}

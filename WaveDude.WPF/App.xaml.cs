using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using NLog;
using NLog.Config;
using NLog.Targets;
using WaveDude.WPF.Logging;
using WaveDude.WPF.Properties;
using WaveDude.Runs;
using WaveDude.WPF.Rendering;
using WaveDude.WPF.Theming;

namespace WaveDude.WPF;

/// <summary>
/// Application entry point. Logging is configured by <see cref="SetupLoggingConfig"/> from
/// <c>Program.Main</c>; <see cref="OnStartup"/> records the environment and applies the theme
/// before the main window is created.
/// </summary>
public partial class App : Application
{
	/// <summary>Logger of the application class; other classes get their own via <see cref="LogManager.GetCurrentClassLogger"/>.</summary>
	public static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

	private const string FileLayout =
		"${date:format=yyyy-MM-dd HH\\:mm\\:ss}\t${threadid}\t${level}\t${logger}\t${message}\t${onexception:EXCEPTION\\: ${exception:format=tostring}}";
	private const string DebugLayout =
		"${date:format=HH\\:mm\\:ss} ${threadid} ${level} ${logger:shortName=true}\t${message}\t${onexception:EXCEPTION\\: ${exception:format=tostring}}";

	private const string LogFileName = "WaveDude.WPF.log";

	/// <summary>
	/// Per-user, per-machine data folder (<c>%LocalAppData%\WaveDude</c>, deliberately not the
	/// roaming profile), used for the log when the executable's folder is read-only.
	/// </summary>
	public static string LocalAppDataDirectory { get; } =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WaveDude");

	/// <summary>
	/// Path of the current log file. Next to the executable when that folder is writable,
	/// otherwise in <see cref="LocalAppDataDirectory"/>. Set by <see cref="SetupLoggingConfig"/>.
	/// </summary>
	public static string LogFile { get; private set; } = Path.Combine(AppContext.BaseDirectory, LogFileName);

	/// <summary>
	/// Replaces the NLog configuration with the application's targets: a rolling file next to
	/// the executable (everything in Debug builds, Info and up in Release), plus the debugger
	/// and console in Debug builds. Also routes unhandled exceptions of the AppDomain and of
	/// unobserved tasks to the log. Call once, before the application object exists.
	/// </summary>
	public static void SetupLoggingConfig()
	{
		LogFile = Path.Combine(ResolveLogDirectory(), LogFileName);
		var config = new LoggingConfiguration();

		var fileTarget = new FileTarget("logfile")
		{
			FileName = LogFile,
			ArchiveFileName = Path.ChangeExtension(LogFile, "archive.log"), // NLog 6 appends _00, _01 itself
			MaxArchiveFiles = 2,
			ArchiveAboveSize = 8 * 1024 * 1024,
			ArchiveOldFileOnStartup = true,
			KeepFileOpen = true,
			Layout = FileLayout,
		};
		config.AddTarget(fileTarget);

		// status bar: Info and above, warnings on orange, errors on red; attaches to the window once it exists
		var statusBar = new StatusBarTarget
		{
			Name = "statusbar",
			WindowName = nameof(MainWindow),
			ControlName = "txtStatus",
			Layout = "${message}${onexception: (${exception:format=message})}",
		};
		statusBar.ColoringRules.Add(new StatusBarColoringRule("level >= LogLevel.Error", "StatusBarErrorForeground", "StatusBarErrorBackground"));
		statusBar.ColoringRules.Add(new StatusBarColoringRule("level == LogLevel.Warn", "StatusBarWarnForeground", "StatusBarWarnBackground"));
		config.AddTarget(statusBar);
		config.AddRule(LogLevel.Info, LogLevel.Fatal, statusBar);

		// in-memory buffer behind the Messages pane; everything in Debug, Info and up in Release
		var messages = new MessagesTarget { Name = "messages" };
		config.AddTarget(messages);
#if DEBUG
		config.AddRule(LogLevel.Trace, LogLevel.Fatal, messages);
#else
		config.AddRule(LogLevel.Info, LogLevel.Fatal, messages);
#endif
#if DEBUG
		config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
		config.AddRule(LogLevel.Trace, LogLevel.Fatal, new DebuggerTarget("debugger") { Layout = DebugLayout });
		config.AddRule(LogLevel.Trace, LogLevel.Fatal, new ColoredConsoleTarget("console") { Layout = DebugLayout });
#else
		config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
#endif
		LogManager.Configuration = config;

#if DEBUG
		// WPF binding errors go to a trace source, not to exceptions; route them into the log
		PresentationTraceSources.Refresh();
		PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
		PresentationTraceSources.DataBindingSource.Listeners.Add(new TraceLogListener { Name = "WPF" });
#endif

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			Logger.Fatal(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
		TaskScheduler.UnobservedTaskException += (_, args) =>
			Logger.Fatal(args.Exception, "TaskScheduler.UnobservedTaskException");
	}

	/// <summary>The executable's folder if a file can be created there, else <see cref="LocalAppDataDirectory"/>.</summary>
	private static string ResolveLogDirectory()
	{
		if (IsWritable(AppContext.BaseDirectory))
		{
			return AppContext.BaseDirectory;
		}
		Directory.CreateDirectory(LocalAppDataDirectory);
		return LocalAppDataDirectory;
	}

	private static bool IsWritable(string directory)
	{
		try
		{
			string probe = Path.Combine(directory, Path.GetRandomFileName());
			using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
			{
			}
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	/// <summary>
	/// Makes the user settings usable: carries them over from the previous version on the
	/// first start after an update, and resets them when <c>user.config</c> is corrupt.
	/// </summary>
	private static void PrepareSettings()
	{
		try
		{
			if (Settings.Default.UpgradeRequired)
			{
				Settings.Default.Upgrade();
				Settings.Default.UpgradeRequired = false;
				Settings.Default.Save();
				Logger.Info("User settings upgraded from the previous version");
			}
		}
		catch (ConfigurationErrorsException ex)
		{
			string? file = (ex.InnerException as ConfigurationErrorsException)?.Filename ?? ex.Filename;
			Logger.Error(ex, "User settings file '{Path}' is unreadable; resetting to defaults", file);
			if (file != null && File.Exists(file))
			{
				File.Delete(file);
			}
			Settings.Default.Reload();
		}
	}

	/// <inheritdoc/>
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// WPF formats and parses bound numbers with the element's Language, which defaults to en-US
		// regardless of the Windows locale; make every element follow the user's culture instead
		// (a combo listing 0.25 next to a text box showing 1,5 otherwise). Must run before any window.
		FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
			new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

		DispatcherUnhandledException += (_, args) =>
		{
			Logger.Fatal(args.Exception, "Application.DispatcherUnhandledException");
			args.Handled = false; // let it crash so the issue is visible
		};

		Logger.Info("Application: '{Path}'", Environment.ProcessPath);
		Logger.Info("Application Version: {Version}", Assembly.GetExecutingAssembly().GetName().Version);
		Logger.Info("OS: '{OS}'", Environment.OSVersion.VersionString);
		Logger.Info("Runtime: '{Runtime}'", RuntimeInformation.FrameworkDescription);
		Logger.Info("Log file: '{Path}'", LogFile);

		// double-clicking a project file in Explorer then starts this exe with the path as e.Args[0]
		if (Environment.ProcessPath is string executable)
		{
			FileAssociation.Register(WPF.MainWindow.ProjectExtension, executable);
		}

		PrepareSettings();
		FrameCache.Sweep();
		ColorSchemeCatalog.Instance.Load();

		ThemeController.Instance.Current = Settings.Default.Theme;
		ThemeController.Instance.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(ThemeController.Current))
			{
				Settings.Default.Theme = ThemeController.Instance.Current;
			}
		};
		ThemeController.Instance.Refresh();

		var window = new MainWindow();
		MainWindow = window;
		window.Show();
		// the last start-up line, and so the status bar's first text unless a file is opened below
		Logger.Info("{Title} {Version} ready", FindResource("Title"), Assembly.GetExecutingAssembly().GetName().Version?.ToString(3));
		if (e.Args.Length > 0)
		{
			// launched with a project file (double-click, "open with", command line); after
			// Show so that an error box has the main window behind it
			Logger.Info("Startup arguments: {Args}", string.Join(" ", e.Args));
			window.OpenFile(e.Args[0]);
		}
	}

	/// <inheritdoc/>
	protected override void OnExit(ExitEventArgs e)
	{
		Logger.Info("Application exit, code {Code}", e.ApplicationExitCode);
		base.OnExit(e);
		LogManager.Shutdown();
	}
}

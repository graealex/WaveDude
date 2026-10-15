using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using NLog;
using WaveDudeNext.WPF.Commands;

namespace WaveDudeNext.WPF.Theming;

/// <summary>
/// Application theme state. Switches between the colour tables in <c>Themes/Dark.xaml</c> and
/// <c>Themes/Light.xaml</c> and keeps native window title bars in step. Bind menus to
/// <see cref="Options"/>, <see cref="Current"/> and <see cref="SelectCommand"/>.
/// </summary>
/// <remarks>
/// The colour table is the first merged dictionary of the application resources; every style in
/// <c>Themes/Controls.xaml</c> reads its brushes from it through <c>DynamicResource</c>, so
/// replacing that one dictionary restyles the whole application live.
/// </remarks>
public sealed class ThemeController : INotifyPropertyChanged
{
	private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
	private const int DwmwaUseImmersiveDarkMode = 20;
	private const string DarkSource = "/WaveDudeNext.WPF;component/Themes/Dark.xaml";
	private const string LightSource = "/WaveDudeNext.WPF;component/Themes/Light.xaml";

	private AppTheme _current = AppTheme.System;

	private ThemeController()
	{
		SelectCommand = new RelayCommand<AppTheme>(theme => Current = theme);
	}

	/// <summary>The single theme state of the application.</summary>
	public static ThemeController Instance { get; } = new();

	/// <summary>All selectable themes, in menu order.</summary>
	public IReadOnlyList<AppTheme> Options { get; } = Enum.GetValues<AppTheme>();

	/// <summary>Sets <see cref="Current"/> to its parameter.</summary>
	public ICommand SelectCommand { get; }

	/// <summary>The theme selected by the user. Setting it restyles the application.</summary>
	public AppTheme Current
	{
		get => _current;
		set
		{
			if (_current == value)
			{
				return;
			}
			_current = value;
			Logger.Info("Theme changed to {Theme} (dark: {IsDark})", value, IsDark);
			ApplyResources();
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsDark));
		}
	}

	/// <summary>True when the effective theme is dark, resolving <see cref="AppTheme.System"/>.</summary>
	public bool IsDark => _current switch
	{
		AppTheme.Dark => true,
		AppTheme.Light => false,
		_ => !SystemUsesLightTheme(),
	};

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Re-applies the resources for <see cref="Current"/>; call once at startup.</summary>
	public void Refresh()
	{
		Logger.Debug("Theme: {Theme} (dark: {IsDark})", _current, IsDark);
		ApplyResources();
	}

	/// <summary>
	/// Makes the native title bar of <paramref name="window"/> follow the effective theme.
	/// Call from <see cref="Window.SourceInitialized"/>; a no-op until the window has a handle.
	/// </summary>
	public void ApplyTitleBar(Window window)
	{
		IntPtr hwnd = new WindowInteropHelper(window).Handle;
		if (hwnd == IntPtr.Zero)
		{
			return;
		}
		int dark = IsDark ? 1 : 0;
		_ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
	}

	private void ApplyResources()
	{
		var table = new ResourceDictionary { Source = new Uri(IsDark ? DarkSource : LightSource, UriKind.Relative) };
		var merged = Application.Current.Resources.MergedDictionaries;
		int index = -1;
		for (int i = 0; i < merged.Count; i++)
		{
			string? source = merged[i].Source?.OriginalString;
			if (source != null && (source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase)
				|| source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase)))
			{
				index = i;
				break;
			}
		}
		if (index >= 0)
		{
			merged[index] = table;
		}
		else
		{
			merged.Insert(0, table);
		}
		foreach (Window window in Application.Current.Windows)
		{
			ApplyTitleBar(window);
		}
	}

	private static bool SystemUsesLightTheme()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
		return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

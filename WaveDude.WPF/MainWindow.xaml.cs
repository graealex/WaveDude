using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Win32;
using NLog;
using WaveDude.Model;
using WaveDude.WPF.Docking;
using WaveDude.WPF.Properties;
using WaveDude.WPF.Theming;

namespace WaveDude.WPF;

/// <summary>
/// Shell window: menu, status bar and the <see cref="DockHost"/> with the Project and
/// Properties panes around the preview. The window is its own data context: it owns the open
/// <see cref="Project"/>, its <see cref="Filename"/>, the tree's <see cref="SelectedItem"/> and the
/// current <see cref="Run"/> (see <c>MainWindow.Simulation.cs</c>). Window placement is bound two-way
/// to <see cref="Settings"/> in the XAML; the pane arrangement is stored there on closing.
/// </summary>
public partial class MainWindow : Window
{
	/// <summary>Extension of project files written by Save.</summary>
	public const string ProjectExtension = ".wdproj";
	private const int RecentFileCount = 10;

	private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

	/// <summary>Identifies the <see cref="Project"/> property.</summary>
	public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register(
		nameof(Project), typeof(Project), typeof(MainWindow), new PropertyMetadata(null, (d, e) => ((MainWindow)d).OnProjectReplaced((Project?)e.NewValue)));

	/// <summary>Identifies the <see cref="Filename"/> property.</summary>
	public static readonly DependencyProperty FilenameProperty = DependencyProperty.Register(
		nameof(Filename), typeof(string), typeof(MainWindow),
		new PropertyMetadata(null, (_, _) => CommandManager.InvalidateRequerySuggested()));

	/// <summary>Identifies the <see cref="SelectedItem"/> property.</summary>
	public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
		nameof(SelectedItem), typeof(object), typeof(MainWindow),
		new PropertyMetadata(null, (d, e) => ((MainWindow)d).OnSelectedItemChanged(e.NewValue)));

	public MainWindow()
	{
		Project = new Project();
		InitializeComponent();

		if (!TryLoadLayout() && !dockHost.LoadLayout(DefaultLayout()))
		{
			// Messages stays hidden until asked for (View > Panes, status bar double-click)
			dockHost.Show(projectDock);
			dockHost.Show(propertiesDock);
		}

		SourceInitialized += (_, _) => ThemeController.Instance.ApplyTitleBar(this);
		Closing += (_, _) =>
		{
			Settings.Default.Layout = dockHost.SaveLayout();
			ShutdownRun();
			ShutdownScratch();
		};
	}

	/// <summary>The open project. Never null: an empty project while nothing is loaded.</summary>
	public Project Project
	{
		get => (Project)GetValue(ProjectProperty);
		set => SetValue(ProjectProperty, value);
	}

	/// <summary>Full path the project was loaded from or saved to; null for an unsaved project.</summary>
	public string? Filename
	{
		get => (string?)GetValue(FilenameProperty);
		set => SetValue(FilenameProperty, value);
	}

	/// <summary>The model object selected in the project tree (or on the preview's pictures), or null.</summary>
	public object? SelectedItem
	{
		get => GetValue(SelectedItemProperty);
		set => SetValue(SelectedItemProperty, value);
	}

	/// <summary>A selection made elsewhere (a marker on the preview) is mirrored in the tree, which then raises its own change with the same item.</summary>
	private void OnSelectedItemChanged(object? item)
	{
		CommandManager.InvalidateRequerySuggested();
		if (item != null && !ReferenceEquals(projectTree.SelectedItem, item))
		{
			projectTree.Select(item);
		}
	}

	/// <summary>
	/// Loads the project file at <paramref name="fileName"/> and makes it the open project.
	/// Failures are logged and shown to the user; the previous project stays open then.
	/// </summary>
	internal void OpenFile(string fileName)
	{
		try
		{
			fileName = Path.GetFullPath(fileName);
			Project loaded = Project.Load(fileName);
			DiscardRun();
			Project = loaded;
			Filename = fileName;
			Logger.Info("Opened '{Path}'", fileName);
			RememberRecent(fileName);
			projectTree.SelectRoot();
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Error while opening '{Path}'", fileName);
			ShowError(string.Format((string)FindResource("OpenError"), fileName), ex);
		}
	}

	/// <summary>Writes the open project to <paramref name="fileName"/>; false when that failed.</summary>
	private bool SaveFile(string fileName)
	{
		try
		{
			fileName = Path.GetFullPath(fileName);
			Project.Save(fileName);
			Filename = fileName;
			Logger.Info("Saved '{Path}'", fileName);
			RememberRecent(fileName);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Error while saving '{Path}'", fileName);
			ShowError(string.Format((string)FindResource("SaveError"), fileName), ex);
			return false;
		}
	}

	/// <summary>Puts the file at the top of the recent list and of the taskbar jump list.</summary>
	private static void RememberRecent(string fileName)
	{
		JumpList.AddToRecentCategory(fileName);

		// a fresh collection so the bound menu sees the change (StringCollection does not notify)
		var recent = new StringCollection { fileName };
		foreach (string? entry in Settings.Default.RecentFiles ?? [])
		{
			if (entry != null && !string.Equals(entry, fileName, StringComparison.OrdinalIgnoreCase) && recent.Count < RecentFileCount)
			{
				recent.Add(entry);
			}
		}
		Settings.Default.RecentFiles = recent;
		SaveSettings();
	}

	private void ShowError(string message, Exception ex) =>
		MessageBox.Show(this, $"{message}\n{ex.Message}", (string)FindResource("Title"), MessageBoxButton.OK, MessageBoxImage.Error);

	private void OnFileNew(object sender, ExecutedRoutedEventArgs e)
	{
		DiscardRun();
		Project = new Project();
		Filename = null;
		projectTree.SelectRoot();
	}

	private void OnFileOpen(object sender, ExecutedRoutedEventArgs e)
	{
		if (e.Parameter is string recent)
		{
			OpenFile(recent);
			return;
		}
		var dialog = new OpenFileDialog
		{
			Filter = (string)FindResource("OpenFileDialogFilter"),
			DefaultExt = ProjectExtension,
		};
		if (dialog.ShowDialog(this) == true)
		{
			OpenFile(dialog.FileName);
		}
	}

	private void OnFileSave(object sender, ExecutedRoutedEventArgs e)
	{
		if (Filename == null)
		{
			OnFileSaveAs(sender, e);
		}
		else
		{
			SaveFile(Filename);
		}
	}

	private void OnFileSaveAs(object sender, ExecutedRoutedEventArgs e)
	{
		var dialog = new SaveFileDialog
		{
			Filter = (string)FindResource("SaveFileDialogFilter"),
			DefaultExt = ProjectExtension,
			AddExtension = true,
			FileName = Filename ?? "",
		};
		if (dialog.ShowDialog(this) == true)
		{
			SaveFile(dialog.FileName);
		}
	}

	private void CanFileClose(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Filename != null;

	private void OnFileClose(object sender, ExecutedRoutedEventArgs e)
	{
		DiscardRun();
		Project = new Project();
		Filename = null;
		projectTree.SelectRoot();
	}

	private void OnFileExit(object sender, ExecutedRoutedEventArgs e) => Close();

	/// <summary>Shows the Messages pane if hidden, brings it to the front of its group, and gives it keyboard focus.</summary>
	private void OnShowMessages(object sender, ExecutedRoutedEventArgs e)
	{
		dockHost.Show(messagesDock);
		messagesPane.FocusList();
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
		{
			OpenFile(files[0]);
		}
	}

	private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => SelectedItem = e.NewValue;

	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		if (Settings.Default.MainWindow_State == WindowState.Minimized)
		{
			// never start minimized
			Settings.Default.MainWindow_State = WindowState.Normal;
		}
		SaveSettings();
	}

	private static void SaveSettings()
	{
		try
		{
			Settings.Default.Save();
			Logger.Debug("User settings saved");
		}
		catch (ConfigurationErrorsException ex)
		{
			// Losing window placement or the recent list is not worth interrupting the user.
			Logger.Warn(ex, "Could not save the user settings");
		}
	}

	/// <summary>
	/// The arrangement of a first start (agreed 2026-10-09): a left column of a quarter of the width with the
	/// Project tree in its upper third and the Properties pane below it, the preview in the rest; Messages hidden.
	/// </summary>
	private DockLayout DefaultLayout() => new()
	{
		Root = new DockLayout.SplitLayout
		{
			Orientation = Orientation.Horizontal,
			Children =
			{
				new DockLayout.SplitLayout
				{
					Orientation = Orientation.Vertical,
					Length = 1,
					Children =
					{
						new DockLayout.GroupLayout { Items = { projectDock.Id }, Active = projectDock.Id, Length = 1 },
						new DockLayout.GroupLayout { Items = { propertiesDock.Id }, Active = propertiesDock.Id, Length = 2 },
					},
				},
				new DockLayout.DocumentLayout { Length = 3 },
			},
		},
	};

	private bool TryLoadLayout()
	{
		DockLayout? layout = Settings.Default.Layout;
		if (layout == null)
		{
			Logger.Debug("No saved layout, using the default arrangement");
			return false;
		}
		bool applied = dockHost.LoadLayout(layout);
		Logger.Debug("Saved layout {Result}", applied ? "restored" : "rejected, using the default arrangement");
		return applied;
	}
}

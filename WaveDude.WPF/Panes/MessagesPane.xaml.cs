using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using NLog;
using WaveDude.WPF.Logging;

namespace WaveDude.WPF.Panes;

/// <summary>
/// The Messages pane: the entries kept by a <see cref="MessagesTarget"/>, filtered by level
/// toggles and a text or regex search, coloured per level, with auto-scroll, clear and copy.
/// The target is found by <see cref="TargetName"/> in the NLog configuration when the pane
/// loads, so the pane shows what was logged before it existed.
/// </summary>
public partial class MessagesPane : UserControl, INotifyPropertyChanged
{
	/// <summary>Copies every visible line to the clipboard.</summary>
	public static readonly RoutedUICommand CopyAll = new("Copy all", nameof(CopyAll), typeof(MessagesPane));

	/// <summary>Drops every entry from the target.</summary>
	public static readonly RoutedUICommand Clear = new("Clear", nameof(Clear), typeof(MessagesPane));

	/// <summary>Identifies the <see cref="TargetName"/> property.</summary>
	public static readonly DependencyProperty TargetNameProperty = DependencyProperty.Register(
		nameof(TargetName), typeof(string), typeof(MessagesPane), new PropertyMetadata("messages"));

	private MessagesTarget? _target;
	private string _searchText = "";
	private bool _useRegex;
	private bool _autoScroll = true;
	private Regex? _regex;

	/// <summary>Trace and Debug start visible in Debug builds only; Info and above always do.</summary>
#if DEBUG
	private const bool ShowDiagnosticsByDefault = true;
#else
	private const bool ShowDiagnosticsByDefault = false;
#endif

	public MessagesPane()
	{
		Levels = new ObservableCollection<LevelFilter>(
			new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal }
				.Select(level => new LevelFilter(level, visible: ShowDiagnosticsByDefault || level >= LogLevel.Info)));
		foreach (LevelFilter level in Levels)
		{
			level.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(LevelFilter.IsVisible))
				{
					RefreshView();
				}
			};
		}
		InitializeComponent();
		Loaded += (_, _) => Attach();
		// the pane can be shown long after the target filled up: start at the newest line
		list.Loaded += (_, _) =>
		{
			if (AutoScroll)
			{
				ScrollToEnd();
			}
		};
	}

	/// <summary>Name of the <see cref="MessagesTarget"/> in the NLog configuration.</summary>
	public string TargetName
	{
		get => (string)GetValue(TargetNameProperty);
		set => SetValue(TargetNameProperty, value);
	}

	/// <summary>One filter per level, in severity order; bound to the toolbar toggles.</summary>
	public ObservableCollection<LevelFilter> Levels { get; }

	/// <summary>The filtered entries the list shows; null until attached.</summary>
	public ICollectionView? View { get; private set; }

	/// <summary>Text every shown entry must contain (message, logger or exception); a regex when <see cref="UseRegex"/>.</summary>
	public string SearchText
	{
		get => _searchText;
		set
		{
			if (Set(ref _searchText, value))
			{
				UpdateSearch();
			}
		}
	}

	/// <summary>Whether <see cref="SearchText"/> is a regular expression.</summary>
	public bool UseRegex
	{
		get => _useRegex;
		set
		{
			if (Set(ref _useRegex, value))
			{
				UpdateSearch();
			}
		}
	}

	/// <summary>Whether the list follows new entries.</summary>
	public bool AutoScroll
	{
		get => _autoScroll;
		set
		{
			if (Set(ref _autoScroll, value) && value)
			{
				ScrollToEnd();
			}
		}
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Puts keyboard focus on the list (its newest line when nothing is selected) once the pane is laid out.</summary>
	public void FocusList() =>
		Dispatcher.InvokeAsync(() =>
		{
			if (list.SelectedItem == null && View != null && View.Cast<object>().LastOrDefault() is { } last)
			{
				list.SelectedItem = last;
			}
			if (list.SelectedItem != null && list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is ListViewItem row)
			{
				row.Focus();
			}
			else
			{
				list.Focus();
			}
		}, System.Windows.Threading.DispatcherPriority.ContextIdle);

	private void Attach()
	{
		if (_target != null)
		{
			return;
		}
		_target = LogManager.Configuration?.FindTargetByName<MessagesTarget>(TargetName);
		if (_target == null)
		{
			return;
		}
		foreach (LogEntry entry in _target.Entries)
		{
			Count(entry, +1);
		}
		((INotifyCollectionChanged)_target.Entries).CollectionChanged += OnEntriesChanged;
		View = CollectionViewSource.GetDefaultView(_target.Entries);
		View.Filter = Passes;
		OnPropertyChanged(nameof(View));
		ScrollToEnd();
	}

	private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				foreach (LogEntry entry in e.NewItems!)
				{
					Count(entry, +1);
				}
				if (AutoScroll)
				{
					ScrollToEnd();
				}
				break;
			case NotifyCollectionChangedAction.Remove:
				foreach (LogEntry entry in e.OldItems!)
				{
					Count(entry, -1);
				}
				break;
			case NotifyCollectionChangedAction.Reset:
				foreach (LevelFilter level in Levels)
				{
					level.Count = 0;
				}
				break;
		}
	}

	private void Count(LogEntry entry, int delta)
	{
		LevelFilter? level = Levels.FirstOrDefault(l => l.Level == entry.Level);
		if (level != null)
		{
			level.Count += delta;
		}
	}

	private bool Passes(object item)
	{
		if (item is not LogEntry entry)
		{
			return false;
		}
		if (Levels.FirstOrDefault(l => l.Level == entry.Level) is { IsVisible: false })
		{
			return false;
		}
		if (_regex != null)
		{
			return _regex.IsMatch(entry.FullMessage) || _regex.IsMatch(entry.Logger);
		}
		return SearchText.Length == 0
			|| entry.FullMessage.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
			|| entry.Logger.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
	}

	private void UpdateSearch()
	{
		_regex = null;
		if (UseRegex && SearchText.Length > 0)
		{
			try
			{
				_regex = new Regex(SearchText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			}
			catch (ArgumentException)
			{
				// an unfinished pattern while typing: show everything until it parses
			}
		}
		RefreshView();
	}

	/// <summary>Re-applies the filter; a refresh resets the scroll position, so follow the end again when asked to.</summary>
	private void RefreshView()
	{
		View?.Refresh();
		if (AutoScroll)
		{
			ScrollToEnd();
		}
	}

	private void ScrollToEnd()
	{
		if (View == null)
		{
			return;
		}
		Dispatcher.InvokeAsync(() =>
		{
			object? last = View.Cast<object>().LastOrDefault();
			if (last != null)
			{
				list.ScrollIntoView(last);
			}
		}, System.Windows.Threading.DispatcherPriority.ContextIdle);
	}

	private void CanCopy(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = list.SelectedItems.Count > 0;

	private void OnCopy(object sender, ExecutedRoutedEventArgs e) =>
		CopyLines(list.SelectedItems.Cast<LogEntry>().OrderBy(entry => entry.Time));

	private void CanCopyAll(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = View != null && !View.IsEmpty;

	private void OnCopyAll(object sender, ExecutedRoutedEventArgs e)
	{
		if (View != null)
		{
			CopyLines(View.Cast<LogEntry>());
		}
	}

	private static void CopyLines(IEnumerable<LogEntry> entries)
	{
		var text = new StringBuilder();
		foreach (LogEntry entry in entries)
		{
			text.AppendLine(entry.ToLine());
		}
		Clipboard.SetText(text.ToString());
	}

	private void OnClear(object sender, ExecutedRoutedEventArgs e) => _target?.Clear();

	private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(name);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Visibility toggle and entry count of one log level in the Messages pane.</summary>
public sealed class LevelFilter : INotifyPropertyChanged
{
	private bool _isVisible;
	private int _count;

	/// <param name="level">The level this filter stands for.</param>
	/// <param name="visible">Initial state of the toggle.</param>
	public LevelFilter(LogLevel level, bool visible)
	{
		Level = level;
		_isVisible = visible;
	}

	/// <summary>The level this filter stands for.</summary>
	public LogLevel Level { get; }

	/// <summary>Level name for the toggle caption.</summary>
	public string Name => Level.Name;

	/// <summary>Whether entries of this level are shown.</summary>
	public bool IsVisible
	{
		get => _isVisible;
		set
		{
			if (_isVisible != value)
			{
				_isVisible = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>Number of kept entries of this level, shown or not.</summary>
	public int Count
	{
		get => _count;
		set
		{
			if (_count != value)
			{
				_count = value;
				OnPropertyChanged();
			}
		}
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

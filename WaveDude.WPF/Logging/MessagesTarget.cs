using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using NLog;
using NLog.Targets;

namespace WaveDude.WPF.Logging;

/// <summary>
/// NLog target that keeps the last <see cref="MaxCount"/> log events in memory for the
/// Messages pane. Events may arrive on any thread; they are queued and moved into
/// <see cref="Entries"/> on the UI thread in batches, so a burst of logging does not stall the
/// UI. Create it on the UI thread (it captures that dispatcher); the pane finds it by name.
/// </summary>
[Target("Messages")]
public sealed class MessagesTarget : Target
{
	private readonly ObservableCollection<LogEntry> _entries = new();
	private readonly ConcurrentQueue<LogEntry> _pending = new();
	private readonly DispatcherTimer _flush;

	public MessagesTarget()
	{
		Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);
		_flush = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
		{
			Interval = TimeSpan.FromMilliseconds(100),
		};
		_flush.Tick += (_, _) => Flush();
	}

	/// <summary>Upper bound of kept entries; the oldest are dropped beyond it.</summary>
	public int MaxCount { get; set; } = 5000;

	/// <summary>The kept entries, oldest first. UI thread only.</summary>
	public ReadOnlyObservableCollection<LogEntry> Entries { get; }

	/// <summary>Drops every kept entry. UI thread only.</summary>
	public void Clear()
	{
		while (_pending.TryDequeue(out _))
		{
		}
		_entries.Clear();
	}

	/// <inheritdoc/>
	protected override void Write(LogEventInfo logEvent)
	{
		_pending.Enqueue(new LogEntry(logEvent.TimeStamp, logEvent.Level, logEvent.LoggerName ?? "", logEvent.FormattedMessage, logEvent.Exception?.ToString()));
		if (!_flush.IsEnabled)
		{
			_flush.Start();
		}
	}

	/// <inheritdoc/>
	protected override void CloseTarget()
	{
		_flush.Stop();
		base.CloseTarget();
	}

	private void Flush()
	{
		_flush.Stop();
		while (_pending.TryDequeue(out LogEntry? entry))
		{
			_entries.Add(entry);
		}
		while (_entries.Count > MaxCount)
		{
			_entries.RemoveAt(0);
		}
	}
}

/// <summary>One line of the Messages pane.</summary>
/// <param name="Time">When the event was logged.</param>
/// <param name="Level">Severity.</param>
/// <param name="Logger">Full logger name, usually the class.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception with stack trace, or null.</param>
public sealed record LogEntry(DateTime Time, LogLevel Level, string Logger, string Message, string? Exception)
{
	/// <summary>Level name as shown in the pane ("Info", "Warn", ...).</summary>
	public string LevelName => Level.Name;

	/// <summary>Logger name without its namespace.</summary>
	public string LoggerShort => Logger[(Logger.LastIndexOf('.') + 1)..];

	/// <summary>Message plus the exception text, for display and copy.</summary>
	public string FullMessage => Exception == null ? Message : $"{Message}\n{Exception}";

	/// <summary>Tab-separated line for the clipboard.</summary>
	public string ToLine() => $"{Time:HH:mm:ss.fff}\t{LevelName}\t{Logger}\t{FullMessage}";
}

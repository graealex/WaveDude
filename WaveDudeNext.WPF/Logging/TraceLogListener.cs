using System.Diagnostics;
using NLog;

namespace WaveDudeNext.WPF.Logging;

/// <summary>
/// Forwards a <see cref="TraceSource"/> into NLog, under the source's name as logger. Used for
/// WPF's data-binding trace, which reports binding errors only this way.
/// </summary>
public sealed class TraceLogListener : TraceListener
{
	/// <inheritdoc/>
	public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
		LogManager.GetLogger(source).Log(ToLevel(eventType), message ?? "");

	/// <inheritdoc/>
	public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
		LogManager.GetLogger(source).Log(ToLevel(eventType), format ?? "", args ?? []);

	/// <inheritdoc/>
	public override void Write(string? message) => LogManager.GetLogger(Name).Debug(message ?? "");

	/// <inheritdoc/>
	public override void WriteLine(string? message) => LogManager.GetLogger(Name).Debug(message ?? "");

	private static LogLevel ToLevel(TraceEventType type) => type switch
	{
		TraceEventType.Critical => LogLevel.Fatal,
		TraceEventType.Error => LogLevel.Error,
		TraceEventType.Warning => LogLevel.Warn,
		TraceEventType.Information => LogLevel.Info,
		TraceEventType.Verbose => LogLevel.Debug,
		_ => LogLevel.Trace,
	};
}

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;
using WaveDude.Rendering;
using WaveDude.Runs;

namespace WaveDude.Export;

/// <summary>Where an export stands.</summary>
public enum ExportState
{
	/// <summary>Not started.</summary>
	Created,
	/// <summary>Frames are being written.</summary>
	Running,
	/// <summary>Every frame of the range was written.</summary>
	Completed,
	/// <summary>Stopped by <see cref="ExportJob.Cancel"/>; the files hold the frames written so far.</summary>
	Cancelled,
	/// <summary>Stopped by an error (<see cref="ExportJob.Error"/>).</summary>
	Failed,
}

/// <summary>
/// How one layer is rendered for an export: the colour table, gain, the scene outline to bake in, and an
/// optional decoration step (the probe graph boxes, drawn by the application). The decoration runs on the
/// render thread, once per frame, after the outline, on the frame's packed BGRA32 pixels.
/// </summary>
/// <param name="Layer">Which plane of the store.</param>
/// <param name="Lut">The colour table (<c>ColorLut.Argb</c>), or null for grey levels.</param>
/// <param name="Brightness">The layer's gain.</param>
/// <param name="Edges">The glowing-edges picture (<see cref="GlowingEdges.Compute"/>) to add, or null.</param>
/// <param name="EdgeGain">The outline's gain; 0 adds nothing.</param>
/// <param name="Decorate">Draws over the rendered frame: the frame index and the pixels; null for none.</param>
public sealed record ExportLayer(FrameLayer Layer, int[]? Lut, float Brightness, byte[]? Edges, double EdgeGain, Action<int, Memory<int>>? Decorate);

/// <summary>
/// One output of an export: a layer rendered into a sink. The sink is opened by the job on its sink thread
/// (<see cref="Open"/>), not by the caller: a Media Foundation writer must be created and used on one
/// thread, and the COM object refused a thread from another apartment (2026-10-14).
/// </summary>
/// <param name="Layer">How the frames are rendered.</param>
/// <param name="Output">What the output will be called in messages before the sink exists: the file or the sequence folder.</param>
/// <param name="Open">Creates the sink; called once on the sink thread, closed by the job.</param>
public sealed record ExportTarget(ExportLayer Layer, string Output, Func<IFrameSink> Open)
{
	/// <summary>The sink once the job has opened it; null before, and when opening failed.</summary>
	public IFrameSink? Sink { get; internal set; }
}

/// <summary>
/// Renders a range of a run's stored frames into one or more sinks: a render thread reads each target's
/// plane from the <see cref="FrameStore"/>, colours it with <see cref="FrameRenderer"/>, adds the outline
/// (<see cref="GlowingEdges.BlendAdditive"/>) and the decoration, and hands the pixels to a sink thread,
/// which opens the sinks, encodes and writes; the two overlap, so a frame's encoding costs no rendering time
/// (2026-10-14). Progress and the final state are published through the <see cref="SynchronizationContext"/>
/// given to the constructor (the WPF dispatcher; null runs them on the worker), so the properties bind
/// directly. Both threads are single-threaded apartments on Windows: the decoration may use WPF drawing
/// objects of its own, and Media Foundation stays on the thread that created its writer.
/// </summary>
public sealed class ExportJob : INotifyPropertyChanged
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	/// <summary>Least interval between progress posts.</summary>
	private const long ProgressIntervalMs = 100;

	/// <summary>Rendered frames waiting for the sinks, per target; the render thread blocks beyond that.</summary>
	private const int QueueDepth = 2;

	private readonly SynchronizationContext? _context;
	private readonly CancellationTokenSource _cancel = new();
	private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private ExportState _state = ExportState.Created;
	private int _framesDone;
	private TimeSpan _elapsed;
	private Exception? _error;

	/// <param name="store">The frames; read from <paramref name="firstFrame"/> to <paramref name="lastFrame"/>, which must exist by the time they are reached.</param>
	/// <param name="firstFrame">First frame index.</param>
	/// <param name="lastFrame">Last frame index, inclusive.</param>
	/// <param name="targets">The outputs; their sinks are opened when the job starts and closed when it ends, however it ends.</param>
	/// <param name="context">Where properties are published; null for the worker.</param>
	public ExportJob(FrameStore store, int firstFrame, int lastFrame, IReadOnlyList<ExportTarget> targets, SynchronizationContext? context)
	{
		if (firstFrame < 0 || lastFrame < firstFrame)
		{
			throw new ArgumentOutOfRangeException(nameof(lastFrame), "The range is empty.");
		}
		Store = store;
		FirstFrame = firstFrame;
		LastFrame = lastFrame;
		Targets = targets;
		_context = context;
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>The job reached a final state; raised once, after <see cref="State"/> changed.</summary>
	public event EventHandler? Finished;

	/// <summary>The frames being exported.</summary>
	public FrameStore Store { get; }

	/// <summary>First frame of the range.</summary>
	public int FirstFrame { get; }

	/// <summary>Last frame of the range, inclusive.</summary>
	public int LastFrame { get; }

	/// <summary>Frames in the range.</summary>
	public int FrameCount => LastFrame - FirstFrame + 1;

	/// <summary>The outputs.</summary>
	public IReadOnlyList<ExportTarget> Targets { get; }

	/// <summary>Current state.</summary>
	public ExportState State
	{
		get => _state;
		private set => Set(ref _state, value);
	}

	/// <summary>Whether the job is running.</summary>
	public bool IsActive => State == ExportState.Running;

	/// <summary>Frames written to every target so far; refreshed about ten times a second and at the end.</summary>
	public int FramesDone
	{
		get => _framesDone;
		private set => Set(ref _framesDone, value);
	}

	/// <summary>Time spent so far, set at the end.</summary>
	public TimeSpan Elapsed
	{
		get => _elapsed;
		private set => Set(ref _elapsed, value);
	}

	/// <summary>The error that ended the job, when <see cref="State"/> is Failed.</summary>
	public Exception? Error
	{
		get => _error;
		private set => Set(ref _error, value);
	}

	/// <summary>Completes when the job has reached a final state and every sink is closed.</summary>
	public Task Completion => _completion.Task;

	/// <summary>Starts the threads. Once.</summary>
	public void Start()
	{
		if (State != ExportState.Created)
		{
			throw new InvalidOperationException("The export has already been started.");
		}
		State = ExportState.Running;
		StartThread("Export render", Render);
	}

	/// <summary>Asks the threads to stop after the frame in progress; the sinks are closed with what they have.</summary>
	public void Cancel() => _cancel.Cancel();

	private static Thread StartThread(string name, ThreadStart work)
	{
		var thread = new Thread(work, 16 << 20) { Name = name, IsBackground = true };
		if (OperatingSystem.IsWindows())
		{
			thread.SetApartmentState(ApartmentState.STA);
		}
		thread.Start();
		return thread;
	}

	/// <summary>A rendered frame on its way to a sink.</summary>
	private sealed record Item(int Frame, int Target, int[] Pixels);

	/// <summary>The render thread: starts the sink thread, renders every frame of the range into pooled buffers and queues them; waits for the sink thread at the end.</summary>
	private void Render()
	{
		var stopwatch = Stopwatch.StartNew();
		ExportState final = ExportState.Completed;
		Exception? error = null;
		int width = Store.Width;
		int height = Store.Height;
		int buffers = Targets.Count * QueueDepth + 1;
		var free = new BlockingCollection<int[]>(buffers);
		for (int i = 0; i < buffers; i++)
		{
			free.Add(new int[width * height]);
		}
		var queue = new BlockingCollection<Item>(Targets.Count * QueueDepth);
		using var sinksReady = new ManualResetEventSlim();
		Exception? sinkError = null;
		Exception? closeError = null;
		Thread sink = StartThread("Export sinks", () =>
		{
			int written = 0;
			long lastPost = 0;
			try
			{
				foreach (ExportTarget target in Targets)
				{
					target.Sink = target.Open();
					Logger.Debug("Export output opened: {Output}", target.Sink.Output);
				}
				sinksReady.Set();
				foreach (Item item in queue.GetConsumingEnumerable())
				{
					Targets[item.Target].Sink!.Add(item.Pixels);
					free.Add(item.Pixels);
					if (item.Target == Targets.Count - 1)
					{
						written++;
						int done = written;
						if (stopwatch.ElapsedMilliseconds - lastPost >= ProgressIntervalMs)
						{
							lastPost = stopwatch.ElapsedMilliseconds;
							Post(() => FramesDone = done);
						}
					}
				}
			}
			catch (Exception ex)
			{
				sinkError = ex;
				_cancel.Cancel();
				sinksReady.Set();
				// drain, so the render thread never blocks on a full queue
				foreach (Item item in queue.GetConsumingEnumerable())
				{
					free.Add(item.Pixels);
				}
			}
			finally
			{
				// closed where they were opened: a Media Foundation writer is bound to its thread
				foreach (ExportTarget target in Targets)
				{
					if (target.Sink == null)
					{
						continue;
					}
					try
					{
						target.Sink.Close();
					}
					catch (Exception ex)
					{
						Logger.Error(ex, "Closing {Output} failed: {Message}", target.Sink.Output, ex.Message);
						closeError ??= ex;
					}
				}
			}
		});
		try
		{
			sinksReady.Wait();
			var values = new Half[width * height];
			for (int frame = FirstFrame; frame <= LastFrame && sinkError == null; frame++)
			{
				if (_cancel.IsCancellationRequested)
				{
					final = ExportState.Cancelled;
					break;
				}
				for (int t = 0; t < Targets.Count; t++)
				{
					ExportLayer layer = Targets[t].Layer;
					if (frame >= Store.Count || !Store.CopyLayer(frame, layer.Layer, values))
					{
						throw new InvalidOperationException($"Frame {frame} is not in the store (it holds {Store.Count}).");
					}
					int[] pixels = free.Take();
					if (layer.Layer == FrameLayer.Intensity)
					{
						FrameRenderer.RenderIntensity(values, width, height, layer.Lut, layer.Brightness, pixels);
					}
					else
					{
						FrameRenderer.RenderField(values, width, height, layer.Lut, layer.Brightness, pixels);
					}
					if (layer.Edges != null && layer.EdgeGain > 0.0)
					{
						GlowingEdges.BlendAdditive(pixels, width, height, layer.Edges, layer.EdgeGain);
					}
					layer.Decorate?.Invoke(frame, pixels);
					queue.Add(new Item(frame, t, pixels));
				}
			}
		}
		catch (Exception ex)
		{
			final = ExportState.Failed;
			error = ex;
			Logger.Error(ex, "Export failed: {Message}", ex.Message);
		}
		finally
		{
			queue.CompleteAdding();
			sink.Join();
			if (sinkError != null)
			{
				final = ExportState.Failed;
				error = sinkError;
				Logger.Error(sinkError, "Export failed: {Message}", sinkError.Message);
			}
			else if (closeError != null && final == ExportState.Completed)
			{
				final = ExportState.Failed;
				error = closeError;
			}
		}
		stopwatch.Stop();
		int framesDone = Targets.Count > 0 ? Targets[0].Sink?.FrameCount ?? 0 : FrameCount;
		Post(() =>
		{
			FramesDone = framesDone;
			Elapsed = stopwatch.Elapsed;
			Error = error;
			State = final;
			Finished?.Invoke(this, EventArgs.Empty);
			_completion.TrySetResult();
		});
	}

	private void Post(Action action)
	{
		if (_context == null)
		{
			action();
		}
		else
		{
			_context.Post(_ => action(), null);
		}
	}

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			if (name == nameof(State))
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
			}
		}
	}
}

using System.Diagnostics;
using NLog;
using WaveDudeNext.Engine;
using WaveDudeNext.Probes;

namespace WaveDudeNext.Runs;

/// <summary>The outcome of a scratch request: the frame and the probe recordings up to it, or why it was not computed.</summary>
/// <param name="Spec">The snapshot of the project the frame was computed for.</param>
/// <param name="Frame">The frame index requested.</param>
/// <param name="Snapshot">The frame, or null when it was skipped; owned by the runner and valid until the next outcome.</param>
/// <param name="Estimate">How long the runner expected (or took) to compute it.</param>
/// <param name="Traces">Per probe of <see cref="RunSpec.Probes"/>: the series up to the frame's step and, for a line probe, its one profile at the frame (index 0); empty when skipped or without probes. Owned by the result; read on the UI thread.</param>
public sealed record ScratchResult(RunSpec Spec, int Frame, FrameSnapshot? Snapshot, TimeSpan Estimate, ProbeTrace[] Traces);

/// <summary>
/// Computes single frames of the current project state on demand, without a store: after an edit the
/// preview asks for "frame k of the project as it is now", and the runner steps an engine from zero to that
/// frame and captures it, sampling the project's probes along the way so their graphs are as live as the
/// picture (2026-10-14; the sampling costs a kernel launch per step and a few kilobytes of readback, nothing
/// against the stepping). The engine is kept between requests (a GPU engine takes a few hundred
/// milliseconds to build) and only reset, or given new emitters when just the sources changed; it is rebuilt
/// when the scene or the solver options change. Only the newest request is served: a request arriving
/// while one is computing abandons it. A request whose predicted cost (steps × the measured cost per step)
/// exceeds <see cref="Budget"/> is skipped and reported, so a slow engine does not stall the UI.
/// </summary>
/// <remarks>
/// Threads: <see cref="Request"/> may be called from any thread; the work runs on one long-running task;
/// <see cref="Completed"/> is raised through the <see cref="SynchronizationContext"/> given to the constructor
/// (null: on the worker). The engine must not be shared with a stored run; the application does not request
/// scratch frames while a run is active.
/// </remarks>
public sealed class ScratchRunner : IDisposable
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	/// <summary>How often the worker checks for a newer request, in steps.</summary>
	private const int CheckInterval = 16;

	/// <summary>Fewer steps than this say nothing reliable about the cost per step.</summary>
	private const int MinStepsToMeasure = 50;

	private readonly SynchronizationContext? _context;
	private readonly SemaphoreSlim _signal = new(0);
	private readonly CancellationTokenSource _cancel = new();
	private readonly object _sync = new();
	private readonly Task _worker;
	private (RunSpec Spec, int Frame)? _request;
	private IWaveEngine? _engine;
	private RunSpec? _engineSpec;
	private FrameSnapshot?[] _snapshots = new FrameSnapshot?[2];
	private int _nextSnapshot;
	private double _msPerStep = double.NaN;
	private int _busy;

	/// <param name="context">Where <see cref="Completed"/> is raised; null raises it on the worker.</param>
	public ScratchRunner(SynchronizationContext? context)
	{
		_context = context;
		_worker = Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning);
	}

	/// <summary>Longest predicted compute time a request may have before it is skipped.</summary>
	public TimeSpan Budget { get; set; } = TimeSpan.FromSeconds(6);

	/// <summary>Whether a request is being computed.</summary>
	public bool IsBusy => Volatile.Read(ref _busy) != 0;

	/// <summary>Cost of one step of the current engine as last measured, or NaN before the first compute.</summary>
	public double MillisecondsPerStep => Volatile.Read(ref _msPerStep);

	/// <summary>A request finished: with a frame, or skipped (<see cref="ScratchResult.Snapshot"/> null).</summary>
	public event EventHandler<ScratchResult>? Completed;

	/// <summary>Asks for frame <paramref name="frame"/> of <paramref name="spec"/>; replaces any request not yet started or in progress.</summary>
	public void Request(RunSpec spec, int frame)
	{
		lock (_sync)
		{
			_request = (spec, frame);
		}
		_signal.Release();
	}

	/// <summary>Stops the worker and releases the engine.</summary>
	public void Dispose()
	{
		_cancel.Cancel();
		_signal.Release();
		try
		{
			_worker.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException)
		{
			// the worker was cancelled
		}
		(_engine as IDisposable)?.Dispose();
		_engine = null;
		_cancel.Dispose();
		_signal.Dispose();
	}

	private (RunSpec Spec, int Frame)? TakeRequest()
	{
		lock (_sync)
		{
			(RunSpec, int)? request = _request;
			_request = null;
			return request;
		}
	}

	private bool HasNewerRequest()
	{
		lock (_sync)
		{
			return _request != null;
		}
	}

	private void Loop()
	{
		CancellationToken token = _cancel.Token;
		while (!token.IsCancellationRequested)
		{
			try
			{
				_signal.Wait(token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			(RunSpec Spec, int Frame)? request = TakeRequest();
			if (request == null)
			{
				continue;
			}
			Volatile.Write(ref _busy, 1);
			try
			{
				Compute(request.Value.Spec, request.Value.Frame, token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Preview computation failed: {Message}", ex.Message);
				(_engine as IDisposable)?.Dispose();
				_engine = null;
				_engineSpec = null;
			}
			finally
			{
				Volatile.Write(ref _busy, 0);
			}
		}
	}

	private void Compute(RunSpec spec, int frame, CancellationToken token)
	{
		int steps = frame * spec.VisualizeEvery + 1;
		double msPerStep = Volatile.Read(ref _msPerStep);
		var estimate = TimeSpan.FromMilliseconds(double.IsNaN(msPerStep) ? 0 : msPerStep * steps);
		if (!double.IsNaN(msPerStep) && estimate > Budget)
		{
			Logger.Debug("Preview frame {Frame} skipped: {Steps} steps would take {Estimate:0.0} s", frame, steps, estimate.TotalSeconds);
			Post(new ScratchResult(spec, frame, null, estimate, []));
			return;
		}
		IWaveEngine engine = EngineFor(spec);
		// the spec's probes, installed per request (Reset removes them; a fresh engine has none)
		ProbeSampler? sampler = spec.Probes.Length > 0 ? ProbeSampler.Build(spec.Probes, spec.Width, spec.Height) : null;
		engine.SetProbes(sampler);
		var clock = Stopwatch.StartNew();
		for (int i = 0; i < steps; i++)
		{
			if (i % CheckInterval == 0 && (HasNewerRequest() || token.IsCancellationRequested))
			{
				return; // the loop picks up the newer request
			}
			engine.AdvanceOneStep();
			if (sampler != null)
			{
				engine.SampleProbes();
			}
		}
		TimeSpan stepping = clock.Elapsed;
		FrameSnapshot snapshot = NextSnapshot(spec);
		engine.CaptureFrame(snapshot, spec.TrackIntensity);
		engine.AwaitFrame(snapshot);
		snapshot.Frame = frame;
		snapshot.Step = steps - 1;
		// the recordings: every series up to this step (integrated probes summed by the collector), the profiles of this instant
		ProbeTrace[] traces = [];
		if (sampler != null)
		{
			var collector = new ProbeCollector(sampler);
			collector.Collect(engine, steps);
			collector.CollectProfiles(engine, steps);
			traces = collector.Traces;
		}
		clock.Stop();
		// the stepping loop alone rules the next prediction (the capture is a fixed cost, and the GPU's first
		// steps include its warm-up), and only when there were enough steps to measure
		if (steps >= MinStepsToMeasure)
		{
			Volatile.Write(ref _msPerStep, stepping.TotalMilliseconds / steps);
		}
		Logger.Debug("Preview frame {Frame}: {Steps} steps in {Elapsed:0} ms on {Engine}", frame, steps, clock.Elapsed.TotalMilliseconds, engine.Description);
		Post(new ScratchResult(spec, frame, snapshot, clock.Elapsed, traces));
	}

	/// <summary>The kept engine, reset to step 0 and given the spec's emitters; rebuilt when the scene or the options differ.</summary>
	private IWaveEngine EngineFor(RunSpec spec)
	{
		if (_engine != null && _engineSpec != null && _engineSpec.SameSceneAs(spec))
		{
			_engine.Reset();
			if (!_engineSpec.SameEmittersAs(spec))
			{
				_engine.SetEmitters(spec.Emitters);
			}
			_engineSpec = spec;
			return _engine;
		}
		(_engine as IDisposable)?.Dispose();
		_engine = null;
		var clock = Stopwatch.StartNew();
		_engine = WaveEngineFactory.Create(spec.BuildScene(), spec.Engine, spec.PreferGpu, spec.Emitters, out string note);
		_engineSpec = spec;
		_msPerStep = double.NaN;
		Logger.Debug("Preview engine: {Engine} in {Elapsed:0} ms{Note}", _engine.Description, clock.ElapsedMilliseconds, note.Length > 0 ? " (" + note + ")" : "");
		return _engine;
	}

	/// <summary>A snapshot of the spec's shape, alternating between two so the one handed out last stays valid while the next is computed.</summary>
	private FrameSnapshot NextSnapshot(RunSpec spec)
	{
		int index = _nextSnapshot;
		_nextSnapshot = (index + 1) % _snapshots.Length;
		FrameSnapshot? snapshot = _snapshots[index];
		if (snapshot == null || snapshot.Shape != spec.Shape || (spec.TrackIntensity && snapshot.IntensityBytes == null))
		{
			snapshot = _snapshots[index] = new FrameSnapshot(spec.Shape, spec.TrackIntensity);
		}
		return snapshot;
	}

	private void Post(ScratchResult result)
	{
		if (_context != null)
		{
			_context.Post(_ => Completed?.Invoke(this, result), null);
		}
		else
		{
			Completed?.Invoke(this, result);
		}
	}
}

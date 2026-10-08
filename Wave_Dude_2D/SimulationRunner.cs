using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Runs a <see cref="SimulationJob"/> on a dedicated background thread ("Wave simulation"), as the loop
	/// in <c>main_curr.py</c>: for every step update the scene (sources), update the field and the intensity;
	/// every N steps render a frame for the previews and the videos, collecting probe data and raising
	/// progress / preview events as it goes. Created and started by <c>Form1</c> (one instance per run);
	/// the form keeps the reference in <c>_runner</c> so stale callbacks from an older run can be ignored.
	/// </summary>
	/// <remarks>
	/// <para><b>Event contract.</b> Every event (<see cref="ProgressChanged"/>, <see cref="EngineReady"/>,
	/// <see cref="ProbesUpdated"/>, <see cref="PreviewReady"/>, <see cref="Finished"/>) is raised
	/// synchronously on the <em>worker thread</em>, never on the UI thread. Handlers must marshal to the UI
	/// themselves; <c>Form1</c> does so through <c>PostToUi</c> (<c>BeginInvoke</c>). Order of events:
	/// <see cref="EngineReady"/> once (after the engine is built, before any frames), then interleaved
	/// <see cref="ProgressChanged"/> / <see cref="ProbesUpdated"/> / <see cref="PreviewReady"/>, then a
	/// final <see cref="ProgressChanged"/> and exactly one <see cref="Finished"/>. If engine creation fails,
	/// only <see cref="ProgressChanged"/> and <see cref="Finished"/> are raised.</para>
	/// <para><b>Pause / cancel handshake.</b> <see cref="_gate"/> is a <see cref="ManualResetEventSlim"/>
	/// that is set while running. <see cref="PauseRun"/> resets it; the worker notices at the top of the
	/// next step, reports the exact step count, stops the elapsed-time stopwatch and blocks on the gate.
	/// <see cref="ResumeRun"/> sets it again. <see cref="Cancel"/> sets the <see cref="_cancel"/> flag and
	/// also sets the gate so a paused run wakes up; the loop exits before the next step with
	/// <see cref="SimulationStatus.Cancelled"/> and the video files are still closed properly.</para>
	/// <para><b>Preview handshake.</b> The worker only renders a new preview when at least
	/// <see cref="PreviewIntervalMs"/> have elapsed since the last one and <see cref="_previewPending"/>
	/// is &lt;= 0. Before raising <see cref="PreviewReady"/> it sets <see cref="_previewPending"/> to the
	/// number of bitmaps it is about to hand out (1 or 2); the UI must call <see cref="PreviewShown"/> once
	/// per bitmap it received (also when it discards it), which decrements the counter. This stops the
	/// worker from flooding the UI message queue with bitmaps faster than they can be displayed.</para>
	/// </remarks>
	internal sealed class SimulationRunner
	{
		/// <summary>
		/// Raised on the worker thread at most every 100 ms with (steps done, total steps), plus once when
		/// pausing (with the exact count) and once just before <see cref="Finished"/>.
		/// </summary>
		public event SimulationRunner.ProgressChangedEventHandler ProgressChanged;

		/// <summary>
		/// Raised once on the worker thread after <see cref="WaveEngineFactory.Create"/> succeeded, with the
		/// engine description (e.g. CPU/GPU) and an optional note (empty string when there is none).
		/// </summary>
		public event SimulationRunner.EngineReadyEventHandler EngineReady;

		/// <summary>
		/// Raised on the worker thread at most every 50 ms (and once at the end) with the new probe measurements
		/// drained since the previous update: the new values of the point probes and the last profile of the line
		/// probes. The receiver owns the arrays. Only raised when the job has probes.
		/// </summary>
		public event SimulationRunner.ProbesUpdatedEventHandler ProbesUpdated;

		/// <summary>
		/// Raised on the worker thread with a freshly allocated preview <see cref="Bitmap"/> (field or
		/// intensity, scaled to fit the preview box). The handler owns the bitmap and must call
		/// <see cref="PreviewShown"/> for every bitmap it receives. Preview bitmaps carry no glow or probe
		/// overlay; the UI composes those.
		/// </summary>
		public event SimulationRunner.PreviewReadyEventHandler PreviewReady;

		/// <summary>
		/// Raised exactly once on the worker thread as the very last action of the run, after the video
		/// files are closed and the engine is disposed. The receiver owns the bitmaps in the result.
		/// </summary>
		public event SimulationRunner.FinishedEventHandler Finished;

		/// <summary>Stores the job and creates the pause gate in the "running" (set) state.</summary>
		public SimulationRunner(SimulationJob job)
		{
			_gate = new ManualResetEventSlim(true);
			_job = job;
		}

		/// <summary>Starts the background worker thread; returns immediately. Call once.</summary>
		public void Start()
		{
			_thread = new Thread(new ThreadStart(Run));
			_thread.IsBackground = true;
			_thread.Name = "Wave simulation";
			_thread.Start();
		}

		/// <summary>
		/// Asks the run to stop after the current step (thread-safe, callable from the UI thread). Also opens
		/// the pause gate so a paused run can wake up and finish; the run then ends with
		/// <see cref="SimulationStatus.Cancelled"/>, the videos are closed properly and <see cref="Finished"/>
		/// is still raised.
		/// </summary>
		public void Cancel()
		{
			Interlocked.Exchange(ref _cancel, 1);
			_gate.Set(); // a paused run must wake up to stop
		}

		/// <summary>
		/// Holds the run before the next step: closes the gate so the worker blocks (it finishes the step in
		/// progress first). The videos stay open. While paused, <see cref="RenderCurrentFrame"/> may be used.
		/// </summary>
		public void PauseRun()
		{
			_gate.Reset();
		}

		/// <summary>Opens the gate so a paused worker continues.</summary>
		public void ResumeRun()
		{
			_gate.Set();
		}

		/// <summary>
		/// Acknowledges one <see cref="PreviewReady"/> bitmap (UI thread). When all outstanding bitmaps are
		/// acknowledged the worker may produce the next preview.
		/// </summary>
		public void PreviewShown()
		{
			Interlocked.Decrement(ref _previewPending);
		}

		/// <summary>
		/// Blocks until the worker thread has ended or the timeout elapses.
		/// </summary>
		/// <returns><c>true</c> if the thread ended (or was never started), <c>false</c> on timeout.</returns>
		public bool WaitForExit(int milliseconds)
		{
			Thread thread = _thread;
			return thread == null || thread.Join(milliseconds);
		}

		/// <summary>
		/// Renders the current field or intensity frame at full engine resolution on demand (for a still-image
		/// export), with probe markers burned in but without the glow overlay. Intended for the UI thread, and
		/// only while the run is paused: it reads the engine buffers directly, which is only safe because the
		/// worker is then guaranteed to be parked on the gate and not touching the engine's arrays.
		/// </summary>
		/// <param name="isField"><c>true</c> for the wave field, <c>false</c> for the intensity.</param>
		/// <param name="lut">ARGB palette with at least 255 entries, or null for greyscale.</param>
		/// <param name="brightness">Gain applied to the raw values before palette lookup.</param>
		/// <returns>
		/// A new <see cref="Bitmap"/> owned by the caller, or null when the run is not paused, no engine
		/// exists yet, or intensity was requested but the engine does not accumulate it.
		/// </returns>
		public Bitmap RenderCurrentFrame(bool isField, int[] lut, float brightness)
		{
			if (_gate.IsSet)
			{
				return null; // running (not paused): unsafe to read
			}
			IWaveEngine sim = _sim;
			if (sim == null)
			{
				return null;
			}
			if (!isField && sim.IntensityData == null)
			{
				return null;
			}
			int width = sim.Width;
			int height = sim.Height;
			sim.PrepareFrame(isField, !isField);
			int[] pixels = new int[width * height];
			if (isField)
			{
				FrameRenderer.RenderField(sim, lut, brightness, pixels, width, height);
				if (_collector != null)
				{
					ProbeOverlay.Burn(pixels, width, height, _job.Probes, _collector.Traces, ProbePlaceOn.Field, _job.Settings.Steps);
				}
			}
			else
			{
				FrameRenderer.RenderIntensity(sim, lut, brightness, pixels, width, height);
				if (_collector != null)
				{
					ProbeOverlay.Burn(pixels, width, height, _job.Probes, _collector.Traces, ProbePlaceOn.Intensity, _job.Settings.Steps);
				}
			}
			return FrameRenderer.ToBitmap(pixels, width, height);
		}

		/// <summary>
		/// Worker thread body. Builds the scene and engine, opens the video sinks, steps the simulation,
		/// and in <c>finally</c> closes the sinks, captures the last full-size frames into the result and
		/// disposes the engine. Never throws: failures end up in <see cref="SimulationResult.Status"/> /
		/// <see cref="SimulationResult.Message"/>. The job's <see cref="SimulationJob.Image"/> and
		/// <see cref="SimulationJob.GlowOverlay"/> bitmaps are disposed here once consumed.
		/// </summary>
		private void Run()
		{
			SimulationResult result = new SimulationResult();
			Stopwatch stopwatch = Stopwatch.StartNew();
			IFrameSink fieldSink = null;
			IFrameSink intensitySink = null;
			IWaveEngine engine = null;
			// declared here, outside the try, because finally also needs them for the last-frame probe overlay
			ProbeCollector collector = null;
			int totalSteps = 0;
			try
			{
				SimulationSettings settings = _job.Settings;
				bool writeField = _job.FieldVideoPath.Length > 0;
				bool writeIntensity = _job.IntensityVideoPath.Length > 0;
				bool needIntensity = writeIntensity || settings.PreviewIntensity;

				// the scene and the simulator
				SceneData scene;
				if (_job.Image != null)
				{
					scene = SceneData.FromImage(_job.Image, _job.UseSources, _job.DampingEdge);
					_job.Image.Dispose();
					_job.Image = null;
				}
				else
				{
					scene = SceneData.Empty(_job.Width, _job.Height, _job.DampingEdge, _job.BackgroundRefractiveIndex);
				}
				int width = scene.Width;
				int height = scene.Height;

				// the probes
				ProbeSampler sampler = null;
				if (_job.Probes != null && _job.Probes.Count > 0)
				{
					sampler = ProbeSampler.Build(_job.Probes, width, height);
					collector = new ProbeCollector(sampler);
					if (sampler.NeedsIntensity)
					{
						needIntensity = true;
					}
				}
				string engineNote = "";
				engine = WaveEngineFactory.Create(scene, settings, _job.SourceAmplitude, needIntensity, ref engineNote, _job.ObjectSources);
				_sim = engine;
				_collector = collector;
				result.Engine = engine.Description;
				result.EngineNote = engineNote;
				EngineReady?.Invoke(engine.Description, engineNote);
				if (sampler != null)
				{
					engine.SetProbes(sampler);
				}

				// videos
				if (writeField)
				{
					fieldSink = SimulationRunner.OpenSink(_job.FieldVideoPath, width, height, settings.VideoFps, settings.VideoQuality, result);
					result.Files.Add(fieldSink.FilePath);
					result.FieldFile = fieldSink.FilePath;
				}
				if (writeIntensity)
				{
					intensitySink = SimulationRunner.OpenSink(_job.IntensityVideoPath, width, height, settings.VideoFps, settings.VideoQuality, result);
					result.Files.Add(intensitySink.FilePath);
					result.IntensityFile = intensitySink.FilePath;
				}
				int[] fullPixels = null;
				if (writeField || writeIntensity)
				{
					fullPixels = new int[width * height];
				}
				// the "glowing edges" overlay for the videos (FrameRenderer.AddOverlay): converted to a plain RGB
				// byte array and gain-prescaled (FrameRenderer.ScaleOverlayRgb) once, up front, rather than redoing
				// either on every single exported frame; this was the actual cost of the overlay on a video export
				byte[] fieldOverlayScaled = null;
				byte[] intensityOverlayScaled = null;
				if (_job.GlowOverlay != null && (writeField || writeIntensity))
				{
					byte[] overlayRgb = FrameRenderer.ToRgbBytes(_job.GlowOverlay, width, height);
					if (writeField)
					{
						fieldOverlayScaled = FrameRenderer.ScaleOverlayRgb(overlayRgb, _job.FieldOverlayGain);
					}
					if (writeIntensity)
					{
						intensityOverlayScaled = FrameRenderer.ScaleOverlayRgb(overlayRgb, _job.IntensityOverlayGain);
					}
				}
				if (_job.GlowOverlay != null)
				{
					_job.GlowOverlay.Dispose();
					_job.GlowOverlay = null;
				}
				// previews are made small enough to fit the preview area: shrunk (never enlarged), keeping the aspect ratio
				int previewWidth = width;
				int previewHeight = height;
				double previewScale = Math.Min((double)_job.PreviewWidth / width, (double)_job.PreviewHeight / height);
				if (previewScale < 1.0)
				{
					previewWidth = Math.Max(1, (int)Math.Floor(width * previewScale));
					previewHeight = Math.Max(1, (int)Math.Floor(height * previewScale));
				}
				int[] previewPixels = null;
				if (settings.PreviewField || settings.PreviewIntensity)
				{
					previewPixels = new int[previewWidth * previewHeight];
				}
				float fieldBrightness = (float)settings.FieldBrightness;
				float intensityBrightness = (float)settings.IntensityBrightness;
				int visualizeEvery = Math.Max(1, settings.VisualizeEvery);
				totalSteps = settings.Steps;
				long lastProgressMs = 0L;
				long lastProbeMs = 0L;
				long lastPreviewMs = 0L;
				result.Status = SimulationStatus.Completed;
				for (int i = 0; i < totalSteps; i++)
				{
					if (!_gate.IsSet)
					{
						// paused: report the exact step count; the time of the pause does not count
						stopwatch.Stop();
						ProgressChanged?.Invoke(result.StepsDone, totalSteps);
						_gate.Wait();
						stopwatch.Start();
					}
					if (Thread.VolatileRead(ref _cancel) != 0)
					{
						result.Status = SimulationStatus.Cancelled;
						break;
					}
					engine.AdvanceOneStep();
					if (sampler != null)
					{
						engine.SampleProbes();
					}
					result.StepsDone = i + 1;
					if (i % visualizeEvery == 0)
					{
						// the probe values up to this step (the graphs in the videos need them)
						if (collector != null)
						{
							collector.Collect(engine, result.StepsDone);
							if (writeField || writeIntensity)
							{
								collector.CollectProfiles(engine, result.StepsDone);
							}
						}
						// previews are only made when the pictures of the last preview have all been shown, and not
						// more often than roughly PreviewIntervalMs. The second condition is the fix for a real bug:
						// without it, this was gated purely by _previewPending (whether the UI thread has finished
						// showing the last one, via ShowPreview/PreviewShown) - and on a GPU engine, PrepareFrame
						// below is where the actual cost is (a full-resolution GPU->host readback, not just the small
						// preview picture), so the GPU run's speed ended up riding on how fast the UI happened to be.
						// With the "display overlay" checkboxes off, SetPreviewFrame is cheap and _previewPending
						// clears almost immediately, so a readback fired on nearly every eligible step; with them on,
						// ComposeGlowOverlay's per-pixel blend made the UI side slow enough to skip most of those
						// readbacks "by accident", which is why turning the overlay on made the run go faster rather
						// than slower. Pacing previews by elapsed time keeps the readback rate steady and no longer
						// dependent on a checkbox that was never meant to affect simulation throughput. Exporting a
						// video already forces a readback every eligible step regardless (see writeField/
						// writeIntensity below), which is why the slowdown was never visible with video export on.
						bool previewDue = stopwatch.ElapsedMilliseconds - lastPreviewMs >= PreviewIntervalMs;
						bool doPreview = previewPixels != null && previewDue && Thread.VolatileRead(ref _previewPending) <= 0;
						if (doPreview)
						{
							lastPreviewMs = stopwatch.ElapsedMilliseconds;
						}
						// a GPU engine copies only the data that is going to be drawn
						engine.PrepareFrame(writeField || (doPreview && settings.PreviewField), writeIntensity || (doPreview && settings.PreviewIntensity));
						if (writeField)
						{
							FrameRenderer.RenderField(engine, _job.FieldLut, fieldBrightness, fullPixels, width, height);
							FrameRenderer.AddOverlay(fullPixels, fieldOverlayScaled, width, height);
							if (collector != null)
							{
								ProbeOverlay.Burn(fullPixels, width, height, _job.Probes, collector.Traces, ProbePlaceOn.Field, totalSteps);
							}
							fieldSink.AddPixels(fullPixels);
						}
						if (writeIntensity)
						{
							FrameRenderer.RenderIntensity(engine, _job.IntensityLut, intensityBrightness, fullPixels, width, height);
							FrameRenderer.AddOverlay(fullPixels, intensityOverlayScaled, width, height);
							if (collector != null)
							{
								ProbeOverlay.Burn(fullPixels, width, height, _job.Probes, collector.Traces, ProbePlaceOn.Intensity, totalSteps);
							}
							intensitySink.AddPixels(fullPixels);
						}
						if (doPreview)
						{
							// Set the outstanding count before raising, so PreviewShown() cannot race it to 0.
							int previewCount = (settings.PreviewField ? 1 : 0) + (settings.PreviewIntensity ? 1 : 0);
							Interlocked.Exchange(ref _previewPending, previewCount);
							if (settings.PreviewField)
							{
								FrameRenderer.RenderField(engine, _job.FieldLut, fieldBrightness, previewPixels, previewWidth, previewHeight);
								PreviewReady?.Invoke(true, FrameRenderer.ToBitmap(previewPixels, previewWidth, previewHeight));
							}
							if (settings.PreviewIntensity)
							{
								FrameRenderer.RenderIntensity(engine, _job.IntensityLut, intensityBrightness, previewPixels, previewWidth, previewHeight);
								PreviewReady?.Invoke(false, FrameRenderer.ToBitmap(previewPixels, previewWidth, previewHeight));
							}
						}
					}
					// the probe values go to the user interface about 20 times a second
					if (collector != null && stopwatch.ElapsedMilliseconds - lastProbeMs >= 50L)
					{
						lastProbeMs = stopwatch.ElapsedMilliseconds;
						collector.Collect(engine, result.StepsDone);
						collector.CollectProfiles(engine, result.StepsDone);
						ProbesUpdated?.Invoke(collector.TakeForUi());
					}
					if (stopwatch.ElapsedMilliseconds - lastProgressMs >= 100L)
					{
						lastProgressMs = stopwatch.ElapsedMilliseconds;
						ProgressChanged?.Invoke(i + 1, totalSteps);
					}
				}

				// the last probe values
				if (collector != null)
				{
					collector.Collect(engine, result.StepsDone);
					collector.CollectProfiles(engine, result.StepsDone);
					ProbesUpdated?.Invoke(collector.TakeForUi());
				}
			}
			catch (OutOfMemoryException)
			{
				result.Status = SimulationStatus.Failed;
				result.Message = "There is not enough memory for a simulation of this size. Use a smaller canvas.";
			}
			catch (Exception ex)
			{
				result.Status = SimulationStatus.Failed;
				result.Message = ex.Message;
			}
			finally
			{
				// close the videos, also after a stop or an error, so that the files can be played
				try
				{
					if (fieldSink != null)
					{
						fieldSink.Close();
					}
				}
				catch (Exception)
				{
					// nothing more can be done
				}
				try
				{
					if (intensitySink != null)
					{
						intensitySink.Close();
					}
				}
				catch (Exception)
				{
					// nothing more can be done
				}
				if (engine != null)
				{
					try
					{
						// one last full-resolution snapshot, so a still export still has something to save once the run
						// has fully stopped and the engine itself is gone (see SimulationResult.LastFieldFrame/LastIntensityFrame)
						int[] lastPixels = new int[engine.Width * engine.Height];
						bool hasIntensity = engine.IntensityData != null;
						engine.PrepareFrame(true, hasIntensity);
						FrameRenderer.RenderField(engine, _job.FieldLut, (float)_job.Settings.FieldBrightness, lastPixels, engine.Width, engine.Height);
						if (collector != null)
						{
							ProbeOverlay.Burn(lastPixels, engine.Width, engine.Height, _job.Probes, collector.Traces, ProbePlaceOn.Field, totalSteps);
						}
						result.LastFieldFrame = FrameRenderer.ToBitmap(lastPixels, engine.Width, engine.Height);
						if (hasIntensity)
						{
							FrameRenderer.RenderIntensity(engine, _job.IntensityLut, (float)_job.Settings.IntensityBrightness, lastPixels, engine.Width, engine.Height);
							if (collector != null)
							{
								ProbeOverlay.Burn(lastPixels, engine.Width, engine.Height, _job.Probes, collector.Traces, ProbePlaceOn.Intensity, totalSteps);
							}
							result.LastIntensityFrame = FrameRenderer.ToBitmap(lastPixels, engine.Width, engine.Height);
						}
					}
					catch (Exception)
					{
						// no still frame this time - not fatal, the export buttons just won't have anything to save
					}
					try
					{
						(engine as IDisposable)?.Dispose();
					}
					catch (Exception)
					{
						// nothing more can be done
					}
					_sim = null;
					_collector = null;
				}
				if (_job.Image != null)
				{
					_job.Image.Dispose();
				}
			}
			stopwatch.Stop();
			result.Elapsed = stopwatch.Elapsed;
			ProgressChanged?.Invoke(result.StepsDone, _job.Settings.Steps);
			Finished?.Invoke(result);
		}

		/// <summary>
		/// Opens a video file. An .mp4 path is written as H.264 with the encoder of Windows (Media Foundation),
		/// falling back to a Motion JPEG .avi (same name, .avi extension) when that is not possible (no Media
		/// Foundation, size not supported); any partial .mp4 is deleted and the reason is stored in
		/// <see cref="SimulationResult.Message"/>. Other extensions always get AVI.
		/// </summary>
		/// <param name="filePath">Requested output path; the extension decides the container.</param>
		/// <param name="w">Frame width in pixels.</param>
		/// <param name="h">Frame height in pixels.</param>
		/// <param name="fps">Playback frame rate.</param>
		/// <param name="quality">1..100; bitrate for MP4, JPEG quality for AVI.</param>
		/// <param name="result">Receives the fallback message; its file lists are filled by the caller.</param>
		/// <returns>An open sink; its <see cref="IFrameSink.FilePath"/> is the path actually used.</returns>
		private static IFrameSink OpenSink(string filePath, int w, int h, int fps, int quality, SimulationResult result)
		{
			if (string.Equals(Path.GetExtension(filePath), ".mp4", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					return new MfVideoWriter(filePath, w, h, fps, quality);
				}
				catch (Exception ex)
				{
					if (File.Exists(filePath))
					{
						try
						{
							File.Delete(filePath);
						}
						catch (Exception)
						{
							// an empty leftover file does no harm
						}
					}
					result.Message = "MP4 (H.264) video could not be made on this computer (" + ex.Message.Trim() + "). The videos were written as Motion JPEG .avi files instead; play them with VLC.";
					filePath = Path.ChangeExtension(filePath, ".avi");
				}
			}
			return new AviFrameSink(filePath, w, h, fps, quality);
		}

		/// <summary>Unused leftover; the JPEG quality actually comes from <see cref="SimulationSettings.VideoQuality"/>.</summary>
		private const long JpegQuality = 90L;

		/// <summary>
		/// Minimum time between two preview frames, in stopwatch milliseconds (about 30 fps). See its use in
		/// <see cref="Run"/> for why this exists: it keeps a GPU engine's readback rate steady and independent of
		/// how fast the UI thread happens to be at displaying the previous one (e.g. with/without the "display
		/// overlay" checkboxes), rather than implicitly riding on <see cref="_previewPending"/> alone. ~30 fps is
		/// already faster than a picture box is usefully watchable at.
		/// </summary>
		private const long PreviewIntervalMs = 33L;

		/// <summary>The job to run; its bitmaps are consumed and disposed by the worker.</summary>
		private readonly SimulationJob _job;

		/// <summary>The worker thread; null until <see cref="Start"/>.</summary>
		private Thread _thread;

		/// <summary>Cancel flag (0/1), written by the UI thread via <see cref="Interlocked"/>, read volatile by the worker.</summary>
		private int _cancel;

		/// <summary>Number of preview bitmaps handed to the UI that have not been acknowledged by <see cref="PreviewShown"/> yet.</summary>
		private int _previewPending;

		/// <summary>Pause gate: set = running, reset = paused. The worker waits on it at the top of each step.</summary>
		private readonly ManualResetEventSlim _gate;

		/// <summary>
		/// The live engine, set once at the start of <see cref="Run"/> and cleared just before it is disposed at the
		/// end; read from the UI thread (<see cref="RenderCurrentFrame"/>) only while paused, when the worker thread
		/// is parked at <c>_gate.Wait()</c> and is not touching it.
		/// </summary>
		private IWaveEngine _sim;

		/// <summary>
		/// The probe collector for this run (null when there are no probes, or after the run ends), set alongside
		/// <see cref="_sim"/> so <see cref="RenderCurrentFrame"/> can also burn the probe markers/graphs onto a still
		/// export: the video export loop in <see cref="Run"/> already did this (<see cref="ProbeOverlay.Burn"/>) but
		/// <see cref="RenderCurrentFrame"/> did not, which left probe data out of exported still frames.
		/// </summary>
		private ProbeCollector _collector;

		/// <summary>Handler for <see cref="ProgressChanged"/>: steps completed so far and the total step count.</summary>
		public delegate void ProgressChangedEventHandler(int stepsDone, int totalSteps);

		/// <summary>Handler for <see cref="EngineReady"/>: engine description and an optional note (empty when none).</summary>
		public delegate void EngineReadyEventHandler(string description, string note);

		/// <summary>Handler for <see cref="ProbesUpdated"/>: the samples drained since the previous update.</summary>
		public delegate void ProbesUpdatedEventHandler(ProbeUpdate update);

		/// <summary>Handler for <see cref="PreviewReady"/>: <paramref name="isField"/> tells which picture box the (caller-owned) bitmap is for.</summary>
		public delegate void PreviewReadyEventHandler(bool isField, Bitmap frame);

		/// <summary>Handler for <see cref="Finished"/>: the completed result, including status, files and last frames.</summary>
		public delegate void FinishedEventHandler(SimulationResult result);
	}
}

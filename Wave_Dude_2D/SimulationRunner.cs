using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Runs a <see cref="SimulationJob"/> on a dedicated background thread ("Wave simulation"),
	/// writing video frames, collecting probe data and raising progress / preview events as it goes.
	/// Created and started by <c>Form1</c> (one instance per run); the form keeps the reference in
	/// <c>_runner</c> so stale callbacks from an older run can be ignored.
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
		/// Raised on the worker thread at most every 50 ms (and once at the end) with the probe samples
		/// drained since the previous update. Only raised when the job has probes.
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
			this._gate = new ManualResetEventSlim(true);
			this._job = job;
		}

		/// <summary>Starts the background worker thread; returns immediately. Call once.</summary>
		public void Start()
		{
			this._thread = new Thread(new ThreadStart(this.Run));
			this._thread.IsBackground = true;
			this._thread.Name = "Wave simulation";
			this._thread.Start();
		}

		/// <summary>
		/// Requests cancellation (thread-safe, callable from the UI thread). Also opens the pause gate so a
		/// paused run can wake up and finish; the run then ends with <see cref="SimulationStatus.Cancelled"/>
		/// and <see cref="Finished"/> is still raised.
		/// </summary>
		public void Cancel()
		{
			Interlocked.Exchange(ref this._cancel, 1);
			this._gate.Set();
		}

		/// <summary>
		/// Closes the gate so the worker blocks before its next step (it finishes the step in progress
		/// first). While paused, <see cref="RenderCurrentFrame"/> may be used.
		/// </summary>
		public void PauseRun()
		{
			this._gate.Reset();
		}

		/// <summary>Opens the gate so a paused worker continues.</summary>
		public void ResumeRun()
		{
			this._gate.Set();
		}

		/// <summary>
		/// Acknowledges one <see cref="PreviewReady"/> bitmap (UI thread). When all outstanding bitmaps are
		/// acknowledged the worker may produce the next preview.
		/// </summary>
		public void PreviewShown()
		{
			Interlocked.Decrement(ref this._previewPending);
		}

		/// <summary>
		/// Blocks until the worker thread has ended or the timeout elapses.
		/// </summary>
		/// <returns><c>true</c> if the thread ended (or was never started), <c>false</c> on timeout.</returns>
		public bool WaitForExit(int milliseconds)
		{
			Thread thread = this._thread;
			return thread == null || thread.Join(milliseconds);
		}

		/// <summary>
		/// Renders the current field or intensity frame at full engine resolution, with probe markers burned
		/// in but without the glow overlay. Intended for the UI thread while the run is paused: it reads the
		/// engine buffers directly, which is only safe because the worker is parked on the gate.
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
			if (this._gate.IsSet)
			{
				return null;
			}
			IWaveEngine sim = this._sim;
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
				if (this._collector != null)
				{
					ProbeOverlay.Burn(pixels, width, height, this._job.Probes, this._collector.Traces, ProbePlaceOn.Field, this._job.Settings.Steps);
				}
			}
			else
			{
				FrameRenderer.RenderIntensity(sim, lut, brightness, pixels, width, height);
				if (this._collector != null)
				{
					ProbeOverlay.Burn(pixels, width, height, this._job.Probes, this._collector.Traces, ProbePlaceOn.Intensity, this._job.Settings.Steps);
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
			ProbeCollector collector = null;
			int totalSteps = 0;
			try
			{
				SimulationSettings settings = this._job.Settings;
				bool writeField = this._job.FieldVideoPath.Length > 0;
				bool writeIntensity = this._job.IntensityVideoPath.Length > 0;
				bool needIntensity = writeIntensity || settings.PreviewIntensity;
				SceneData scene;
				if (this._job.Image != null)
				{
					scene = SceneData.FromImage(this._job.Image, this._job.UseSources, this._job.DampingEdge);
					this._job.Image.Dispose();
					this._job.Image = null;
				}
				else
				{
					scene = SceneData.Empty(this._job.Width, this._job.Height, this._job.DampingEdge, this._job.BackgroundRefractiveIndex);
				}
				int width = scene.Width;
				int height = scene.Height;
				ProbeSampler sampler = null;
				if (this._job.Probes != null && this._job.Probes.Count > 0)
				{
					sampler = ProbeSampler.Build(this._job.Probes, width, height);
					collector = new ProbeCollector(sampler);
					if (sampler.NeedsIntensity)
					{
						needIntensity = true;
					}
				}
				string engineNote = "";
				engine = WaveEngineFactory.Create(scene, settings, this._job.SourceAmplitude, needIntensity, ref engineNote, this._job.ObjectSources);
				this._sim = engine;
				this._collector = collector;
				result.Engine = engine.Description;
				result.EngineNote = engineNote;
				this.EngineReady?.Invoke(engine.Description, engineNote);
				if (sampler != null)
				{
					engine.SetProbes(sampler);
				}
				if (writeField)
				{
					fieldSink = SimulationRunner.OpenSink(this._job.FieldVideoPath, width, height, settings.VideoFps, settings.VideoQuality, result);
					result.Files.Add(fieldSink.FilePath);
					result.FieldFile = fieldSink.FilePath;
				}
				if (writeIntensity)
				{
					intensitySink = SimulationRunner.OpenSink(this._job.IntensityVideoPath, width, height, settings.VideoFps, settings.VideoQuality, result);
					result.Files.Add(intensitySink.FilePath);
					result.IntensityFile = intensitySink.FilePath;
				}
				int[] framePixels = null;
				if (writeField || writeIntensity)
				{
					framePixels = new int[width * height];
				}
				byte[] fieldOverlay = null;
				byte[] intensityOverlay = null;
				if (this._job.GlowOverlay != null && (writeField || writeIntensity))
				{
					byte[] overlayRgb = FrameRenderer.ToRgbBytes(this._job.GlowOverlay, width, height);
					if (writeField)
					{
						fieldOverlay = FrameRenderer.ScaleOverlayRgb(overlayRgb, this._job.FieldOverlayGain);
					}
					if (writeIntensity)
					{
						intensityOverlay = FrameRenderer.ScaleOverlayRgb(overlayRgb, this._job.IntensityOverlayGain);
					}
				}
				if (this._job.GlowOverlay != null)
				{
					this._job.GlowOverlay.Dispose();
					this._job.GlowOverlay = null;
				}
				// Preview is shrunk (never enlarged) to fit the preview box, keeping the aspect ratio.
				int previewWidth = width;
				int previewHeight = height;
				double previewScale = Math.Min((double)this._job.PreviewWidth / width, (double)this._job.PreviewHeight / height);
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
					if (!this._gate.IsSet)
					{
						// Paused: report the exact step count and exclude the wait from the elapsed time.
						stopwatch.Stop();
						this.ProgressChanged?.Invoke(result.StepsDone, totalSteps);
						this._gate.Wait();
						stopwatch.Start();
					}
					if (Thread.VolatileRead(ref this._cancel) != 0)
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
						if (collector != null)
						{
							collector.Collect(engine, result.StepsDone);
							if (writeField || writeIntensity)
							{
								collector.CollectProfiles(engine, result.StepsDone);
							}
						}
						bool previewDue = stopwatch.ElapsedMilliseconds - lastPreviewMs >= PreviewIntervalMs;
						bool doPreview = previewPixels != null && previewDue && Thread.VolatileRead(ref this._previewPending) <= 0;
						if (doPreview)
						{
							lastPreviewMs = stopwatch.ElapsedMilliseconds;
						}
						engine.PrepareFrame(writeField || (doPreview && settings.PreviewField), writeIntensity || (doPreview && settings.PreviewIntensity));
						if (writeField)
						{
							FrameRenderer.RenderField(engine, this._job.FieldLut, fieldBrightness, framePixels, width, height);
							FrameRenderer.AddOverlay(framePixels, fieldOverlay, width, height);
							if (collector != null)
							{
								ProbeOverlay.Burn(framePixels, width, height, this._job.Probes, collector.Traces, ProbePlaceOn.Field, totalSteps);
							}
							fieldSink.AddPixels(framePixels);
						}
						if (writeIntensity)
						{
							FrameRenderer.RenderIntensity(engine, this._job.IntensityLut, intensityBrightness, framePixels, width, height);
							FrameRenderer.AddOverlay(framePixels, intensityOverlay, width, height);
							if (collector != null)
							{
								ProbeOverlay.Burn(framePixels, width, height, this._job.Probes, collector.Traces, ProbePlaceOn.Intensity, totalSteps);
							}
							intensitySink.AddPixels(framePixels);
						}
						if (doPreview)
						{
							// Set the outstanding count before raising, so PreviewShown() cannot race it to 0.
							int previewCount = (settings.PreviewField ? 1 : 0) + (settings.PreviewIntensity ? 1 : 0);
							Interlocked.Exchange(ref this._previewPending, previewCount);
							if (settings.PreviewField)
							{
								FrameRenderer.RenderField(engine, this._job.FieldLut, fieldBrightness, previewPixels, previewWidth, previewHeight);
								this.PreviewReady?.Invoke(true, FrameRenderer.ToBitmap(previewPixels, previewWidth, previewHeight));
							}
							if (settings.PreviewIntensity)
							{
								FrameRenderer.RenderIntensity(engine, this._job.IntensityLut, intensityBrightness, previewPixels, previewWidth, previewHeight);
								this.PreviewReady?.Invoke(false, FrameRenderer.ToBitmap(previewPixels, previewWidth, previewHeight));
							}
						}
					}
					if (collector != null && stopwatch.ElapsedMilliseconds - lastProbeMs >= 50L)
					{
						lastProbeMs = stopwatch.ElapsedMilliseconds;
						collector.Collect(engine, result.StepsDone);
						collector.CollectProfiles(engine, result.StepsDone);
						this.ProbesUpdated?.Invoke(collector.TakeForUi());
					}
					if (stopwatch.ElapsedMilliseconds - lastProgressMs >= 100L)
					{
						lastProgressMs = stopwatch.ElapsedMilliseconds;
						this.ProgressChanged?.Invoke(i + 1, totalSteps);
					}
				}
				if (collector != null)
				{
					collector.Collect(engine, result.StepsDone);
					collector.CollectProfiles(engine, result.StepsDone);
					this.ProbesUpdated?.Invoke(collector.TakeForUi());
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
				try
				{
					if (fieldSink != null)
					{
						fieldSink.Close();
					}
				}
				catch (Exception)
				{
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
				}
				if (engine != null)
				{
					try
					{
						// Capture the final full-size frames for the UI / frame export before the engine goes away.
						int[] lastPixels = new int[engine.Width * engine.Height];
						bool hasIntensity = engine.IntensityData != null;
						engine.PrepareFrame(true, hasIntensity);
						FrameRenderer.RenderField(engine, this._job.FieldLut, (float)this._job.Settings.FieldBrightness, lastPixels, engine.Width, engine.Height);
						if (collector != null)
						{
							ProbeOverlay.Burn(lastPixels, engine.Width, engine.Height, this._job.Probes, collector.Traces, ProbePlaceOn.Field, totalSteps);
						}
						result.LastFieldFrame = FrameRenderer.ToBitmap(lastPixels, engine.Width, engine.Height);
						if (hasIntensity)
						{
							FrameRenderer.RenderIntensity(engine, this._job.IntensityLut, (float)this._job.Settings.IntensityBrightness, lastPixels, engine.Width, engine.Height);
							if (collector != null)
							{
								ProbeOverlay.Burn(lastPixels, engine.Width, engine.Height, this._job.Probes, collector.Traces, ProbePlaceOn.Intensity, totalSteps);
							}
							result.LastIntensityFrame = FrameRenderer.ToBitmap(lastPixels, engine.Width, engine.Height);
						}
					}
					catch (Exception)
					{
					}
					try
					{
						engine.Dispose();
					}
					catch (Exception)
					{
					}
					this._sim = null;
					this._collector = null;
				}
				if (this._job.Image != null)
				{
					this._job.Image.Dispose();
				}
			}
			stopwatch.Stop();
			result.Elapsed = stopwatch.Elapsed;
			this.ProgressChanged?.Invoke(result.StepsDone, this._job.Settings.Steps);
			this.Finished?.Invoke(result);
		}

		/// <summary>
		/// Opens an H.264 MP4 writer for .mp4 paths, falling back to a Motion JPEG .avi (same name, .avi
		/// extension) when the Media Foundation encoder cannot be set up; any partial .mp4 is deleted and
		/// the reason is stored in <see cref="SimulationResult.Message"/>. Other extensions always get AVI.
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

		/// <summary>Minimum time between two preview frames, in stopwatch milliseconds (about 30 fps).</summary>
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

		/// <summary>The live engine, published for <see cref="RenderCurrentFrame"/>; null before creation and after the run ends.</summary>
		private IWaveEngine _sim;

		/// <summary>The live probe collector (null when the job has no probes or after the run ends); used by <see cref="RenderCurrentFrame"/>.</summary>
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

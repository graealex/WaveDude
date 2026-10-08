using System;
using System.Collections.Generic;
using System.Drawing;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Outcome of one simulation run. Filled by <see cref="SimulationRunner"/> on its worker thread and
	/// handed to the form through the <c>Finished</c> event (marshalled to the UI thread by
	/// <c>Form1.PostToUi</c>); after that only the UI thread touches it. String members are never null
	/// (empty means "none"). The receiver takes ownership of the two bitmaps.
	/// </summary>
	internal sealed class SimulationResult
	{
		public SimulationResult()
		{
			Message = "";
			Files = new List<string>();
			FieldFile = "";
			IntensityFile = "";
			Engine = "";
			EngineNote = "";
		}

		/// <summary>How the run ended.</summary>
		public SimulationStatus Status;

		/// <summary>
		/// Failure reason when <see cref="Status"/> is <see cref="SimulationStatus.Failed"/>; for other outcomes it may
		/// carry the note that MP4 encoding was unavailable and .avi files were written instead. Empty when there is nothing to say.
		/// </summary>
		public string Message;

		/// <summary>Number of time steps actually computed (equals the requested steps when completed).</summary>
		public int StepsDone;

		/// <summary>Wall-clock time of the run, excluding the time spent paused.</summary>
		public TimeSpan Elapsed;

		/// <summary>Full paths of the video files that were written, in the order field then intensity (after any .mp4 to .avi fallback).</summary>
		public List<string> Files;

		/// <summary>Full path of the field video, or empty when the field was not exported.</summary>
		public string FieldFile;

		/// <summary>Full path of the intensity video, or empty when the intensity was not exported.</summary>
		public string IntensityFile;

		/// <summary>
		/// Where the simulation ran, the <see cref="IWaveEngine.Description"/> of the engine ("CPU (8 threads)",
		/// "GPU: NVIDIA GeForce MX130 (OpenCL)"); empty when engine creation itself failed.
		/// </summary>
		public string Engine;

		/// <summary>Note from <see cref="WaveEngineFactory.Create"/>: why the GPU was not used when it was asked for; empty when there is none.</summary>
		public string EngineNote;

		/// <summary>
		/// A full simulation-resolution snapshot of the field (with probe overlays burned in), rendered right before the engine
		/// was closed down, so a still export (the Preview tab's "Export current Field/Intensity frame" buttons) has something to
		/// save even once the run has fully stopped and the live engine no longer exists. Null when the run failed before an
		/// engine was created or rendering failed. The receiver owns the bitmap.
		/// </summary>
		public Bitmap LastFieldFrame;

		/// <summary>
		/// The same snapshot of the intensity; null when the intensity was not tracked for this run (or, as for
		/// <see cref="LastFieldFrame"/>, no engine was created or rendering failed). The receiver owns the bitmap.
		/// </summary>
		public Bitmap LastIntensityFrame;
	}
}

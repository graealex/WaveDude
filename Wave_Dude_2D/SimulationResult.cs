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
			this.Message = "";
			this.Files = new List<string>();
			this.FieldFile = "";
			this.IntensityFile = "";
			this.Engine = "";
			this.EngineNote = "";
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

		/// <summary><see cref="IWaveEngine.Description"/> of the engine that ran; empty when engine creation itself failed.</summary>
		public string Engine;

		/// <summary>Note from <see cref="WaveEngineFactory.Create"/> about why the GPU was not used; empty when there is none.</summary>
		public string EngineNote;

		/// <summary>Full-size field frame of the final state (with probe overlays burned in); null when no engine was created or rendering failed.</summary>
		public Bitmap LastFieldFrame;

		/// <summary>Full-size intensity frame of the final state; null when the intensity was not tracked or rendering failed.</summary>
		public Bitmap LastIntensityFrame;
	}
}

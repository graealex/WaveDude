using System.Collections.Generic;
using System.Drawing;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Everything the background worker needs to run one simulation: scene, settings, colour LUTs, output paths and probes.
	/// Built by <c>Form1.StartSimulation</c> on the UI thread, then owned exclusively by <see cref="SimulationRunner"/>'s
	/// worker thread, which also disposes the bitmaps. Scene size and all pixel values are in canvas (scene image) pixels.
	/// </summary>
	internal sealed class SimulationJob
	{
		public SimulationJob()
		{
			this.BackgroundRefractiveIndex = 1.0;
			this.SourceAmplitude = 1.0;
			this.FieldVideoPath = "";
			this.IntensityVideoPath = "";
		}

		/// <summary>
		/// Private copy of the scene image (red = refractive index, green = source level, blue = damping); null for an empty
		/// canvas of <see cref="Width"/> x <see cref="Height"/>. The runner disposes it as soon as the <see cref="SceneData"/> is built.
		/// </summary>
		public Bitmap Image;

		/// <summary>Canvas width in pixels; only used when <see cref="Image"/> is null.</summary>
		public int Width;

		/// <summary>Canvas height in pixels; only used when <see cref="Image"/> is null.</summary>
		public int Height;

		/// <summary>Refractive index of the whole empty canvas (not used with an image); defaults to 1.0.</summary>
		public double BackgroundRefractiveIndex;

		/// <summary>Whether pixels with a green value above 0 in <see cref="Image"/> act as sources.</summary>
		public bool UseSources;

		/// <summary>Amplitude multiplier for the image-defined sources (the scene's image source scale); defaults to 1.0.</summary>
		public double SourceAmplitude;

		/// <summary>Thickness in pixels of the absorbing border around the canvas; 0 = none.</summary>
		public int DampingEdge;

		/// <summary>Snapshot (clone) of the simulation settings, so later edits in the UI do not affect the running job.</summary>
		public SimulationSettings Settings;

		/// <summary>ARGB palette of <see cref="ColorLut.Size"/> entries for field frames; null when neither field export nor field preview is wanted.</summary>
		public int[] FieldLut;

		/// <summary>ARGB palette of <see cref="ColorLut.Size"/> entries for intensity frames; null when neither intensity export nor preview is wanted.</summary>
		public int[] IntensityLut;

		/// <summary>Full path of the field video to write (.mp4, may fall back to .avi); empty = do not export the field.</summary>
		public string FieldVideoPath;

		/// <summary>Full path of the intensity video to write (.mp4, may fall back to .avi); empty = do not export the intensity.</summary>
		public string IntensityVideoPath;

		/// <summary>Probes to sample during the run; null or empty when there are none.</summary>
		public List<ProbeSettings> Probes;

		/// <summary>Object sources (added on the Sources tab) to drive in addition to the image sources; may be null.</summary>
		public List<SourceElement> ObjectSources;

		/// <summary>Maximum preview width in picture-box pixels; the runner shrinks previews to fit (keeping the aspect ratio) but never enlarges them.</summary>
		public int PreviewWidth;

		/// <summary>Maximum preview height in picture-box pixels; see <see cref="PreviewWidth"/>.</summary>
		public int PreviewHeight;

		/// <summary>
		/// Glowing-edges image of the scene, canvas sized, to add onto the exported video frames; null when not exporting or both gains are 0.
		/// The runner converts and disposes it before the first step.
		/// </summary>
		public Bitmap GlowOverlay;

		/// <summary>Brightness multiplier for <see cref="GlowOverlay"/> on field frames; 0 = no overlay.</summary>
		public double FieldOverlayGain;

		/// <summary>Brightness multiplier for <see cref="GlowOverlay"/> on intensity frames; 0 = no overlay.</summary>
		public double IntensityOverlayGain;
	}
}

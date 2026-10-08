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
			BackgroundRefractiveIndex = 1.0;
			SourceAmplitude = 1.0;
			FieldVideoPath = "";
			IntensityVideoPath = "";
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

		/// <summary>Colour table (<see cref="ColorLut.Size"/> packed ARGB values) for field frames, or null for grey levels (<see cref="FrameRenderer"/> then uses the table index itself as the grey level).</summary>
		public int[] FieldLut;

		/// <summary>Colour table (<see cref="ColorLut.Size"/> packed ARGB values) for intensity frames, or null for grey levels.</summary>
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
		/// The Viewer tab's "glowing edges" overlay (<c>Form1</c>'s <c>_glowEdgesImage</c>), exactly the size of the simulation
		/// canvas; null when there is none (no scene image), or when both gains below are 0 (the overlay is switched off for
		/// both Field and Intensity). Frozen at the start of the run, the same as <see cref="Settings"/>; the runner owns and
		/// disposes it (converted to bytes before the first step). See <see cref="FrameRenderer.AddOverlay"/>.
		/// </summary>
		public Bitmap GlowOverlay;

		/// <summary>
		/// How strongly to blend <see cref="GlowOverlay"/> into the Field video, frozen at the start of the run from
		/// <c>Form1</c>'s <c>numBrightnessFieldOverlay</c> and <c>chkDiplayOverlayInField</c> (0 when unchecked, see
		/// <c>Form1.FieldOverlayGain</c>). Only affects the exported videos: the live preview keeps blending its own copy of the
		/// overlay in on the UI thread (<c>Form1.ShowPreview</c>/<c>ComposeGlowOverlay</c>), which can track the checkbox/brightness
		/// box live; a running export cannot, so it uses whatever these were at the moment Start Simulation was pressed.
		/// </summary>
		public double FieldOverlayGain;

		/// <summary>
		/// How strongly to blend <see cref="GlowOverlay"/> into the Intensity video, frozen at the start of the run from
		/// <c>numBrightnessIntensityOverlay</c> and <c>chkDiplayOverlayInIntensity</c> (0 when unchecked); see
		/// <see cref="FieldOverlayGain"/> for why only the exported video is affected.
		/// </summary>
		public double IntensityOverlayGain;
	}
}

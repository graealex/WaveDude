using System.Collections.Immutable;
using NLog;
using WaveDude.Engine;
using WaveDude.Model;
using WaveDude.Probes;

namespace WaveDude.Runs;

/// <summary>A project cannot be run as it stands; the message is meant for the user.</summary>
public sealed class RunSpecException(string message) : Exception(message);

/// <summary>
/// Everything a run needs, snapshotted from the <see cref="Project"/> on the UI thread by <see cref="Create"/>
/// so the worker never touches the (UI-thread-only) model and later edits do not affect a run in progress.
/// Immutable; shared read-only by the worker and the UI.
/// </summary>
/// <remarks>
/// Frames and steps: the run records a frame after every <see cref="VisualizeEvery"/>-th step, starting with
/// the first, so frame k shows the state after 0-based step k × <see cref="VisualizeEvery"/> (the step index
/// the preview readout prints) and the run yields <see cref="FrameCount"/> frames in total.
/// </remarks>
public sealed class RunSpec
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	private RunSpec(Project project, ScenePixels? image, int width, int height, bool trackIntensity, bool preferGpu)
	{
		Seed = project.Seed;
		Steps = project.Simulation.Steps;
		VisualizeEvery = Math.Max(1, project.Simulation.VisualizeEvery);
		PreferGpu = preferGpu;
		Engine = EngineOptions.From(project.Simulation, project.Scene, trackIntensity);
		Image = image;
		Width = width;
		Height = height;
		DampingEdge = project.Scene.DampingEdge;
		RefractiveIndex = project.Scene.RefractiveIndex;
		ImageSources = image != null && project.Scene.ImageSources;
		Emitters = [.. Emitter.FromProject(project)];
		Probes = [.. project.Probes.Where(probe => probe.Active).Select(ProbeSpec.From)];
	}

	/// <summary>The project's random seed, for the record (the emitters already carry their derived seeds).</summary>
	public int Seed { get; }

	/// <summary>Number of simulation steps to run.</summary>
	public int Steps { get; }

	/// <summary>A frame is recorded every this many steps (at least 1).</summary>
	public int VisualizeEvery { get; }

	/// <summary>Number of frames the run produces: ⌈<see cref="Steps"/> / <see cref="VisualizeEvery"/>⌉.</summary>
	public int FrameCount => (Steps + VisualizeEvery - 1) / VisualizeEvery;

	/// <summary>Try the OpenCL engine first; an application setting, not part of the project.</summary>
	public bool PreferGpu { get; }

	/// <summary>The physics options the engine is created with.</summary>
	public EngineOptions Engine { get; }

	/// <summary>Whether the engine tracks the intensity, i.e. the frame store has an intensity plane.</summary>
	public bool TrackIntensity => Engine.TrackIntensity;

	/// <summary>The planes every frame of the store holds.</summary>
	public FrameLayers Layers => FrameLayers.Field | (TrackIntensity ? FrameLayers.Intensity : FrameLayers.None);

	/// <summary>The decoded scene image, or null for a blank canvas of <see cref="Width"/> × <see cref="Height"/>.</summary>
	public ScenePixels? Image { get; }

	/// <summary>Canvas width in pixels.</summary>
	public int Width { get; }

	/// <summary>Canvas height in pixels.</summary>
	public int Height { get; }

	/// <summary>Thickness of the absorbing border in pixels; 0 for none.</summary>
	public int DampingEdge { get; }

	/// <summary>Background refractive index of a blank canvas.</summary>
	public double RefractiveIndex { get; }

	/// <summary>Whether the green channel of the scene image emits.</summary>
	public bool ImageSources { get; }

	/// <summary>The emitters of the enabled object sources (empty when the scene does not use them).</summary>
	public ImmutableArray<Emitter> Emitters { get; }

	/// <summary>The active probes, in project order.</summary>
	public ImmutableArray<ProbeSpec> Probes { get; }

	/// <summary>
	/// Snapshots <paramref name="project"/> for a run. Regenerates the source emitters first (the model only
	/// does that on load, reseed and add, not on edits). UI thread.
	/// </summary>
	/// <param name="project">The project to run; not modified beyond the emitter regeneration.</param>
	/// <param name="image">The decoded scene image when <see cref="Scene.Mode"/> is <see cref="SceneMode.Image"/>; ignored otherwise.</param>
	/// <param name="preferGpu">Try the OpenCL engine first (the application's choice; the CPU is the fallback either way).</param>
	/// <param name="trackIntensity">Store the intensity plane even when nothing in the project needs it (an export of the intensity asks for it).</param>
	/// <exception cref="RunSpecException">The project cannot run: no image for an image scene, or no steps.</exception>
	public static RunSpec Create(Project project, ScenePixels? image, bool preferGpu = true, bool trackIntensity = false)
	{
		project.Normalize();
		if (project.Simulation.Steps < 1)
		{
			throw new RunSpecException("The simulation has no steps to run.");
		}
		int width;
		int height;
		if (project.Scene.Mode == SceneMode.Image)
		{
			if (image == null)
			{
				throw new RunSpecException("The scene uses an image, but no image is loaded.");
			}
			width = image.Width;
			height = image.Height;
			if (project.Scene.ImageSources && SceneData.CountSources(image.Bgra, width, height, image.StrideBytes) == 0)
			{
				Logger.Warn("Image sources are enabled, but the scene image has no green pixels.");
			}
		}
		else
		{
			image = null;
			width = project.Scene.Width;
			height = project.Scene.Height;
		}
		trackIntensity |= project.Display.Intensity.Preview
			|| project.Probes.Any(probe => probe.Active && probe.Measurement != ProbeMeasurement.Amplitude);
		return new RunSpec(project, image, width, height, trackIntensity, preferGpu);
	}

	/// <summary>
	/// Whether an engine built for <paramref name="other"/> serves this spec too: the same canvas (size, image,
	/// damping, refractive index, image sources), the same solver options and engine choice. The emitters may differ.
	/// </summary>
	public bool SameSceneAs(RunSpec other) =>
		Width == other.Width && Height == other.Height && ReferenceEquals(Image, other.Image) && DampingEdge == other.DampingEdge
		&& RefractiveIndex == other.RefractiveIndex && ImageSources == other.ImageSources && Engine == other.Engine && PreferGpu == other.PreferGpu;

	/// <summary>Whether the object sources are the same emitters, value for value.</summary>
	public bool SameEmittersAs(RunSpec other) => Emitters.AsSpan().SequenceEqual(other.Emitters.AsSpan());

	/// <summary>Builds the engine's scene input from the snapshot. Worker; allocates the per-pixel maps.</summary>
	internal SceneData BuildScene() => Image is { } image
		? SceneData.FromBgra(image.Bgra, image.Width, image.Height, image.StrideBytes, ImageSources, DampingEdge)
		: SceneData.Empty(Width, Height, DampingEdge, RefractiveIndex);
}

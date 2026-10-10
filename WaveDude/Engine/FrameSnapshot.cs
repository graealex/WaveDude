using System.Runtime.InteropServices;

namespace WaveDude.Engine;

/// <summary>
/// A host-side copy of one frame in the frame store's format: the field and, optionally, the intensity as
/// <see cref="Half"/> planes of <see cref="Width"/> × <see cref="Height"/> values, row-major, without the
/// engine's padding. Filled by <see cref="IWaveEngine.CaptureFrame"/> and complete after
/// <see cref="IWaveEngine.AwaitFrame"/>; the arrays live on the pinned object heap so the GPU can copy into
/// them asynchronously. Reusable: a pool of two or three lets a run convert and store one frame while the
/// engine computes the next. Not thread-safe; one owner at a time.
/// </summary>
public sealed class FrameSnapshot
{
	/// <param name="width">Canvas width in pixels.</param>
	/// <param name="height">Canvas height in pixels.</param>
	/// <param name="withIntensity">Whether to allocate the intensity plane.</param>
	public FrameSnapshot(int width, int height, bool withIntensity)
	{
		Width = width;
		Height = height;
		Field = GC.AllocateArray<Half>(width * height, pinned: true);
		Intensity = withIntensity ? GC.AllocateArray<Half>(width * height, pinned: true) : null;
	}

	/// <summary>Canvas width in pixels.</summary>
	public int Width { get; }

	/// <summary>Canvas height in pixels.</summary>
	public int Height { get; }

	/// <summary>The field plane; valid after <see cref="IWaveEngine.AwaitFrame"/>.</summary>
	public Half[] Field { get; }

	/// <summary>The intensity plane, or null when the snapshot was created without one.</summary>
	public Half[]? Intensity { get; }

	/// <summary>Whether the last capture filled <see cref="Intensity"/> (the engine may have been asked for the field only).</summary>
	public bool HasIntensity { get; internal set; }

	/// <summary>The frame index the owner captured into this snapshot; a tag for the pipeline, not used by the engines.</summary>
	public int Frame { get; set; }

	/// <summary>The simulation step the snapshot shows (0-based), set by the owner.</summary>
	public int Step { get; set; }

	/// <summary>OpenCL events of the reads in flight (GPU engine), released by <see cref="IWaveEngine.AwaitFrame"/>.</summary>
	internal IntPtr[] Events { get; } = new IntPtr[2];

	/// <summary>Number of valid entries in <see cref="Events"/>.</summary>
	internal int EventCount { get; set; }

	/// <summary>The CPU engine's staging: the planes as floats, copied at capture time and converted in <see cref="IWaveEngine.AwaitFrame"/> (on the awaiting thread, off the stepping thread).</summary>
	internal float[]? FieldFloats { get; set; }

	/// <summary>Intensity staging of the CPU engine; see <see cref="FieldFloats"/>.</summary>
	internal float[]? IntensityFloats { get; set; }

	/// <summary>Whether <see cref="FieldFloats"/> (and <see cref="IntensityFloats"/> when <see cref="HasIntensity"/>) hold a capture not yet converted.</summary>
	internal bool PendingConversion { get; set; }

	/// <summary>Address of a plane's first element, stable because the arrays are pinned.</summary>
	internal static IntPtr AddressOf(Half[] plane) => Marshal.UnsafeAddrOfPinnedArrayElement(plane, 0);
}

using System.Runtime.InteropServices;
using WaveDude.Engine;

namespace WaveDude.Runs;

/// <summary>
/// Something a frame can be read from for display: the <see cref="FrameStore"/> of a run, or a single
/// captured <see cref="FrameSnapshot"/>. Frames are planes of <see cref="Width"/> × <see cref="Height"/>
/// samples in <see cref="Format"/>, shrunk from a canvas of <see cref="CanvasWidth"/> × <see cref="CanvasHeight"/>
/// (the coordinate system of sources and probes). Readers call <see cref="CopyLayer(int, FrameLayer, Span{byte})"/>
/// from any thread.
/// </summary>
public interface IFrameSource
{
	/// <summary>Frame width in samples.</summary>
	int Width { get; }

	/// <summary>Frame height in samples.</summary>
	int Height { get; }

	/// <summary>Width of the simulated canvas the frames were shrunk from, in cells; equals <see cref="Width"/> at full resolution.</summary>
	int CanvasWidth { get; }

	/// <summary>Height of the simulated canvas, in cells.</summary>
	int CanvasHeight { get; }

	/// <summary>The sample format of every plane.</summary>
	FrameFormat Format { get; }

	/// <summary>Frames available: indices 0..Count-1 may be read.</summary>
	int Count { get; }

	/// <summary>Whether the frames hold the given plane.</summary>
	bool Has(FrameLayer layer);

	/// <summary>Reads one plane of a frame as bytes (<see cref="Width"/> × <see cref="Height"/> samples of <see cref="Format"/>) into <paramref name="destination"/>; false when the source is gone.</summary>
	bool CopyLayer(int frame, FrameLayer layer, Span<byte> destination);

	/// <summary>Reads one plane of a frame as <see cref="Half"/> values into <paramref name="destination"/>; false when the source is gone.</summary>
	/// <exception cref="InvalidOperationException">The samples are not halves (<see cref="Format"/>).</exception>
	bool CopyLayer(int frame, FrameLayer layer, Span<Half> destination);
}

/// <summary>A single captured frame as a one-frame <see cref="IFrameSource"/>; every frame index reads the same snapshot.</summary>
public sealed class SnapshotFrameSource(FrameSnapshot snapshot) : IFrameSource
{
	/// <summary>The snapshot read.</summary>
	public FrameSnapshot Snapshot { get; } = snapshot;

	/// <inheritdoc/>
	public int Width => Snapshot.Width;

	/// <inheritdoc/>
	public int Height => Snapshot.Height;

	/// <inheritdoc/>
	public int CanvasWidth => Snapshot.Shape.CanvasWidth;

	/// <inheritdoc/>
	public int CanvasHeight => Snapshot.Shape.CanvasHeight;

	/// <inheritdoc/>
	public FrameFormat Format => Snapshot.Shape.Format;

	/// <inheritdoc/>
	public int Count => 1;

	/// <inheritdoc/>
	public bool Has(FrameLayer layer) => layer == FrameLayer.Field || Snapshot.HasIntensity;

	/// <inheritdoc/>
	public bool CopyLayer(int frame, FrameLayer layer, Span<byte> destination)
	{
		byte[]? plane = layer == FrameLayer.Field ? Snapshot.FieldBytes : Snapshot.HasIntensity ? Snapshot.IntensityBytes : null;
		if (plane == null)
		{
			return false;
		}
		plane.AsSpan().CopyTo(destination);
		return true;
	}

	/// <inheritdoc/>
	public bool CopyLayer(int frame, FrameLayer layer, Span<Half> destination)
	{
		if (Format != FrameFormat.Half)
		{
			throw new InvalidOperationException("The snapshot's samples are palette indices, not halves.");
		}
		return CopyLayer(frame, layer, MemoryMarshal.AsBytes(destination));
	}
}

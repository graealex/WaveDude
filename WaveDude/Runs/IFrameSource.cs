using WaveDude.Engine;

namespace WaveDude.Runs;

/// <summary>
/// Something a frame can be read from for display: the <see cref="FrameStore"/> of a run, or a single
/// captured <see cref="FrameSnapshot"/>. Readers call <see cref="CopyLayer"/> from any thread.
/// </summary>
public interface IFrameSource
{
	/// <summary>Frame width in pixels.</summary>
	int Width { get; }

	/// <summary>Frame height in pixels.</summary>
	int Height { get; }

	/// <summary>Frames available: indices 0..Count-1 may be read.</summary>
	int Count { get; }

	/// <summary>Whether the frames hold the given plane.</summary>
	bool Has(FrameLayer layer);

	/// <summary>Reads one plane of a frame into <paramref name="destination"/>; false when the source is gone.</summary>
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
	public int Count => 1;

	/// <inheritdoc/>
	public bool Has(FrameLayer layer) => layer == FrameLayer.Field || Snapshot.HasIntensity;

	/// <inheritdoc/>
	public bool CopyLayer(int frame, FrameLayer layer, Span<Half> destination)
	{
		Half[]? plane = layer == FrameLayer.Field ? Snapshot.Field : Snapshot.HasIntensity ? Snapshot.Intensity : null;
		if (plane == null)
		{
			return false;
		}
		plane.AsSpan().CopyTo(destination);
		return true;
	}
}

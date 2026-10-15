using WaveDudeNext.Engine;

namespace WaveDudeNext.Runs;

/// <summary>
/// A run's store seen in timeline frames when only every <see cref="Stride"/>-th frame was stored
/// (<see cref="RunSpec.FrameStride"/>, 2026-10-15): timeline frame k reads stored frame ⌊k / stride⌋, so a stored
/// frame stands in for the frames up to the next one and the timeline keeps the project's numbering. Thread-safe
/// like the store it wraps.
/// </summary>
/// <param name="store">The run's store, holding every stride-th frame in order.</param>
/// <param name="stride">Timeline frames per stored frame, at least 1.</param>
/// <param name="frameCount">Frames of the timeline (<see cref="RunSpec.FrameCount"/>), the upper bound of <see cref="Count"/>.</param>
public sealed class StridedFrameSource(FrameStore store, int stride, int frameCount) : IFrameSource
{
	/// <summary>The store read.</summary>
	public FrameStore Store { get; } = store;

	/// <summary>Timeline frames per stored frame.</summary>
	public int Stride { get; } = Math.Max(1, stride);

	/// <inheritdoc/>
	public int Width => Store.Width;

	/// <inheritdoc/>
	public int Height => Store.Height;

	/// <inheritdoc/>
	public int CanvasWidth => Store.CanvasWidth;

	/// <inheritdoc/>
	public int CanvasHeight => Store.CanvasHeight;

	/// <inheritdoc/>
	public FrameFormat Format => Store.Format;

	/// <summary>Timeline frames that can be shown: every stored frame covers <see cref="Stride"/> of them, never beyond the run's frame count.</summary>
	public int Count => (int)Math.Min((long)Store.Count * Stride, frameCount);

	/// <inheritdoc/>
	public bool Has(FrameLayer layer) => Store.Has(layer);

	/// <inheritdoc/>
	public bool CopyLayer(int frame, FrameLayer layer, Span<byte> destination) => Store.CopyLayer(frame / Stride, layer, destination);

	/// <inheritdoc/>
	public bool CopyLayer(int frame, FrameLayer layer, Span<Half> destination) => Store.CopyLayer(frame / Stride, layer, destination);
}

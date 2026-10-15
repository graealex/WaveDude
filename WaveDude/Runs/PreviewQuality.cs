using WaveDude.Engine;

namespace WaveDude.Runs;

/// <summary>
/// How much of a run is kept for the preview (2026-10-15, the user's design for weaker machines and a first
/// step towards a browser build): the frames shrunk by <see cref="ResolutionDivisor"/> along each axis, only
/// every <see cref="FrameDivisor"/>-th frame of the timeline stored (the others are stood in for by the last
/// stored one; the timeline itself keeps the project's numbering), each sample a <see cref="FrameFormat.Half"/>
/// or a palette index byte. An application setting, not part of the project: the simulation itself is the
/// same, only what is captured, stored and shown changes, and an export always uses a <see cref="Full"/> run.
/// Immutable; compared by value.
/// </summary>
/// <param name="ResolutionDivisor">Cells per sample along each axis (1, 2, 4: full, half, quarter resolution).</param>
/// <param name="FrameDivisor">Timeline frames per stored frame (1, 2, 4: every frame, every second, every fourth); see <see cref="RunSpec.FrameStride"/>.</param>
/// <param name="Format">The sample format; <see cref="FrameFormat.Index"/> bakes the display brightness into the frames.</param>
public sealed record PreviewQuality(int ResolutionDivisor, int FrameDivisor, FrameFormat Format)
{
	/// <summary>Every cell of every frame as a <see cref="FrameFormat.Half"/>: what an export needs and the default.</summary>
	public static readonly PreviewQuality Full = new(1, 1, FrameFormat.Half);

	/// <summary>Whether this is <see cref="Full"/>.</summary>
	public bool IsFull => this == Full;

	/// <summary>Validates the record: both divisors at least 1.</summary>
	/// <exception cref="ArgumentOutOfRangeException">A divisor is below 1.</exception>
	public PreviewQuality Validate()
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(ResolutionDivisor, 1, nameof(ResolutionDivisor));
		ArgumentOutOfRangeException.ThrowIfLessThan(FrameDivisor, 1, nameof(FrameDivisor));
		return this;
	}

	/// <inheritdoc/>
	public override string ToString() =>
		IsFull ? "full" : $"1/{ResolutionDivisor} resolution, every {FrameDivisor}. frame, {(Format == FrameFormat.Half ? "16-bit" : "8-bit")}";
}

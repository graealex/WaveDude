namespace WaveDudeNext.Export;

/// <summary>
/// One export output: a video file or an image sequence that takes rendered frames one after the other.
/// Frames are <see cref="Width"/> × <see cref="Height"/> packed BGRA32 pixels (<c>0xAARRGGBB</c> as an
/// <c>int</c>, blue in the lowest byte in memory), row-major, top row first; the alpha byte is ignored.
/// One thread at a time (the export worker); <see cref="Close"/> finishes the file and <see cref="IDisposable.Dispose"/>
/// does the same, so a failed export still leaves a readable file of the frames written so far where the
/// format allows it.
/// </summary>
public interface IFrameSink : IDisposable
{
	/// <summary>Width of every frame in pixels.</summary>
	int Width { get; }

	/// <summary>Height of every frame in pixels.</summary>
	int Height { get; }

	/// <summary>Frames taken so far.</summary>
	int FrameCount { get; }

	/// <summary>A description of the output for the log: the file, or the folder of a sequence.</summary>
	string Output { get; }

	/// <summary>Appends one frame of <see cref="Width"/> × <see cref="Height"/> pixels.</summary>
	/// <param name="pixels">The frame, packed BGRA32, row-major, at least width × height values.</param>
	/// <exception cref="InvalidOperationException">The sink is closed.</exception>
	/// <exception cref="IOException">The file could not be written.</exception>
	void Add(ReadOnlySpan<int> pixels);

	/// <summary>Finishes the output (indexes, trailers, open handles). Idempotent.</summary>
	void Close();
}

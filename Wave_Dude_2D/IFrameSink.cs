using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// A video file that accepts one rendered frame at a time. Implemented by <see cref="MfVideoWriter"/>
	/// (H.264 MP4) and <see cref="AviFrameSink"/> (Motion JPEG AVI); created by <see cref="SimulationRunner"/>
	/// and used only from its worker thread. Frames must have the width/height the sink was opened with.
	/// </summary>
	internal interface IFrameSink : IDisposable
	{
		/// <summary>
		/// Encodes and appends one frame given as a row-major ARGB buffer of width * height ints.
		/// The buffer may be reused by the caller once this returns.
		/// </summary>
		/// <exception cref="InvalidOperationException">The sink has already been closed.</exception>
		void AddPixels(int[] pixels);

		/// <summary>Finalizes and closes the file (writes trailers/index). Safe to call more than once; <see cref="IDisposable.Dispose"/> also closes.</summary>
		void Close();

		/// <summary>The path actually written (may differ in extension from the one requested, see <see cref="SimulationRunner"/>).</summary>
		string FilePath { get; }
	}
}

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Static helpers that turn the field or the intensity of a wave engine (CPU or GPU) into packed ARGB
	/// pixel buffers, like <c>WaveVisualizer.render_field</c> and <c>render_intensity</c> in the Python code:
	/// the value is converted to a table index (see <see cref="ColorLut"/>) and the index to a colour. Without
	/// a colour table (null) the index itself is used as grey level. The picture can be smaller than the
	/// simulation (a preview): every picture pixel then takes the nearest simulation pixel. Also blends a glow
	/// overlay on top and wraps the result in a <see cref="Bitmap"/>. Used by <see cref="SimulationRunner"/>
	/// (worker thread) and by the video sinks. Row loops run in parallel, so the methods are CPU-bound but
	/// not thread-affine.
	/// </summary>
	internal sealed class FrameRenderer
	{
		/// <summary>Not instantiable; all members are static.</summary>
		private FrameRenderer()
		{
		}

		/// <summary>
		/// Renders the wave field, nearest-neighbour scaled from the engine grid (<see cref="IWaveEngine.Width"/> x
		/// <see cref="IWaveEngine.Height"/>) to <paramref name="dw"/> x <paramref name="dh"/>. Reads the engine's padded
		/// buffer (row y starts at <c>(y + 1) * Stride + 1</c>); the caller must have called <see cref="IWaveEngine.PrepareFrame"/>.
		/// </summary>
		/// <param name="sim">Engine whose <see cref="IWaveEngine.FieldData"/> is read.</param>
		/// <param name="lut">ARGB palette indexed 0..254 (127 = zero field), or null for greyscale.</param>
		/// <param name="brightness">Gain applied before the value is clamped to -1..1.</param>
		/// <param name="pixels">Destination, at least <paramref name="dw"/> * <paramref name="dh"/> ARGB ints, row-major.</param>
		/// <param name="dw">Destination width in pixels.</param>
		/// <param name="dh">Destination height in pixels.</param>
		public static void RenderField(IWaveEngine sim, int[] lut, float brightness, int[] pixels, int dw, int dh)
		{
			float[] field = sim.FieldData;
			int stride = sim.Stride;
			int sw = sim.Width;
			int sh = sim.Height;

			Parallel.For(0, dh, y =>
			{
				int srcY = (int)((long)y * sh / dh);
				int srcRowStart = (srcY + 1) * stride + 1;
				int dstRowStart = y * dw;
				for (int x = 0; x < dw; x++)
				{
					int srcX = (int)((long)x * sw / dw);
					float value = field[srcRowStart + srcX];
					pixels[dstRowStart + x] = Colorize(ColorLut.FieldIndex(value, brightness), lut);
				}
			});
		}

		/// <summary>
		/// Renders the accumulated intensity, nearest-neighbour scaled to <paramref name="dw"/> x <paramref name="dh"/>.
		/// Each value is mapped as <c>sqrt(value) * brightness</c> (exponent 0.5, as in the Python code), clamped to 1,
		/// then scaled to palette index 0..254 (non-positive values map to 0). Same layout rules as <see cref="RenderField"/>.
		/// </summary>
		/// <param name="sim">Engine whose <see cref="IWaveEngine.IntensityData"/> is read (must not be null).</param>
		/// <param name="lut">ARGB palette indexed 0..254, or null for greyscale.</param>
		/// <param name="brightness">Gain applied after the square-root compression.</param>
		/// <param name="pixels">Destination, at least <paramref name="dw"/> * <paramref name="dh"/> ARGB ints, row-major.</param>
		/// <param name="dw">Destination width in pixels.</param>
		/// <param name="dh">Destination height in pixels.</param>
		public static void RenderIntensity(IWaveEngine sim, int[] lut, float brightness, int[] pixels, int dw, int dh)
		{
			float[] intensity = sim.IntensityData;
			int stride = sim.Stride;
			int sw = sim.Width;
			int sh = sim.Height;

			Parallel.For(0, dh, y =>
			{
				int srcY = (int)((long)y * sh / dh);
				int srcRowStart = (srcY + 1) * stride + 1;
				int dstRowStart = y * dw;
				for (int x = 0; x < dw; x++)
				{
					int srcX = (int)((long)x * sw / dw);
					float value = intensity[srcRowStart + srcX];
					int index = 0;
					if (value > 0f)
					{
						double scaled = Math.Sqrt(value) * brightness;
						if (scaled > 1.0)
						{
							scaled = 1.0;
						}
						index = (int)(scaled * 254.0);
					}
					pixels[dstRowStart + x] = Colorize(index, lut);
				}
			});
		}

		/// <summary>Looks up a palette index in <paramref name="lut"/>, or builds an opaque grey of that level when there is no LUT.</summary>
		private static int Colorize(int index, int[] lut)
		{
			if (lut != null)
			{
				return lut[index];
			}
			return unchecked((int)0xFF000000) | (index << 16) | (index << 8) | index;
		}

		/// <summary>
		/// Reads <paramref name="bmp"/> into a flat array of tightly packed 24-bit pixels, 3 bytes per pixel in memory
		/// order B,G,R, row by row without padding, exactly <paramref name="w"/> x <paramref name="h"/>. The bitmap is
		/// expected to already be that size in the one place this is used (<see cref="SimulationJob.GlowOverlay"/> in
		/// <see cref="SimulationRunner"/>), so this is just a format normalisation, not a real resize: it is drawn
		/// unscaled at (0,0) onto a black <paramref name="w"/> x <paramref name="h"/> bitmap. Used to additively blend
		/// <c>Form1</c>'s "glowing edges" preview overlay into exported video frames, see <see cref="AddOverlay"/>.
		/// </summary>
		/// <param name="bmp">Source image, normally already <paramref name="w"/> x <paramref name="h"/>.</param>
		/// <param name="w">Width in pixels.</param>
		/// <param name="h">Height in pixels.</param>
		/// <returns>A new array of <c>w * h * 3</c> bytes.</returns>
		public static byte[] ToRgbBytes(Bitmap bmp, int w, int h)
		{
			byte[] rgb = new byte[w * h * 3];
			using (Bitmap normalized = new Bitmap(w, h, PixelFormat.Format24bppRgb))
			{
				using (Graphics g = Graphics.FromImage(normalized))
				{
					g.Clear(Color.Black);
					g.DrawImageUnscaled(bmp, 0, 0);
				}
				Rectangle rect = new Rectangle(0, 0, w, h);
				BitmapData data = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
				try
				{
					int stride = data.Stride;
					byte[] raw = new byte[stride * h];
					Marshal.Copy(data.Scan0, raw, 0, raw.Length);
					for (int y = 0; y < h; y++)
					{
						int srcRow = y * stride;
						int dstRow = y * w * 3;
						for (int x = 0; x < w; x++)
						{
							int src = srcRow + x * 3;
							int dst = dstRow + x * 3;
							rgb[dst] = raw[src];
							rgb[dst + 1] = raw[src + 1];
							rgb[dst + 2] = raw[src + 2];
						}
					}
				}
				finally
				{
					normalized.UnlockBits(data);
				}
			}
			return rgb;
		}

		/// <summary>
		/// Multiplies every byte of a <see cref="ToRgbBytes"/> buffer by <paramref name="gain"/>, rounded and clamped to 255,
		/// so that <see cref="AddOverlay"/>'s per-frame, per-pixel work is a plain add instead of repeating a floating-point
		/// multiply and a byte conversion on the same bytes for every single exported frame. The gain is fixed for the whole
		/// run (frozen at Start Simulation, like everything else in <see cref="SimulationJob"/>), so there is no reason to
		/// redo this more than once.
		/// </summary>
		/// <param name="overlayRgb">Packed B,G,R overlay, or null.</param>
		/// <param name="gain">Multiplier; values &lt;= 0 disable the overlay.</param>
		/// <returns>A new scaled copy, or null (<see cref="AddOverlay"/> then does nothing) when the input is null or the gain is &lt;= 0.</returns>
		public static byte[] ScaleOverlayRgb(byte[] overlayRgb, double gain)
		{
			if (overlayRgb == null || gain <= 0.0)
			{
				return null;
			}
			byte[] scaled = new byte[overlayRgb.Length];
			for (int i = 0; i < overlayRgb.Length; i++)
			{
				scaled[i] = (byte)Math.Round(Math.Min(255.0, overlayRgb[i] * gain));
			}
			return scaled;
		}

		/// <summary>
		/// Additively blends a gain-prescaled packed B,G,R overlay (see <see cref="ScaleOverlayRgb"/>) onto ARGB pixels
		/// (packed 0xAARRGGBB, as <see cref="RenderField"/>/<see cref="RenderIntensity"/> produce) in place, each channel
		/// saturating at 255: never darkens, never touches the alpha byte, and a black overlay pixel (no edge there) leaves
		/// the pixel untouched. No-op when the overlay is null. Parallelised by row like <see cref="RenderField"/>/
		/// <see cref="RenderIntensity"/>, since this runs once per exported video frame at full simulation resolution and
		/// was the actual cost of the glow-edges overlay on a video export; the live preview (<c>Form1</c>'s own
		/// <c>ComposeGlowOverlay</c>, same multiply-then-add idea, kept separate since it runs on the UI thread against a
		/// <see cref="Bitmap"/>) is comparatively small and only runs a few times a second, so it was never the bottleneck.
		/// </summary>
		/// <param name="pixels">ARGB buffer of <paramref name="w"/> * <paramref name="h"/> ints, modified in place.</param>
		/// <param name="scaledOverlayRgb">Overlay of <paramref name="w"/> * <paramref name="h"/> * 3 bytes, or null.</param>
		/// <param name="w">Width in pixels.</param>
		/// <param name="h">Height in pixels.</param>
		public static void AddOverlay(int[] pixels, byte[] scaledOverlayRgb, int w, int h)
		{
			if (scaledOverlayRgb == null)
			{
				return;
			}
			Parallel.For(0, h, y =>
			{
				int pixelRow = y * w;
				int overlayRow = y * w * 3;
				for (int x = 0; x < w; x++)
				{
					int o = overlayRow + x * 3;
					int ob = scaledOverlayRgb[o];
					int og = scaledOverlayRgb[o + 1];
					int or = scaledOverlayRgb[o + 2];
					if (ob == 0 && og == 0 && or == 0)
					{
						continue;
					}
					int p = pixelRow + x;
					int argb = pixels[p];
					int a = argb & unchecked((int)0xFF000000);
					int r = Math.Min(255, ((argb >> 16) & 255) + or);
					int g = Math.Min(255, ((argb >> 8) & 255) + og);
					int b = Math.Min(255, (argb & 255) + ob);
					pixels[p] = a | (r << 16) | (g << 8) | b;
				}
			});
		}

		/// <summary>
		/// Copies a row-major ARGB buffer into a new 32bpp ARGB <see cref="Bitmap"/> (top-down). The caller owns and
		/// must dispose the bitmap; the source array is not referenced afterwards.
		/// </summary>
		/// <param name="pixels">At least <paramref name="w"/> * <paramref name="h"/> ARGB ints.</param>
		/// <param name="w">Bitmap width in pixels.</param>
		/// <param name="h">Bitmap height in pixels.</param>
		public static Bitmap ToBitmap(int[] pixels, int w, int h)
		{
			Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
			BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				for (int y = 0; y < h; y++)
				{
					Marshal.Copy(pixels, y * w, IntPtr.Add(data.Scan0, y * data.Stride), w);
				}
			}
			finally
			{
				bitmap.UnlockBits(data);
			}
			return bitmap;
		}
	}
}

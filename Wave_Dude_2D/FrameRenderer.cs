using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Static helpers that turn raw engine field/intensity data into packed ARGB pixel buffers
	/// (optionally through a colour LUT), blend a glow overlay on top and wrap the result in a
	/// <see cref="Bitmap"/>. Used by <see cref="SimulationRunner"/> (worker thread) and by the video sinks.
	/// Row loops run in parallel, so the methods are CPU-bound but not thread-affine.
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
		/// Each value is mapped as <c>sqrt(value) * brightness</c>, clamped to 1, then scaled to palette index 0..254
		/// (non-positive values map to 0). Same layout rules as <see cref="RenderField"/>.
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
		/// Draws <paramref name="bmp"/> unscaled at (0,0) onto a black <paramref name="w"/> x <paramref name="h"/> canvas and
		/// returns the canvas as tightly packed 24-bit pixels, 3 bytes per pixel in memory order B,G,R, no row padding.
		/// </summary>
		/// <param name="bmp">Source image (any size; clipped or padded with black to the canvas).</param>
		/// <param name="w">Canvas width in pixels.</param>
		/// <param name="h">Canvas height in pixels.</param>
		/// <returns>A new array of <c>w * h * 3</c> bytes.</returns>
		public static byte[] ToRgbBytes(Bitmap bmp, int w, int h)
		{
			byte[] rgb = new byte[w * h * 3];
			using (Bitmap canvas = new Bitmap(w, h, PixelFormat.Format24bppRgb))
			{
				using (Graphics g = Graphics.FromImage(canvas))
				{
					g.Clear(Color.Black);
					g.DrawImageUnscaled(bmp, 0, 0);
				}
				Rectangle rect = new Rectangle(0, 0, w, h);
				BitmapData data = canvas.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
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
					canvas.UnlockBits(data);
				}
			}
			return rgb;
		}

		/// <summary>
		/// Multiplies every byte of a <see cref="ToRgbBytes"/> buffer by <paramref name="gain"/>, rounded and clamped to 255.
		/// </summary>
		/// <param name="overlayRgb">Packed B,G,R overlay, or null.</param>
		/// <param name="gain">Multiplier; values &lt;= 0 disable the overlay.</param>
		/// <returns>A new scaled copy, or null when the input is null or the gain is &lt;= 0 (nothing to overlay).</returns>
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
		/// Additively blends a packed B,G,R overlay (see <see cref="ToRgbBytes"/>) onto ARGB pixels in place, each channel
		/// saturating at 255; the alpha byte is kept. Fully black overlay pixels are skipped. No-op when the overlay is null.
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

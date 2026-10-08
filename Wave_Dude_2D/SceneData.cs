using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Per-pixel simulation input derived from a scene image: squared wave speed (<see cref="C2"/>,
	/// <c>(1 / refractive index)^2</c>; the time step is 1), damping factor (<see cref="D"/>, the factor on the
	/// velocity: 1 = no damping, 0 = the wave is stopped) and the positions / levels of image-defined sources
	/// (the value of the green channel sets the frequency).
	/// The rules are those of the Python classes <c>StaticImageScene</c>, <c>StaticRefractiveIndex</c> and
	/// <c>StaticDampening</c>: red channel = refractive index * 100 (clamped to 0.9..10), green = every pixel
	/// above 0 is a sinusoidal source whose level is the green value, blue = damping strength (0 = none,
	/// 255 = full; stored as <c>D = 1 - blue / 255</c>), damping edge = a border of the given thickness with
	/// damping factor <c>sqrt(i / thickness)</c> at i pixels from the edge.
	/// Created by <see cref="SimulationRunner"/> on its worker thread
	/// via <see cref="FromImage"/> or <see cref="Empty"/> and then copied into the padded grids of
	/// <see cref="WaveSimulator"/> / <see cref="GpuWaveSimulator"/>. The arrays are row-major
	/// (<c>y * Width + x</c>) without padding and are not modified after construction.
	/// </summary>
	internal sealed class SceneData
	{
		private SceneData(int w, int h, float[] c2, float[] d, int[] srcX, int[] srcY, byte[] srcLevel)
		{
			Width = w;
			Height = h;
			C2 = c2;
			D = d;
			SourceX = srcX;
			SourceY = srcY;
			SourceLevel = srcLevel;
		}

		/// <summary>Number of image-defined source pixels (length of <see cref="SourceX"/>, <see cref="SourceY"/> and <see cref="SourceLevel"/>).</summary>
		public int SourceCount => SourceX.Length;

		/// <summary>
		/// Decodes <paramref name="image"/> (any pixel format; read as 32bpp ARGB) into per-pixel squared
		/// speed and damping factor and, when <paramref name="useSources"/> is set, collects every pixel
		/// with green &gt; 0 as a source whose level is that green value, in row-major order. Finally
		/// fades the damping in a border of <paramref name="dampingEdge"/> pixels (see <see cref="ApplyBorder"/>).
		/// The bitmap is only locked for reading and is not disposed.
		/// </summary>
		/// <param name="image">Scene image; its size becomes the canvas size.</param>
		/// <param name="useSources">Collect green pixels as sources; otherwise the source arrays are empty.</param>
		/// <param name="dampingEdge">Thickness of the absorbing border in pixels; 0 or less for none.</param>
		public static SceneData FromImage(Bitmap image, bool useSources, int dampingEdge)
		{
			int width = image.Width;
			int height = image.Height;
			float[] c2 = new float[width * height];
			float[] d = new float[width * height];
			List<int> sourceX = new List<int>();
			List<int> sourceY = new List<int>();
			List<byte> sourceLevel = new List<byte>();
			BitmapData data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				byte[] row = new byte[width * 4];
				for (int y = 0; y < height; y++)
				{
					Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, width * 4);
					for (int x = 0; x < width; x++)
					{
						int offset = x * 4;
						// Format32bppArgb is stored as B, G, R, A
						int blue = row[offset];
						int green = row[offset + 1];
						int red = row[offset + 2];
						double refractiveIndex = red / 100.0;
						if (refractiveIndex < 0.9)
						{
							refractiveIndex = 0.9;
						}
						if (refractiveIndex > 10.0)
						{
							refractiveIndex = 10.0;
						}
						double speed = 1.0 / refractiveIndex;
						int index = y * width + x;
						c2[index] = (float)(speed * speed);
						d[index] = (float)(1.0 - blue / 255.0);
						if (useSources && green > 0)
						{
							sourceX.Add(x);
							sourceY.Add(y);
							sourceLevel.Add((byte)green);
						}
					}
				}
			}
			finally
			{
				image.UnlockBits(data);
			}
			SceneData.ApplyBorder(d, width, height, dampingEdge);
			return new SceneData(width, height, c2, d, sourceX.ToArray(), sourceY.ToArray(), sourceLevel.ToArray());
		}

		/// <summary>
		/// Creates a uniform, user defined <paramref name="w"/> x <paramref name="h"/> scene without sources: the
		/// speed is derived from <paramref name="refractiveIndex"/> (clamped to 0.9..10) and the damping
		/// factor is 1 (none) everywhere except in the <paramref name="dampingEdge"/> border.
		/// </summary>
		/// <param name="w">Canvas width in pixels.</param>
		/// <param name="h">Canvas height in pixels.</param>
		/// <param name="dampingEdge">Thickness of the absorbing border in pixels; 0 or less for none.</param>
		/// <param name="refractiveIndex">Background refractive index (1.0 by default, the same rule as a scene image's red channel: index = red / 100).</param>
		public static SceneData Empty(int w, int h, int dampingEdge, double refractiveIndex = 1.0)
		{
			double clampedIndex = refractiveIndex;
			if (clampedIndex < 0.9)
			{
				clampedIndex = 0.9;
			}
			if (clampedIndex > 10.0)
			{
				clampedIndex = 10.0;
			}
			float speed = (float)(1.0 / clampedIndex);
			float[] c2 = new float[w * h];
			float[] d = new float[w * h];
			for (int i = 0; i < c2.Length; i++)
			{
				c2[i] = speed * speed;
				d[i] = 1f;
			}
			SceneData.ApplyBorder(d, w, h, dampingEdge);
			return new SceneData(w, h, c2, d, new int[0], new int[0], new byte[0]);
		}

		/// <summary>
		/// Counts the pixels of <paramref name="image"/> with green &gt; 0, i.e. the number of sources
		/// <see cref="FromImage"/> would collect. Used by <see cref="Form1"/> on the UI thread before a
		/// run to warn when image sources are enabled but the image contains none.
		/// </summary>
		public static int CountSources(Bitmap image)
		{
			int width = image.Width;
			int height = image.Height;
			int count = 0;
			BitmapData data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				byte[] row = new byte[width * 4];
				for (int y = 0; y < height; y++)
				{
					Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, width * 4);
					for (int x = 0; x < width; x++)
					{
						if (row[x * 4 + 1] > 0)
						{
							count++;
						}
					}
				}
			}
			finally
			{
				image.UnlockBits(data);
			}
			return count;
		}

		/// <summary>
		/// Damping edge, as in <c>StaticDampening</c>: overwrites the damping factor in a frame of
		/// <paramref name="thickness"/> pixels (capped at half the smaller side) around the edge of the
		/// <paramref name="w"/> x <paramref name="h"/> grid with <c>sqrt(i / thickness)</c>, where i is the
		/// distance from the edge, so the outermost ring stops the wave completely and the factor rises to 1
		/// inside. Does nothing for a thickness of 0 or less.
		/// </summary>
		/// <param name="d">Damping array, row-major, modified in place.</param>
		/// <param name="w">Grid width in pixels.</param>
		/// <param name="h">Grid height in pixels.</param>
		/// <param name="thickness">Requested border thickness in pixels.</param>
		internal static void ApplyBorder(float[] d, int w, int h, int thickness)
		{
			int border = Math.Min(thickness, Math.Min(w, h) / 2);
			if (border <= 0)
			{
				return;
			}
			for (int i = 0; i < border; i++)
			{
				float factor = (float)Math.Sqrt((double)i / border);
				for (int x = i; x <= w - 1 - i; x++)
				{
					d[i * w + x] = factor;
					d[(h - 1 - i) * w + x] = factor;
				}
				for (int y = i; y <= h - 1 - i; y++)
				{
					d[y * w + i] = factor;
					d[y * w + (w - 1 - i)] = factor;
				}
			}
		}

		/// <summary>Canvas width in pixels.</summary>
		public readonly int Width;

		/// <summary>Canvas height in pixels.</summary>
		public readonly int Height;

		/// <summary>Squared wave speed per pixel (<c>1 / n^2</c> for refractive index n, so 1 for n = 1), row-major.</summary>
		public readonly float[] C2;

		/// <summary>Damping factor per pixel, 0..1 where 1 = no damping, row-major.</summary>
		public readonly float[] D;

		/// <summary>X canvas coordinate of each image-defined source pixel; parallel to <see cref="SourceY"/> and <see cref="SourceLevel"/>.</summary>
		public readonly int[] SourceX;

		/// <summary>Y canvas coordinate of each image-defined source pixel.</summary>
		public readonly int[] SourceY;

		/// <summary>Green channel value (1..255) of each image-defined source pixel; the engines treat it as the source's level.</summary>
		public readonly byte[] SourceLevel;
	}
}

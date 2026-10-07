using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Burns probe markers and probe graph boxes directly into a 32-bit ARGB pixel buffer, for frames that go
	/// to a video sink or into the final result and therefore have no on-screen Graphics surface. Called by
	/// <c>SimulationRunner</c> on the simulation worker thread with the collector's traces
	/// (<see cref="ProbeCollector.Traces"/>); also from the UI thread through
	/// <c>SimulationRunner.RenderCurrentFrame</c>, but only while the worker is parked at the pause gate, so
	/// the traces are never read while they are being written.
	/// </summary>
	internal sealed class ProbeOverlay
	{
		private ProbeOverlay()
		{
		}

		/// <summary>
		/// Draws the markers and, where the box is at least 60 x 30 pixels, the graph of every active probe placed
		/// on <paramref name="placeOn"/> into <paramref name="pixels"/>. Graphs are rendered by
		/// <see cref="ProbeGraphPainter.DrawGraph"/> into a temporary premultiplied bitmap and alpha-blended in.
		/// </summary>
		/// <param name="pixels">Opaque ARGB frame, row-major, index y * w + x; modified in place.</param>
		/// <param name="w">Frame width in pixels (equals the canvas width).</param>
		/// <param name="h">Frame height in pixels.</param>
		/// <param name="probes">Run probe list.</param>
		/// <param name="traces">Trace per probe, index parallel to <paramref name="probes"/>.</param>
		/// <param name="placeOn">Surface this frame shows; only probes placed on it are drawn.</param>
		/// <param name="totalSteps">Total steps of the run, for the x-axis of full-scale series graphs.</param>
		public static void Burn(int[] pixels, int w, int h, IList<ProbeSettings> probes, ProbeTrace[] traces, ProbePlaceOn placeOn, int totalSteps)
		{
			for (int i = 0; i < probes.Count; i++)
			{
				ProbeSettings probe = probes[i];
				if (probe.PlaceOn != placeOn || !probe.Active)
				{
					continue;
				}
				ProbeOverlay.DrawMarkerPixels(pixels, w, h, probe, i);
				Rectangle box = ProbeGraphPainter.BoxInCanvas(probe, w, h);
				if (box.Width < 60 || box.Height < 30)
				{
					continue;
				}
				using (Bitmap graphBitmap = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppPArgb))
				{
					using (Graphics g = Graphics.FromImage(graphBitmap))
					{
						g.Clear(Color.Transparent);
						ProbeGraphPainter.DrawGraph(g, new Rectangle(0, 0, box.Width, box.Height), probe.Title(i), probe, traces[i], totalSteps);
					}
					ProbeOverlay.Blend(pixels, w, h, graphBitmap, box);
				}
			}
		}

		/// <summary>
		/// Alpha-blends a premultiplied-ARGB bitmap onto the pixel buffer with its top-left corner at the position
		/// of <paramref name="rect"/> (buffer coordinates); parts outside the buffer are clipped. Fully transparent
		/// source pixels are skipped, result pixels are always opaque.
		/// </summary>
		private static void Blend(int[] pixels, int w, int h, Bitmap bmp, Rectangle rect)
		{
			BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
			try
			{
				int[] row = new int[bmp.Width];
				for (int i = 0; i < bmp.Height; i++)
				{
					int y = rect.Y + i;
					if (y < 0 || y >= h)
					{
						continue;
					}
					Marshal.Copy(IntPtr.Add(data.Scan0, i * data.Stride), row, 0, bmp.Width);
					for (int j = 0; j < bmp.Width; j++)
					{
						int x = rect.X + j;
						if (x < 0 || x >= w)
						{
							continue;
						}
						int src = row[j];
						int alpha = (src >> 24) & 255;
						if (alpha == 0)
						{
							continue;
						}
						int index = y * w + x;
						if (alpha == 255)
						{
							pixels[index] = src | unchecked((int)0xFF000000);
						}
						else
						{
							// src is premultiplied: out = src + dst * (1 - alpha), rounded to nearest
							int dst = pixels[index];
							int inverseAlpha = 255 - alpha;
							int r = Math.Min(255, ((src >> 16) & 255) + (((dst >> 16) & 255) * inverseAlpha + 127) / 255);
							int gr = Math.Min(255, ((src >> 8) & 255) + (((dst >> 8) & 255) * inverseAlpha + 127) / 255);
							int b = Math.Min(255, (src & 255) + ((dst & 255) * inverseAlpha + 127) / 255);
							pixels[index] = unchecked((int)0xFF000000) | (r << 16) | (gr << 8) | b;
						}
					}
				}
			}
			finally
			{
				bmp.UnlockBits(data);
			}
		}

		/// <summary>
		/// Draws the probe cross(es) and, for line probes, the connecting line in two passes (a black outline, then
		/// a thinner white core), followed by the 1-based index label ("nA"/"nB" for line probes) above and to the
		/// right of each cross. Arm length and font size scale with the frame height.
		/// </summary>
		/// <param name="pixels">Opaque ARGB frame, modified in place.</param>
		/// <param name="w">Frame width in pixels.</param>
		/// <param name="h">Frame height in pixels.</param>
		/// <param name="probe">Probe whose positions (canvas pixels) and kind are drawn.</param>
		/// <param name="index">Zero-based probe index; shown as index + 1.</param>
		private static void DrawMarkerPixels(int[] pixels, int w, int h, ProbeSettings probe, int index)
		{
			int arm = Math.Max(6, h / 120);
			bool isLine = probe.Kind == ProbeKind.Line;
			for (int pass = 0; pass <= 1; pass++)
			{
				int color = (pass == 0) ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF);
				int radius = (pass == 0) ? 2 : 1;
				if (isLine)
				{
					ProbeOverlay.DrawThickLine(pixels, w, h, probe.PosAX, probe.PosAY, probe.PosBX, probe.PosBY, color, radius);
				}
				ProbeOverlay.DrawCrossPixels(pixels, w, h, probe.PosAX, probe.PosAY, arm, color, radius);
				if (isLine)
				{
					ProbeOverlay.DrawCrossPixels(pixels, w, h, probe.PosBX, probe.PosBY, arm, color, radius);
				}
			}
			string label = (index + 1).ToString(CultureInfo.InvariantCulture);
			float fontPx = Math.Max(12f, h / 60f);
			if (isLine)
			{
				ProbeOverlay.BurnLabel(pixels, w, h, label + "A", probe.PosAX + arm + 2, probe.PosAY - arm - (int)Math.Round(fontPx * 1.4f), fontPx);
				ProbeOverlay.BurnLabel(pixels, w, h, label + "B", probe.PosBX + arm + 2, probe.PosBY - arm - (int)Math.Round(fontPx * 1.4f), fontPx);
			}
			else
			{
				ProbeOverlay.BurnLabel(pixels, w, h, label, probe.PosAX + arm + 2, probe.PosAY - arm - (int)Math.Round(fontPx * 1.4f), fontPx);
			}
		}

		/// <summary>
		/// Renders <paramref name="text"/> with a drop shadow into a temporary bitmap (sized with a throwaway 1 x 1
		/// measuring bitmap) and blends it in with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>)
		/// in buffer coordinates.
		/// </summary>
		/// <param name="pixels">Opaque ARGB frame, modified in place.</param>
		/// <param name="w">Frame width in pixels.</param>
		/// <param name="h">Frame height in pixels.</param>
		/// <param name="text">Label text.</param>
		/// <param name="x">Left edge of the label in buffer pixels.</param>
		/// <param name="y">Top edge of the label in buffer pixels.</param>
		/// <param name="fontPx">Font height in pixels.</param>
		private static void BurnLabel(int[] pixels, int w, int h, string text, int x, int y, float fontPx)
		{
			using (Font font = new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
			{
				int labelWidth;
				int labelHeight;
				using (Bitmap measureBitmap = new Bitmap(1, 1))
				{
					using (Graphics measureGraphics = Graphics.FromImage(measureBitmap))
					{
						SizeF textSize = measureGraphics.MeasureString(text, font);
						labelWidth = (int)Math.Ceiling(textSize.Width) + 4;
						labelHeight = (int)Math.Ceiling(textSize.Height) + 4;
					}
				}
				using (Bitmap labelBitmap = new Bitmap(labelWidth, labelHeight, PixelFormat.Format32bppPArgb))
				{
					using (Graphics g = Graphics.FromImage(labelBitmap))
					{
						g.Clear(Color.Transparent);
						g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
						using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
						{
							using (SolidBrush textBrush = new SolidBrush(Color.White))
							{
								g.DrawString(text, font, shadowBrush, 3f, 3f);
								g.DrawString(text, font, textBrush, 2f, 2f);
							}
						}
					}
					ProbeOverlay.Blend(pixels, w, h, labelBitmap, new Rectangle(x, y, labelWidth, labelHeight));
				}
			}
		}

		/// <summary>Draws a plus-shaped cross centred on (x, y) with arms of <paramref name="arm"/> pixels, using <see cref="DrawThickLine"/>.</summary>
		private static void DrawCrossPixels(int[] pixels, int w, int h, int x, int y, int arm, int color, int radius)
		{
			ProbeOverlay.DrawThickLine(pixels, w, h, x - arm, y, x + arm, y, color, radius);
			ProbeOverlay.DrawThickLine(pixels, w, h, x, y - arm, x, y + arm, color, radius);
		}

		/// <summary>
		/// Bresenham line from (x0, y0) to (x1, y1), both inclusive, stamped with a square brush of side
		/// 2 * <paramref name="radius"/> + 1; pixels outside the buffer are skipped. Gives up after 100000 steps
		/// as a safety net against runaway coordinates.
		/// </summary>
		/// <param name="pixels">Opaque ARGB frame, modified in place.</param>
		/// <param name="w">Frame width in pixels.</param>
		/// <param name="h">Frame height in pixels.</param>
		/// <param name="x0">Start x in buffer pixels.</param>
		/// <param name="y0">Start y in buffer pixels.</param>
		/// <param name="x1">End x in buffer pixels.</param>
		/// <param name="y1">End y in buffer pixels.</param>
		/// <param name="color">ARGB value written to every covered pixel (no blending).</param>
		/// <param name="radius">Brush half-width in pixels; 0 draws a one-pixel line.</param>
		private static void DrawThickLine(int[] pixels, int w, int h, int x0, int y0, int x1, int y1, int color, int radius)
		{
			int dx = Math.Abs(x1 - x0);
			int dy = -Math.Abs(y1 - y0);
			int stepX = (x0 < x1) ? 1 : -1;
			int stepY = (y0 < y1) ? 1 : -1;
			int error = dx + dy;
			int iterations = 0;
			while (true)
			{
				for (int oy = -radius; oy <= radius; oy++)
				{
					for (int ox = -radius; ox <= radius; ox++)
					{
						int px = x0 + ox;
						int py = y0 + oy;
						if (px >= 0 && px < w && py >= 0 && py < h)
						{
							pixels[py * w + px] = color;
						}
					}
				}
				if ((x0 == x1 && y0 == y1) || iterations > 100000)
				{
					break;
				}
				iterations++;
				int error2 = 2 * error;
				if (error2 >= dy)
				{
					error += dy;
					x0 += stepX;
				}
				if (error2 <= dx)
				{
					error += dx;
					y0 += stepY;
				}
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// GDI+ rendering of probe graphs (time series / line profiles) and of the probe position markers.
	/// Stateless: every call draws onto the <see cref="Graphics"/> it is given. Used on the UI thread by the
	/// main form's paint handlers (painting the form's own copies of the traces onto the picture boxes) and on
	/// the simulation worker thread by <see cref="ProbeOverlay"/> (painting the collector's traces into an
	/// off-screen bitmap). A <see cref="ProbeTrace"/> must only be painted by the thread that owns it.
	/// </summary>
	internal sealed class ProbeGraphPainter
	{
		private ProbeGraphPainter()
		{
		}

		/// <summary>Unscaled graph box size in canvas pixels for a box size setting; Medium (and any unknown value) is 400 x 200.</summary>
		public static Size NominalBoxSize(ProbeBoxSize boxSize)
		{
			switch (boxSize)
			{
			case ProbeBoxSize.Tiny:
				return new Size(200, 100);
			case ProbeBoxSize.Small:
				return new Size(300, 150);
			case ProbeBoxSize.Large:
				return new Size(500, 250);
			case ProbeBoxSize.ExtraLarge:
				return new Size(700, 350);
			}
			return new Size(400, 200);
		}

		/// <summary>
		/// Graph box rectangle for a probe, shrunk (keeping its aspect ratio) so it fits into half the canvas width
		/// and the full canvas height, and clamped to stay inside the canvas.
		/// </summary>
		/// <param name="probe">Settings providing the box size and the wanted box position.</param>
		/// <param name="w">Canvas width in pixels.</param>
		/// <param name="h">Canvas height in pixels.</param>
		/// <returns>The box in canvas pixel coordinates, never smaller than 1 x 1.</returns>
		public static Rectangle BoxInCanvas(ProbeSettings probe, int w, int h)
		{
			Size nominal = ProbeGraphPainter.NominalBoxSize(probe.BoxSize);
			double scale = 1.0;
			if (nominal.Width > w / 2.0)
			{
				scale = Math.Min(scale, w / 2.0 / nominal.Width);
			}
			if (nominal.Height > h)
			{
				scale = Math.Min(scale, (double)h / nominal.Height);
			}
			int boxWidth = Math.Max(1, (int)Math.Round(nominal.Width * scale));
			int boxHeight = Math.Max(1, (int)Math.Round(nominal.Height * scale));
			int boxX = Math.Max(0, Math.Min(w - boxWidth, probe.BoxX));
			int boxY = Math.Max(0, Math.Min(h - boxHeight, probe.BoxY));
			return new Rectangle(boxX, boxY, boxWidth, boxHeight);
		}

		/// <summary>
		/// Computes the axis ranges of a probe graph. For series graphs x is the step number (sample i sits at
		/// x = i + 1), spanning either the whole run or a running window; for line profiles x is the distance along
		/// the line in canvas pixels. y comes from the probe's fixed YMin/YMax (falling back to -1..1 when the
		/// range is empty or inverted) or, with AutoScaleY, from the visible data plus a small margin.
		/// Also used by the form to freeze auto-scaled y limits into the probe settings.
		/// </summary>
		/// <param name="probe">Settings that select kind, scale mode, fixed y range and auto-scaling.</param>
		/// <param name="trace">Recorded data; may be null, which yields the ranges of an empty graph.</param>
		/// <param name="totalSteps">Total steps of the run; upper x limit of full-scale series graphs without AutoScaleX.</param>
		/// <param name="xMin">Receives the lower x limit.</param>
		/// <param name="xMax">Receives the upper x limit, always greater than <paramref name="xMin"/>.</param>
		/// <param name="yMin">Receives the lower y limit.</param>
		/// <param name="yMax">Receives the upper y limit, always greater than <paramref name="yMin"/>.</param>
		public static void ComputeRanges(ProbeSettings probe, ProbeTrace trace, int totalSteps, ref double xMin, ref double xMax, ref double yMin, ref double yMax)
		{
			if (probe.Kind == ProbeKind.Line)
			{
				ProbeGraphPainter.ProfileRanges(probe, trace, ref xMin, ref xMax, ref yMin, ref yMax);
			}
			else
			{
				ProbeSeries series = (trace != null) ? trace.Series : null;
				double startStep = probe.UseStartAfterStep ? Math.Max(0, probe.StartAfterStep) : 0.0;
				ProbeGraphPainter.SeriesRanges(probe, series, totalSteps, startStep, ref xMin, ref xMax, ref yMin, ref yMax);
			}
		}

		/// <summary>
		/// Ranges of a time-series graph. Full mode spans from <paramref name="startStep"/> to the sample count
		/// (AutoScaleX) or to the run length. Running mode shows a window of <c>RunningWindow</c> (at least 10)
		/// steps that slides with the data; with AutoScaleX it starts narrow and grows to full width, otherwise it
		/// is full width from the start. With AutoScaleY the y range is fitted to the samples inside the x range.
		/// </summary>
		/// <param name="probe">Settings that select scale mode, running window, fixed y range and auto-scaling.</param>
		/// <param name="series">Sampled values; null is treated as empty.</param>
		/// <param name="totalSteps">Total steps of the run.</param>
		/// <param name="startStep">First step shown: the probe's start-after step, or 0.</param>
		/// <param name="xMin">Receives the lower x limit.</param>
		/// <param name="xMax">Receives the upper x limit.</param>
		/// <param name="yMin">Receives the lower y limit.</param>
		/// <param name="yMax">Receives the upper y limit.</param>
		private static void SeriesRanges(ProbeSettings probe, ProbeSeries series, int totalSteps, double startStep, ref double xMin, ref double xMax, ref double yMin, ref double yMax)
		{
			int count = (series == null) ? 0 : series.Count;
			double sampleCount = count;
			if (probe.ScaleMode == ProbeScaleMode.Full)
			{
				xMin = startStep;
				xMax = probe.AutoScaleX ? Math.Max(sampleCount, startStep + 1.0) : Math.Max(totalSteps, startStep + 1.0);
			}
			else
			{
				double window = Math.Max(10, probe.RunningWindow);
				if (probe.AutoScaleX)
				{
					xMax = Math.Max(sampleCount, startStep + 1.0);
					xMin = Math.Max(startStep, xMax - window);
				}
				else
				{
					xMax = Math.Max(sampleCount, startStep + window);
					xMin = Math.Max(startStep, xMax - window);
				}
			}
			if (xMax <= xMin)
			{
				xMax = xMin + 1.0;
			}
			yMin = probe.YMin;
			yMax = probe.YMax;
			if (yMax <= yMin)
			{
				yMin = -1.0;
				yMax = 1.0;
			}
			if (probe.AutoScaleY && count > 0)
			{
				int firstIndex = Math.Max(0, (int)Math.Floor(xMin) - 1);
				int lastIndex = Math.Min(count - 1, (int)Math.Ceiling(xMax));
				double lo = double.MaxValue;
				double hi = double.MinValue;
				float[] values = series.Values;
				for (int i = firstIndex; i <= lastIndex; i++)
				{
					float value = values[i];
					if (!float.IsNaN(value) && !float.IsInfinity(value))
					{
						if (value < lo)
						{
							lo = value;
						}
						if (value > hi)
						{
							hi = value;
						}
					}
				}
				if (lo <= hi)
				{
					ProbeGraphPainter.FitRange(lo, hi, false, ref yMin, ref yMax);
				}
			}
		}

		/// <summary>
		/// Ranges of a line-profile graph: x spans 0..line length in canvas pixels (0..1 without a trace); y is the
		/// fixed range or, with AutoScaleY, fitted to the profile values. Intensity profiles that are entirely
		/// non-negative are fitted from 0 upwards.
		/// </summary>
		/// <param name="probe">Settings providing the fixed y range, auto-scaling and measurement.</param>
		/// <param name="trace">Recorded data; null yields an empty graph.</param>
		/// <param name="xMin">Receives the lower x limit (always 0).</param>
		/// <param name="xMax">Receives the upper x limit.</param>
		/// <param name="yMin">Receives the lower y limit.</param>
		/// <param name="yMax">Receives the upper y limit.</param>
		private static void ProfileRanges(ProbeSettings probe, ProbeTrace trace, ref double xMin, ref double xMax, ref double yMin, ref double yMax)
		{
			double profileLength = 0.0;
			float[] profile = null;
			if (trace != null)
			{
				profileLength = trace.ProfileLength;
				profile = trace.Profile;
			}
			xMin = 0.0;
			xMax = (profileLength > 0.0) ? profileLength : 1.0;
			yMin = probe.YMin;
			yMax = probe.YMax;
			if (yMax <= yMin)
			{
				yMin = -1.0;
				yMax = 1.0;
			}
			if (probe.AutoScaleY && profile != null)
			{
				double lo = double.MaxValue;
				double hi = double.MinValue;
				for (int i = 0; i < profile.Length; i++)
				{
					float value = profile[i];
					if (!float.IsNaN(value) && !float.IsInfinity(value))
					{
						if (value < lo)
						{
							lo = value;
						}
						if (value > hi)
						{
							hi = value;
						}
					}
				}
				if (lo <= hi)
				{
					ProbeGraphPainter.FitRange(lo, hi, probe.Measurement != ProbeMeasurement.Amplitude && lo >= 0.0, ref yMin, ref yMax);
				}
			}
		}

		/// <summary>
		/// Expands [lo, hi] by an 8% margin so the curve does not touch the plot border. A (nearly) flat range
		/// gets a margin of 10% of its magnitude (at least 1e-6) instead, so it never collapses to zero height.
		/// </summary>
		/// <param name="lo">Smallest data value.</param>
		/// <param name="hi">Largest data value, not below <paramref name="lo"/>.</param>
		/// <param name="zeroBased">When true the lower limit is pinned to 0 instead of lo minus the margin.</param>
		/// <param name="yMin">Receives the lower y limit.</param>
		/// <param name="yMax">Receives the upper y limit.</param>
		private static void FitRange(double lo, double hi, bool zeroBased, ref double yMin, ref double yMax)
		{
			double span = hi - lo;
			double magnitude = Math.Max(Math.Abs(hi), Math.Abs(lo));
			double margin = span * 0.08;
			if (span < magnitude * 1E-09 || span < 1E-30)
			{
				margin = Math.Max(magnitude * 0.1, 1E-06);
			}
			yMin = zeroBased ? 0.0 : (lo - margin);
			yMax = hi + margin;
		}

		/// <summary>
		/// Returns "nice" (1/2/5 x 10^n) tick positions covering [lo, hi], at most 40 of them. A tick within
		/// rounding distance of 0 is snapped to exactly 0 so the zero line can be recognised.
		/// </summary>
		/// <param name="lo">Lower end of the axis range.</param>
		/// <param name="hi">Upper end of the axis range.</param>
		/// <param name="target">Approximate number of ticks wanted.</param>
		/// <returns>Ascending tick values; empty when the range is empty, inverted or infinite.</returns>
		private static List<double> NiceTicks(double lo, double hi, int target)
		{
			List<double> ticks = new List<double>();
			double span = hi - lo;
			if (span <= 0.0 || double.IsInfinity(span))
			{
				return ticks;
			}
			double rawStep = span / Math.Max(1, target);
			double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(rawStep)));
			double mantissa = rawStep / magnitude;
			double step;
			if (mantissa < 1.5)
			{
				step = 1.0;
			}
			else if (mantissa < 3.0)
			{
				step = 2.0;
			}
			else if (mantissa < 7.0)
			{
				step = 5.0;
			}
			else
			{
				step = 10.0;
			}
			step *= magnitude;
			double tick = Math.Ceiling(lo / step) * step;
			int tickCount = 0;
			while (tick <= hi + step * 1E-09 && tickCount < 40)
			{
				ticks.Add((Math.Abs(tick) < step * 1E-09) ? 0.0 : tick);
				tick += step;
				tickCount++;
			}
			return ticks;
		}

		/// <summary>Formats an axis value with three significant digits (invariant culture); 0 is just "0".</summary>
		private static string FormatTick(double v)
		{
			return (v == 0.0) ? "0" : v.ToString("G3", CultureInfo.InvariantCulture);
		}

		/// <summary>Formats an x-axis position: as an integer when the visible <paramref name="span"/> is at least 10 units, otherwise with one decimal.</summary>
		private static string FormatPosition(double v, double span)
		{
			return (span >= 10.0) ? Math.Round(v).ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.#", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Draws one complete probe graph box (background, frame, axes, curve and title bar with live readout)
		/// into <paramref name="bounds"/>. Nothing is drawn when the box is smaller than 40 x 30 or the plot area
		/// would be smaller than 20 x 15. A line probe with "start after step" shows neither profile nor readout
		/// until <see cref="ProbeTrace.StepsSampled"/> has reached that step; series are always drawn. The
		/// Graphics state is saved and restored around the call.
		/// </summary>
		/// <param name="g">Target surface. <paramref name="bounds"/> is in its device pixels: picture-box pixels when painting the form, bitmap pixels for exported frames.</param>
		/// <param name="bounds">Outer rectangle of the graph box.</param>
		/// <param name="title">Title bar text, trimmed with an ellipsis if it does not fit next to the readout.</param>
		/// <param name="probe">Settings that select kind, scaling and the start-after gate.</param>
		/// <param name="trace">Recorded data, or null for an empty graph.</param>
		/// <param name="totalSteps">Total steps of the run, see <see cref="ComputeRanges"/>.</param>
		public static void DrawGraph(Graphics g, Rectangle bounds, string title, ProbeSettings probe, ProbeTrace trace, int totalSteps)
		{
			if (bounds.Width < 40 || bounds.Height < 30)
			{
				return;
			}
			GraphicsState savedState = g.Save();
			try
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(235, 14, 16, 22)))
				{
					g.FillRectangle(backgroundBrush, bounds);
				}
				using (Pen borderPen = new Pen(Color.FromArgb(170, 200, 200, 210), 1f))
				{
					g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
				}
				// Font and margins scale with the box height so small boxes stay legible.
				float fontPx = Math.Max(9f, bounds.Height / 14f);
				using (Font font = new Font("Segoe UI", fontPx, FontStyle.Regular, GraphicsUnit.Pixel))
				{
					int leftMargin = (int)Math.Ceiling(fontPx * 4.4f);
					int topMargin = (int)Math.Round(fontPx * 2.3f);
					int bottomMargin = (int)Math.Round(fontPx * 1.9f);
					Rectangle plot = new Rectangle(bounds.X + leftMargin + 6, bounds.Y + topMargin, bounds.Width - leftMargin - 6 - 10, bounds.Height - topMargin - bottomMargin);
					if (plot.Width < 20 || plot.Height < 15)
					{
						return;
					}
					bool isLine = probe.Kind == ProbeKind.Line;
					ProbeTrace gatedTrace = (!probe.UseStartAfterStep || trace == null || trace.StepsSampled >= probe.StartAfterStep) ? trace : null;
					double xMin = 0.0;
					double xMax = 0.0;
					double yMin = 0.0;
					double yMax = 0.0;
					ProbeGraphPainter.ComputeRanges(probe, isLine ? gatedTrace : trace, totalSteps, ref xMin, ref xMax, ref yMin, ref yMax);
					ProbeGraphPainter.DrawAxes(g, font, fontPx, bounds, plot, xMin, xMax, yMin, yMax);
					float lineWidth = Math.Max(1.2f, bounds.Height / 110f);
					if (isLine)
					{
						if (gatedTrace != null)
						{
							ProbeGraphPainter.DrawProfile(g, plot, gatedTrace.Profile, yMin, yMax, lineWidth);
						}
					}
					else if (trace != null)
					{
						ProbeGraphPainter.DrawCurve(g, plot, trace.Series, xMin, xMax, yMin, yMax, lineWidth);
					}
					ProbeGraphPainter.DrawTitle(g, font, bounds, title, ProbeGraphPainter.ValueText(isLine, isLine ? gatedTrace : trace), fontPx);
				}
			}
			finally
			{
				g.Restore(savedState);
			}
		}

		/// <summary>Short readout shown in the title bar: "max" of the profile or "=" latest series value; empty when there is no valid value.</summary>
		private static string ValueText(bool isLine, ProbeTrace trace)
		{
			if (trace == null)
			{
				return "";
			}
			if (isLine)
			{
				float[] profile = trace.Profile;
				if (profile == null || profile.Length == 0)
				{
					return "";
				}
				float max = float.MinValue;
				for (int i = 0; i < profile.Length; i++)
				{
					float value = profile[i];
					if (!float.IsNaN(value) && !float.IsInfinity(value) && value > max)
					{
						max = value;
					}
				}
				if (max == float.MinValue)
				{
					return "";
				}
				return "max " + ProbeGraphPainter.FormatTick(max);
			}
			if (trace.Series.Count == 0)
			{
				return "";
			}
			float lastValue = trace.Series.LastValue;
			if (float.IsNaN(lastValue) || float.IsInfinity(lastValue))
			{
				return "";
			}
			return "= " + ProbeGraphPainter.FormatTick(lastValue);
		}

		/// <summary>
		/// Draws grid lines and labels at the "nice" ticks of both axes (the y = 0 line brighter than the rest),
		/// then the plot frame. y labels are right-aligned in the left margin, x labels centred below the plot.
		/// </summary>
		/// <param name="g">Target surface.</param>
		/// <param name="font">Label font.</param>
		/// <param name="fontPx">Font height in pixels, used to size the label rectangles.</param>
		/// <param name="bounds">Whole graph box.</param>
		/// <param name="plot">Plot area inside the box.</param>
		/// <param name="xMin">Lower x limit.</param>
		/// <param name="xMax">Upper x limit.</param>
		/// <param name="yMin">Lower y limit.</param>
		/// <param name="yMax">Upper y limit.</param>
		private static void DrawAxes(Graphics g, Font font, float fontPx, Rectangle bounds, Rectangle plot, double xMin, double xMax, double yMin, double yMax)
		{
			using (Pen gridPen = new Pen(Color.FromArgb(46, 255, 255, 255), 1f))
			{
				using (Pen zeroPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
				{
					using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(225, 225, 225, 230)))
					{
						using (StringFormat yLabelFormat = new StringFormat())
						{
							using (StringFormat xLabelFormat = new StringFormat())
							{
								yLabelFormat.Alignment = StringAlignment.Far;
								yLabelFormat.LineAlignment = StringAlignment.Center;
								yLabelFormat.FormatFlags = StringFormatFlags.NoWrap;
								xLabelFormat.Alignment = StringAlignment.Center;
								xLabelFormat.FormatFlags = StringFormatFlags.NoWrap;
								foreach (double tick in ProbeGraphPainter.NiceTicks(yMin, yMax, 4))
								{
									float py = ProbeGraphPainter.ToPixelY(tick, plot, yMin, yMax);
									g.DrawLine((tick == 0.0) ? zeroPen : gridPen, (float)plot.Left, py, (float)plot.Right, py);
									g.DrawString(ProbeGraphPainter.FormatTick(tick), font, labelBrush, new RectangleF(bounds.X + 1, py - fontPx * 0.7f, plot.Left - bounds.X - 4, fontPx * 1.4f), yLabelFormat);
								}
								foreach (double tick in ProbeGraphPainter.NiceTicks(xMin, xMax, 5))
								{
									float px = (float)(plot.Left + (tick - xMin) / (xMax - xMin) * plot.Width);
									g.DrawLine(gridPen, px, (float)plot.Top, px, (float)plot.Bottom);
									g.DrawString(ProbeGraphPainter.FormatPosition(tick, xMax - xMin), font, labelBrush, new RectangleF(px - fontPx * 2.5f, plot.Bottom + 2, fontPx * 5f, fontPx * 1.4f), xLabelFormat);
								}
							}
						}
					}
				}
				using (Pen framePen = new Pen(Color.FromArgb(130, 200, 200, 210), 1f))
				{
					g.DrawRectangle(framePen, plot.X, plot.Y, plot.Width, plot.Height);
				}
			}
		}

		/// <summary>
		/// Maps a data value to a pixel y inside <paramref name="plot"/> (bottom edge = yMin, top edge = yMax).
		/// The result is clamped to +/-30000 because GDI+ fails on coordinates far outside the surface.
		/// </summary>
		private static float ToPixelY(double v, Rectangle plot, double yMin, double yMax)
		{
			double py = plot.Bottom - (v - yMin) / (yMax - yMin) * plot.Height;
			if (py < -30000.0)
			{
				py = -30000.0;
			}
			if (py > 30000.0)
			{
				py = 30000.0;
			}
			return (float)py;
		}

		/// <summary>
		/// Draws a time series: sample i is placed at x = i + 1 (its step number); NaN and infinite samples are
		/// skipped. When there are more than two samples per pixel column, each column gets a vertical min/max
		/// segment instead of individual points.
		/// </summary>
		/// <param name="g">Target surface.</param>
		/// <param name="plot">Plot area in device pixels.</param>
		/// <param name="series">Sampled values; null or empty draws nothing.</param>
		/// <param name="xMin">Lower x limit in steps.</param>
		/// <param name="xMax">Upper x limit in steps.</param>
		/// <param name="yMin">Lower y limit.</param>
		/// <param name="yMax">Upper y limit.</param>
		/// <param name="lineWidth">Pen width in pixels.</param>
		private static void DrawCurve(Graphics g, Rectangle plot, ProbeSeries series, double xMin, double xMax, double yMin, double yMax, float lineWidth)
		{
			if (series == null || series.Count < 1)
			{
				return;
			}
			float[] values = series.Values;
			int count = series.Count;
			int firstIndex = Math.Max(0, (int)Math.Floor(xMin) - 1);
			int lastIndex = Math.Min(count - 1, (int)Math.Ceiling(xMax));
			if (lastIndex < firstIndex)
			{
				return;
			}
			List<PointF> points = new List<PointF>();
			int visibleCount = lastIndex - firstIndex + 1;
			double xScale = plot.Width / (xMax - xMin);
			if (visibleCount <= plot.Width * 2)
			{
				for (int i = firstIndex; i <= lastIndex; i++)
				{
					float value = values[i];
					if (!float.IsNaN(value) && !float.IsInfinity(value))
					{
						points.Add(new PointF((float)(plot.Left + (i + 1 - xMin) * xScale), ProbeGraphPainter.ToPixelY(value, plot, yMin, yMax)));
					}
				}
			}
			else
			{
				for (int column = 0; column < plot.Width; column++)
				{
					// long arithmetic: column * visibleCount can exceed the int range for long runs
					int binStart = firstIndex + (int)((long)column * (long)visibleCount / (long)plot.Width);
					int binEnd = firstIndex + (int)((long)(column + 1) * (long)visibleCount / (long)plot.Width) - 1;
					if (binEnd < binStart)
					{
						binEnd = binStart;
					}
					float binMin = float.MaxValue;
					float binMax = float.MinValue;
					int binLast = Math.Min(binEnd, lastIndex);
					for (int k = binStart; k <= binLast; k++)
					{
						float value = values[k];
						if (!float.IsNaN(value) && !float.IsInfinity(value))
						{
							if (value < binMin)
							{
								binMin = value;
							}
							if (value > binMax)
							{
								binMax = value;
							}
						}
					}
					if (binMin <= binMax)
					{
						float px = plot.Left + column + 0.5f;
						points.Add(new PointF(px, ProbeGraphPainter.ToPixelY(binMin, plot, yMin, yMax)));
						points.Add(new PointF(px, ProbeGraphPainter.ToPixelY(binMax, plot, yMin, yMax)));
					}
				}
			}
			ProbeGraphPainter.StrokePoints(g, plot, points, lineWidth);
		}

		/// <summary>
		/// Draws a line-probe profile stretched over the full plot width (first value at the left edge, last value
		/// at the right edge), min/max binned per pixel column when dense. NaN and infinite values are skipped.
		/// </summary>
		/// <param name="g">Target surface.</param>
		/// <param name="plot">Plot area in device pixels.</param>
		/// <param name="profile">Values along the line; null or empty draws nothing.</param>
		/// <param name="yMin">Lower y limit.</param>
		/// <param name="yMax">Upper y limit.</param>
		/// <param name="lineWidth">Pen width in pixels.</param>
		private static void DrawProfile(Graphics g, Rectangle plot, float[] profile, double yMin, double yMax, float lineWidth)
		{
			if (profile == null || profile.Length < 1)
			{
				return;
			}
			int count = profile.Length;
			List<PointF> points = new List<PointF>();
			if (count <= plot.Width * 2)
			{
				for (int i = 0; i < count; i++)
				{
					float value = profile[i];
					if (!float.IsNaN(value) && !float.IsInfinity(value))
					{
						double t = (count > 1) ? (i / (count - 1.0)) : 0.0;
						points.Add(new PointF((float)(plot.Left + t * plot.Width), ProbeGraphPainter.ToPixelY(value, plot, yMin, yMax)));
					}
				}
			}
			else
			{
				for (int column = 0; column < plot.Width; column++)
				{
					int binStart = (int)((long)column * (long)count / (long)plot.Width);
					int binEnd = (int)((long)(column + 1) * (long)count / (long)plot.Width) - 1;
					if (binEnd < binStart)
					{
						binEnd = binStart;
					}
					float binMin = float.MaxValue;
					float binMax = float.MinValue;
					int binLast = Math.Min(binEnd, count - 1);
					for (int k = binStart; k <= binLast; k++)
					{
						float value = profile[k];
						if (!float.IsNaN(value) && !float.IsInfinity(value))
						{
							if (value < binMin)
							{
								binMin = value;
							}
							if (value > binMax)
							{
								binMax = value;
							}
						}
					}
					if (binMin <= binMax)
					{
						float px = plot.Left + column + 0.5f;
						points.Add(new PointF(px, ProbeGraphPainter.ToPixelY(binMin, plot, yMin, yMax)));
						points.Add(new PointF(px, ProbeGraphPainter.ToPixelY(binMax, plot, yMin, yMax)));
					}
				}
			}
			ProbeGraphPainter.StrokePoints(g, plot, points, lineWidth);
		}

		/// <summary>
		/// Strokes the points as one polyline clipped to the plot area; a single point is drawn as a small dot,
		/// an empty list draws nothing. The previous clip region is restored afterwards.
		/// </summary>
		private static void StrokePoints(Graphics g, Rectangle plot, List<PointF> points, float lineWidth)
		{
			if (points.Count < 2)
			{
				if (points.Count == 1)
				{
					using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(255, 255, 208, 64)))
					{
						g.FillEllipse(dotBrush, points[0].X - 2f, points[0].Y - 2f, 4f, 4f);
					}
				}
				return;
			}
			// g.Clip returns a copy that we own and must dispose.
			Region savedClip = g.Clip;
			try
			{
				g.SetClip(plot);
				using (Pen curvePen = new Pen(Color.FromArgb(255, 255, 208, 64), lineWidth))
				{
					curvePen.LineJoin = LineJoin.Round;
					g.DrawLines(curvePen, points.ToArray());
				}
			}
			finally
			{
				g.Clip = savedClip;
				savedClip.Dispose();
			}
		}

		/// <summary>
		/// Draws the title strip of the box: the readout right-aligned in its own colour, then the title
		/// left-aligned in the remaining width (ellipsis-trimmed), so the readout always stays visible.
		/// </summary>
		/// <param name="g">Target surface.</param>
		/// <param name="font">Title font.</param>
		/// <param name="bounds">Outer rectangle of the graph box.</param>
		/// <param name="title">Title text.</param>
		/// <param name="valueText">Readout from <see cref="ValueText"/>; empty draws no readout.</param>
		/// <param name="fontPx">Font height in pixels, used to size the text rectangles.</param>
		private static void DrawTitle(Graphics g, Font font, Rectangle bounds, string title, string valueText, float fontPx)
		{
			using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(240, 240, 240, 245)))
			{
				using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(255, 255, 208, 64)))
				{
					using (StringFormat valueFormat = new StringFormat())
					{
						using (StringFormat titleFormat = new StringFormat())
						{
							valueFormat.Alignment = StringAlignment.Far;
							valueFormat.FormatFlags = StringFormatFlags.NoWrap;
							titleFormat.Alignment = StringAlignment.Near;
							titleFormat.Trimming = StringTrimming.EllipsisCharacter;
							titleFormat.FormatFlags = StringFormatFlags.NoWrap;
							float valueWidth = 0f;
							if (valueText.Length > 0)
							{
								valueWidth = g.MeasureString(valueText, font).Width;
								g.DrawString(valueText, font, valueBrush, new RectangleF(bounds.Right - valueWidth - 8f, bounds.Y + 3, valueWidth + 4f, fontPx * 1.4f), valueFormat);
							}
							g.DrawString(title, font, titleBrush, new RectangleF(bounds.X + 6, bounds.Y + 3, Math.Max(10f, bounds.Width - valueWidth - 16f), fontPx * 1.4f), titleFormat);
						}
					}
				}
			}
		}

		/// <summary>
		/// Draws the probe position cross(es), the connecting line (line probes) and the index label onto a
		/// scaled canvas view, as used by the form's paint handlers where the canvas is shown zoomed in a picture
		/// box. Canvas pixel (x, y) maps to device position origin + (x + 0.5) * scale, i.e. the marker sits on
		/// the pixel centre. Point probes are labelled with the 1-based index, line probes with "nA" and "nB".
		/// </summary>
		/// <param name="g">Target surface.</param>
		/// <param name="probe">Probe whose positions (canvas pixels) and kind are drawn.</param>
		/// <param name="index">Zero-based probe index; shown as index + 1.</param>
		/// <param name="originX">Device x of the canvas origin.</param>
		/// <param name="originY">Device y of the canvas origin.</param>
		/// <param name="scaleX">Device pixels per canvas pixel, horizontally.</param>
		/// <param name="scaleY">Device pixels per canvas pixel, vertically.</param>
		public static void DrawMarkers(Graphics g, ProbeSettings probe, int index, float originX, float originY, float scaleX, float scaleY)
		{
			float ax = originX + (probe.PosAX + 0.5f) * scaleX;
			float ay = originY + (probe.PosAY + 0.5f) * scaleY;
			float bx = originX + (probe.PosBX + 0.5f) * scaleX;
			float by = originY + (probe.PosBY + 0.5f) * scaleY;
			bool isLine = probe.Kind == ProbeKind.Line;
			string label = (index + 1).ToString(CultureInfo.InvariantCulture);
			GraphicsState savedState = g.Save();
			try
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				using (Pen darkPen = new Pen(Color.FromArgb(210, 0, 0, 0), 3.5f))
				{
					using (Pen lightPen = new Pen(Color.White, 1.5f))
					{
						if (isLine)
						{
							g.DrawLine(darkPen, ax, ay, bx, by);
							g.DrawLine(lightPen, ax, ay, bx, by);
						}
						ProbeGraphPainter.DrawCross(g, darkPen, lightPen, ax, ay);
						if (isLine)
						{
							ProbeGraphPainter.DrawCross(g, darkPen, lightPen, bx, by);
						}
					}
				}
				using (Font font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
				{
					using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
					{
						using (SolidBrush textBrush = new SolidBrush(Color.White))
						{
							if (isLine)
							{
								ProbeGraphPainter.DrawNumber(g, font, shadowBrush, textBrush, label + "A", ax, ay);
								ProbeGraphPainter.DrawNumber(g, font, shadowBrush, textBrush, label + "B", bx, by);
							}
							else
							{
								ProbeGraphPainter.DrawNumber(g, font, shadowBrush, textBrush, label, ax, ay);
							}
						}
					}
				}
			}
			finally
			{
				g.Restore(savedState);
			}
		}

		/// <summary>Draws a plus-shaped cross with 7-pixel arms centred on (x, y): the wide dark pen first as outline, then the thin light pen on top.</summary>
		private static void DrawCross(Graphics g, Pen dark, Pen light, float x, float y)
		{
			g.DrawLine(dark, x - 7f, y, x + 7f, y);
			g.DrawLine(dark, x, y - 7f, x, y + 7f);
			g.DrawLine(light, x - 7f, y, x + 7f, y);
			g.DrawLine(light, x, y - 7f, x, y + 7f);
		}

		/// <summary>Draws a marker label above and to the right of (x, y) with a 1-pixel dark drop shadow.</summary>
		private static void DrawNumber(Graphics g, Font font, Brush shadow, Brush textBrush, string text, float x, float y)
		{
			g.DrawString(text, font, shadow, x + 9f, y - 18f);
			g.DrawString(text, font, textBrush, x + 8f, y - 19f);
		}
	}
}

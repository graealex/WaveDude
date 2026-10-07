using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// HSV colour picker used by the colour-scheme tab (created in code by <see cref="Form1"/> and
	/// docked into a panel): a saturation/value square, a vertical hue strip, "New" / "Old" swatches
	/// (clicking "Old" restores <see cref="OriginalColor"/>), a hex text box, an RGB read-out and a
	/// "More..." button that opens the standard <see cref="ColorDialog"/>. The layout is fixed for a
	/// 423 x 411 client area. <see cref="ColorChanged"/> is raised for user edits only, not when
	/// <see cref="SelectedColor"/> is assigned from code. UI thread only.
	/// </summary>
	[DesignerCategory("Code")]
	public class ColorPickerControl : UserControl
	{
		/// <summary>
		/// Raised after the user changes the colour (dragging, a complete hex entry, the dialog or a click
		/// on the "Old" swatch). Not raised when <see cref="SelectedColor"/> or <see cref="OriginalColor"/> is set from code.
		/// </summary>
		public event EventHandler ColorChanged;

		/// <summary>
		/// Creates the child controls at fixed positions, enables double-buffered owner painting and
		/// starts with red selected and grey as the original colour.
		/// </summary>
		public ColorPickerControl()
		{
			this._svRect = new Rectangle(10, 10, 300, 300);
			this._hueRect = new Rectangle(326, 10, 28, 300);
			this._newRect = new Rectangle(10, 324, 150, 36);
			this._oldRect = new Rectangle(160, 324, 150, 36);
			this._lblHex = new Label();
			this._txtHex = new TextBox();
			this._lblRgb = new Label();
			this._btnDialog = new Button();
			this._color = Color.Red;
			this._original = Color.Gray;
			this._svHue = -1.0;
			base.SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			base.Size = new Size(423, 411);
			this._lblHex.Text = "Hex";
			this._lblHex.AutoSize = true;
			this._lblHex.Location = new Point(10, 377);
			this._txtHex.Location = new Point(48, 373);
			this._txtHex.Width = 90;
			this._txtHex.MaxLength = 7;
			this._lblRgb.AutoSize = true;
			this._lblRgb.Location = new Point(152, 377);
			this._btnDialog.Text = "More...";
			this._btnDialog.Location = new Point(326, 324);
			this._btnDialog.Size = new Size(90, 36);
			base.Controls.Add(this._lblHex);
			base.Controls.Add(this._txtHex);
			base.Controls.Add(this._lblRgb);
			base.Controls.Add(this._btnDialog);
			this._txtHex.TextChanged += this.HexTextChanged;
			this._btnDialog.Click += this.DialogButtonClick;
			this.ApplyColor(Color.Red, false, false);
		}

		/// <summary>
		/// Gets the current colour (always opaque). Setting it updates the HSV state, texts and swatch
		/// without raising <see cref="ColorChanged"/>.
		/// </summary>
		public Color SelectedColor
		{
			get
			{
				return this._color;
			}
			set
			{
				this.ApplyColor(value, false, false);
			}
		}

		/// <summary>The reference colour shown in the "Old" swatch and restored when it is clicked; alpha is discarded.</summary>
		public Color OriginalColor
		{
			get
			{
				return this._original;
			}
			set
			{
				this._original = Color.FromArgb(value.R, value.G, value.B);
				base.Invalidate();
			}
		}

		/// <summary>
		/// Makes <paramref name="c"/> (alpha discarded) the current colour and derives H/S/V from it. The
		/// hue is left unchanged for greys and black (saturation or value 0) so the hue marker does not
		/// jump to 0. Updates the texts, repaints and optionally raises <see cref="ColorChanged"/>.
		/// </summary>
		/// <param name="c">The new colour; its alpha is ignored.</param>
		/// <param name="raiseChanged">Raise <see cref="ColorChanged"/> (true for user edits only).</param>
		/// <param name="fromHexBox">Leave the hex box untouched because the user is typing in it.</param>
		private void ApplyColor(Color c, bool raiseChanged, bool fromHexBox)
		{
			this._color = Color.FromArgb(c.R, c.G, c.B);
			double hue = 0.0;
			double saturation = 0.0;
			double value = 0.0;
			ColorPickerControl.RgbToHsv(this._color, ref hue, ref saturation, ref value);
			if (saturation > 0.0 && value > 0.0)
			{
				this._h = hue;
			}
			this._s = saturation;
			this._v = value;
			this.UpdateTexts(fromHexBox);
			base.Invalidate();
			if (raiseChanged)
			{
				EventHandler colorChangedEvent = this.ColorChanged;
				if (colorChangedEvent != null)
				{
					colorChangedEvent(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Recomputes the colour from <see cref="_h"/>/<see cref="_s"/>/<see cref="_v"/> after a drag, updates the texts, repaints and raises <see cref="ColorChanged"/>.</summary>
		private void SyncFromHsv()
		{
			this._color = ColorPickerControl.HsvToColor(this._h, this._s, this._v);
			this.UpdateTexts(false);
			base.Invalidate();
			EventHandler colorChangedEvent = this.ColorChanged;
			if (colorChangedEvent != null)
			{
				colorChangedEvent(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Writes "#RRGGBB" into the hex box (unless <paramref name="skipHexBox"/>) and "R G B" into the
		/// read-out label, with <see cref="_updatingText"/> set so <see cref="HexTextChanged"/> ignores the programmatic change.
		/// </summary>
		private void UpdateTexts(bool skipHexBox)
		{
			this._updatingText = true;
			try
			{
				if (!skipHexBox)
				{
					this._txtHex.Text = "#" + this._color.R.ToString("X2", CultureInfo.InvariantCulture) + this._color.G.ToString("X2", CultureInfo.InvariantCulture) + this._color.B.ToString("X2", CultureInfo.InvariantCulture);
				}
				this._lblRgb.Text = string.Format(CultureInfo.InvariantCulture, "R {0}   G {1}   B {2}", this._color.R, this._color.G, this._color.B);
			}
			finally
			{
				this._updatingText = false;
			}
		}

		/// <summary>Typing a complete 6-digit hex colour (optional leading '#') applies it and raises <see cref="ColorChanged"/>; incomplete or invalid text is ignored.</summary>
		private void HexTextChanged(object sender, EventArgs e)
		{
			if (this._updatingText)
			{
				return;
			}
			string hex = this._txtHex.Text.Trim().TrimStart('#');
			if (hex.Length != 6)
			{
				return;
			}
			int rgb;
			if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
			{
				this.ApplyColor(Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255), true, true);
			}
		}

		/// <summary>"More..." opens the standard colour dialog (custom-colour panel expanded) preset to the current colour and applies the chosen colour.</summary>
		private void DialogButtonClick(object sender, EventArgs e)
		{
			using (ColorDialog colorDialog = new ColorDialog())
			{
				colorDialog.FullOpen = true;
				colorDialog.Color = this._color;
				if (colorDialog.ShowDialog(this) == DialogResult.OK)
				{
					this.ApplyColor(colorDialog.Color, true, false);
				}
			}
		}

		/// <summary>
		/// Left button: starts a drag in the S/V square (<see cref="_drag"/> = 1) or the hue strip
		/// (<see cref="_drag"/> = 2) and applies the pressed point immediately; a click on the "Old"
		/// swatch restores <see cref="OriginalColor"/>.
		/// </summary>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button != MouseButtons.Left)
			{
				return;
			}
			if (this._svRect.Contains(e.Location))
			{
				this._drag = 1;
				this.UpdateFromMouse(e.Location);
			}
			else if (this._hueRect.Contains(e.Location))
			{
				this._drag = 2;
				this.UpdateFromMouse(e.Location);
			}
			else if (this._oldRect.Contains(e.Location))
			{
				this.ApplyColor(this._original, true, false);
			}
		}

		/// <summary>Continues an active drag; positions outside the square / strip are clamped to its edge.</summary>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (this._drag != 0)
			{
				this.UpdateFromMouse(e.Location);
			}
		}

		/// <summary>Ends any drag.</summary>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			this._drag = 0;
		}

		/// <summary>
		/// Converts a mouse position in control client pixels into S/V (square: saturation 0..1 left to
		/// right, value 1..0 top to bottom) or hue (strip: 0..360 degrees top to bottom) depending on
		/// <see cref="_drag"/>, clamped to the rectangle, and then calls <see cref="SyncFromHsv"/>.
		/// </summary>
		private void UpdateFromMouse(Point p)
		{
			if (this._drag == 1)
			{
				this._s = ColorPickerControl.Clamp01((p.X - this._svRect.X) / (double)(this._svRect.Width - 1));
				this._v = 1.0 - ColorPickerControl.Clamp01((p.Y - this._svRect.Y) / (double)(this._svRect.Height - 1));
			}
			else if (this._drag == 2)
			{
				this._h = ColorPickerControl.Clamp01((p.Y - this._hueRect.Y) / (double)(this._hueRect.Height - 1)) * 360.0;
			}
			else
			{
				return;
			}
			this.SyncFromHsv();
		}

		/// <summary>
		/// Paints the S/V square (bilinear upscale of the <see cref="SvResolution"/> cache), the hue strip,
		/// the two markers, the swatches with contrasting captions and, when the control is disabled, a
		/// translucent veil in the background colour over everything.
		/// </summary>
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics g = e.Graphics;
			g.Clear(this.BackColor);
			this.EnsureSvBitmap();
			g.InterpolationMode = InterpolationMode.HighQualityBilinear;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			g.DrawImage(this._svBitmap, this._svRect);
			g.DrawRectangle(Pens.Black, this._svRect.X - 1, this._svRect.Y - 1, this._svRect.Width + 1, this._svRect.Height + 1);
			this.EnsureHueBitmap();
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.DrawImage(this._hueBitmap, this._hueRect);
			g.DrawRectangle(Pens.Black, this._hueRect.X - 1, this._hueRect.Y - 1, this._hueRect.Width + 1, this._hueRect.Height + 1);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float svX = (float)(this._svRect.X + this._s * (this._svRect.Width - 1));
			float svY = (float)(this._svRect.Y + (1.0 - this._v) * (this._svRect.Height - 1));
			g.DrawEllipse(Pens.Black, svX - 7f, svY - 7f, 14f, 14f);
			g.DrawEllipse(Pens.White, svX - 6f, svY - 6f, 12f, 12f);
			float hueY = (float)(this._hueRect.Y + this._h / 360.0 * (this._hueRect.Height - 1));
			g.DrawRectangle(Pens.Black, this._hueRect.X - 3, hueY - 3f, this._hueRect.Width + 5, 6f);
			g.DrawRectangle(Pens.White, this._hueRect.X - 2, hueY - 2f, this._hueRect.Width + 3, 4f);
			g.SmoothingMode = SmoothingMode.Default;
			using (SolidBrush newBrush = new SolidBrush(this._color))
			{
				g.FillRectangle(newBrush, this._newRect);
			}
			using (SolidBrush oldBrush = new SolidBrush(this._original))
			{
				g.FillRectangle(oldBrush, this._oldRect);
			}
			g.DrawRectangle(Pens.Black, this._newRect.X, this._newRect.Y, this._newRect.Width - 1, this._newRect.Height - 1);
			g.DrawRectangle(Pens.Black, this._oldRect.X, this._oldRect.Y, this._oldRect.Width - 1, this._oldRect.Height - 1);
			TextRenderer.DrawText(g, "New", this.Font, this._newRect, ColorPickerControl.ContrastColor(this._color), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			TextRenderer.DrawText(g, "Old (click to restore)", this.Font, this._oldRect, ColorPickerControl.ContrastColor(this._original), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			if (!base.Enabled)
			{
				using (SolidBrush overlayBrush = new SolidBrush(Color.FromArgb(150, this.BackColor)))
				{
					g.FillRectangle(overlayBrush, base.ClientRectangle);
				}
			}
		}

		/// <summary>Repaints so the disabled veil appears or disappears.</summary>
		protected override void OnEnabledChanged(EventArgs e)
		{
			base.OnEnabledChanged(e);
			base.Invalidate();
		}

		/// <summary>
		/// Rebuilds the <see cref="SvResolution"/>-square saturation/value bitmap for the current hue
		/// when it has never been built or the hue moved by 0.0001 degrees or more; otherwise keeps the
		/// cached bitmap. Pixels are written in one block via <c>LockBits</c>.
		/// </summary>
		private void EnsureSvBitmap()
		{
			if (this._svBitmap != null && Math.Abs(this._svHue - this._h) < 0.0001)
			{
				return;
			}
			if (this._svBitmap == null)
			{
				this._svBitmap = new Bitmap(SvResolution, SvResolution, PixelFormat.Format32bppArgb);
			}
			Color pure = ColorPickerControl.HsvToColor(this._h, 1.0, 1.0);
			double pureR = pure.R / 255.0;
			double pureG = pure.G / 255.0;
			double pureB = pure.B / 255.0;
			int[] pixels = new int[SvResolution * SvResolution];
			for (int y = 0; y < SvResolution; y++)
			{
				double value = 1.0 - y / (SvResolution - 1.0);
				for (int x = 0; x < SvResolution; x++)
				{
					double saturation = x / (SvResolution - 1.0);
					int r = (int)Math.Round(255.0 * value * (1.0 - saturation + saturation * pureR));
					int g = (int)Math.Round(255.0 * value * (1.0 - saturation + saturation * pureG));
					int b = (int)Math.Round(255.0 * value * (1.0 - saturation + saturation * pureB));
					pixels[y * SvResolution + x] = Color.FromArgb(255, r, g, b).ToArgb();
				}
			}
			BitmapData bitmapData = this._svBitmap.LockBits(new Rectangle(0, 0, SvResolution, SvResolution), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
			}
			finally
			{
				this._svBitmap.UnlockBits(bitmapData);
			}
			this._svHue = this._h;
		}

		/// <summary>Creates the 1-pixel-wide hue gradient bitmap (hue 0 at the top, 360 at the bottom) on first use; it never changes afterwards.</summary>
		private void EnsureHueBitmap()
		{
			if (this._hueBitmap != null)
			{
				return;
			}
			this._hueBitmap = new Bitmap(1, this._hueRect.Height, PixelFormat.Format32bppArgb);
			for (int i = 0; i < this._hueRect.Height; i++)
			{
				this._hueBitmap.SetPixel(0, i, ColorPickerControl.HsvToColor(i / (double)(this._hueRect.Height - 1) * 360.0, 1.0, 1.0));
			}
		}

		/// <summary>Disposes the cached square and hue bitmaps; the child controls are disposed by the base class.</summary>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (this._svBitmap != null)
				{
					this._svBitmap.Dispose();
				}
				if (this._hueBitmap != null)
				{
					this._hueBitmap.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		/// <summary>Clamps <paramref name="x"/> to 0..1.</summary>
		private static double Clamp01(double x)
		{
			if (x < 0.0)
			{
				return 0.0;
			}
			if (x > 1.0)
			{
				return 1.0;
			}
			return x;
		}

		/// <summary>Black or white, whichever reads better on <paramref name="c"/> (Rec. 601 luma, threshold 140 of 255).</summary>
		private static Color ContrastColor(Color c)
		{
			double luma = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
			return luma > 140.0 ? Color.Black : Color.White;
		}

		/// <summary>Converts HSV to an opaque <see cref="Color"/>.</summary>
		/// <param name="h">Hue in degrees; any value, wrapped into 0..360.</param>
		/// <param name="s">Saturation 0..1.</param>
		/// <param name="v">Value 0..1.</param>
		public static Color HsvToColor(double h, double s, double v)
		{
			double hue = h % 360.0;
			if (hue < 0.0)
			{
				hue += 360.0;
			}
			double chroma = v * s;
			double sector = hue / 60.0;
			double x = chroma * (1.0 - Math.Abs(sector % 2.0 - 1.0));
			double r;
			double g;
			double b;
			switch ((int)Math.Floor(sector))
			{
			case 0:
				r = chroma;
				g = x;
				b = 0.0;
				break;
			case 1:
				r = x;
				g = chroma;
				b = 0.0;
				break;
			case 2:
				r = 0.0;
				g = chroma;
				b = x;
				break;
			case 3:
				r = 0.0;
				g = x;
				b = chroma;
				break;
			case 4:
				r = x;
				g = 0.0;
				b = chroma;
				break;
			default:
				r = chroma;
				g = 0.0;
				b = x;
				break;
			}
			double m = v - chroma;
			return Color.FromArgb(ColorPickerControl.ToByte(r + m), ColorPickerControl.ToByte(g + m), ColorPickerControl.ToByte(b + m));
		}

		/// <summary>Scales 0..1 to 0..255 with rounding and clamping.</summary>
		private static int ToByte(double v)
		{
			int value = (int)Math.Round(v * 255.0);
			if (value < 0)
			{
				value = 0;
			}
			if (value > 255)
			{
				value = 255;
			}
			return value;
		}

		/// <summary>Converts <paramref name="c"/> to hue in degrees 0..360 (0 for greys), saturation 0..1 and value 0..1.</summary>
		private static void RgbToHsv(Color c, ref double h, ref double s, ref double v)
		{
			double r = c.R / 255.0;
			double g = c.G / 255.0;
			double b = c.B / 255.0;
			double max = Math.Max(r, Math.Max(g, b));
			double min = Math.Min(r, Math.Min(g, b));
			double delta = max - min;
			v = max;
			s = max <= 0.0 ? 0.0 : delta / max;
			if (delta <= 0.0)
			{
				h = 0.0;
			}
			else if (max == r)
			{
				h = 60.0 * ((g - b) / delta % 6.0);
			}
			else if (max == g)
			{
				h = 60.0 * ((b - r) / delta + 2.0);
			}
			else
			{
				h = 60.0 * ((r - g) / delta + 4.0);
			}
			if (h < 0.0)
			{
				h += 360.0;
			}
		}

		/// <summary>Pixel size of the cached saturation/value square; it is upscaled to <see cref="_svRect"/> when drawn.</summary>
		private const int SvResolution = 150;

		/// <summary>Bounds of the saturation/value square in client pixels.</summary>
		private readonly Rectangle _svRect;

		/// <summary>Bounds of the vertical hue strip in client pixels.</summary>
		private readonly Rectangle _hueRect;

		/// <summary>Bounds of the "New" (current colour) swatch.</summary>
		private readonly Rectangle _newRect;

		/// <summary>Bounds of the "Old" (original colour) swatch; clicking it restores the original.</summary>
		private readonly Rectangle _oldRect;

		/// <summary>"Hex" caption next to the hex box.</summary>
		private readonly Label _lblHex;

		/// <summary>Editable "#RRGGBB" box; see <see cref="HexTextChanged"/>.</summary>
		private readonly TextBox _txtHex;

		/// <summary>Read-only "R G B" decimal read-out.</summary>
		private readonly Label _lblRgb;

		/// <summary>"More..." button that opens the standard colour dialog.</summary>
		private readonly Button _btnDialog;

		/// <summary>Hue in degrees 0..360; preserved when the colour becomes grey or black.</summary>
		private double _h;

		/// <summary>Saturation 0..1.</summary>
		private double _s;

		/// <summary>Value (brightness) 0..1.</summary>
		private double _v;

		/// <summary>Current colour, always opaque.</summary>
		private Color _color;

		/// <summary>Original colour shown in the "Old" swatch, always opaque.</summary>
		private Color _original;

		/// <summary>Cached S/V square built for <see cref="_svHue"/>; null until first paint; owned and disposed here.</summary>
		private Bitmap _svBitmap;

		/// <summary>Hue the cached S/V square was built for; -1 forces a build on the first paint.</summary>
		private double _svHue;

		/// <summary>Cached 1-pixel-wide hue strip; null until first paint; owned and disposed here.</summary>
		private Bitmap _hueBitmap;

		/// <summary>Active drag target: 0 = none, 1 = S/V square, 2 = hue strip.</summary>
		private int _drag;

		/// <summary>True while <see cref="UpdateTexts"/> writes the hex box, so <see cref="HexTextChanged"/> ignores the change.</summary>
		private bool _updatingText;
	}
}

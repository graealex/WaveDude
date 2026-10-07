using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Scene tab: scene image / user-defined canvas, damping edge, image sources,
	/// and the glowing-edges overlay drawn on top of the field / intensity previews.
	/// The model is <c>_scene</c> (a <see cref="SceneSettings"/>), the loaded bitmap is
	/// <c>_sceneImage</c> (owned by the form, replaced through <see cref="ReplaceSceneImage"/>),
	/// and <c>_loadingScene</c> is the re-entrancy guard that makes the change handlers ignore
	/// programmatic control updates. Everything here runs on the UI thread.
	/// </summary>
	public partial class Form1
	{
		/// <summary>
		/// Applies the Scene tab control settings that are not in the designer (preview zoom mode,
		/// label ellipsis, text lengths, image-source scale range). Called once from the constructor.
		/// </summary>
		private void InitializeSceneControls()
		{
			this.picImagePreview.BorderStyle = BorderStyle.FixedSingle;
			this.picImagePreview.BackColor = SystemColors.ControlLight;
			this.picImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
			this.lblImageNameAndSize.AutoSize = false;
			this.lblImageNameAndSize.AutoEllipsis = true;
			this.lblImageNameAndSize.Size = new Size(470, 20);
			this.txtXSize.MaxLength = 5;
			this.txtYsize.MaxLength = 5;
			this.txtDampingEdgeSize.MaxLength = 4;
			this.numScaleImageSource.Minimum = 0m;
			this.numScaleImageSource.Maximum = 1000m;
		}

		/// <summary>
		/// Replaces <c>_scene</c> with the scene section of the open project's config, loads the
		/// referenced image as the scene image (silently leaving it unloaded when the file is missing
		/// or unreadable) and shows everything in the controls. Does nothing when no project is open.
		/// </summary>
		private void LoadSceneFromProject()
		{
			if (this._project == null)
			{
				return;
			}
			this._scene = SceneSettings.FromConfig(this._project.Config);
			this.ReplaceSceneImage(null);
			if (this._scene.ImageFile.Length > 0)
			{
				string imagePath = SceneSettings.ToFullPath(this._project.FolderPath, this._scene.ImageFile);
				if (File.Exists(imagePath))
				{
					try
					{
						this.ReplaceSceneImage(Form1.LoadImageCopy(imagePath));
					}
					catch (Exception)
					{
					}
				}
			}
			this.ShowSceneInControls();
		}

		/// <summary>
		/// Copies <c>_scene</c> into the Scene tab controls with <c>_loadingScene</c> set so the change
		/// handlers neither write back nor mark the project dirty, then refreshes control enabling,
		/// the preview and the damping-edge validation colour.
		/// </summary>
		private void ShowSceneInControls()
		{
			this._loadingScene = true;
			try
			{
				this.radUseSceneImage.Checked = this._scene.UseImage;
				this.radUserDefinedScene.Checked = !this._scene.UseImage;
				this.txtXSize.Text = this._scene.UserWidth.ToString(CultureInfo.InvariantCulture);
				this.txtYsize.Text = this._scene.UserHeight.ToString(CultureInfo.InvariantCulture);
				this.chkDampingEdge.Checked = this._scene.DampingEdgeEnabled;
				this.txtDampingEdgeSize.Text = this._scene.DampingEdgeSize.ToString(CultureInfo.InvariantCulture);
				this.chkUseImageSources.Checked = this._scene.UseSources;
				this.numScaleImageSource.Value = Form1.ClampToRange(this.numScaleImageSource, this._scene.ImageSourceScale);
				this.chkUseObjectSources.Checked = this._scene.UseObjectSources;
				this.numRefractiveIndex.Value = Form1.ClampToRange(this.numRefractiveIndex, this._scene.BackgroundRefractiveIndex);
				this.txtXSize.ForeColor = SystemColors.WindowText;
				this.txtYsize.ForeColor = SystemColors.WindowText;
			}
			finally
			{
				this._loadingScene = false;
			}
			this.UpdateSceneControlState();
			this.UpdateScenePreview();
			this.ShowDampingValidity();
		}

		/// <summary>Enables or disables the Scene tab controls according to the scene mode (image vs. user-defined size), the damping-edge checkbox and the image-sources checkbox.</summary>
		private void UpdateSceneControlState()
		{
			bool useImage = this._scene.UseImage;
			this.btnLoadSceneImage.Enabled = useImage;
			this.lblImageNameAndSize.Enabled = useImage;
			this.txtXSize.Enabled = !useImage;
			this.txtYsize.Enabled = !useImage;
			this.lblX.Enabled = !useImage;
			this.lblPix.Enabled = !useImage;
			this.numRefractiveIndex.Enabled = !useImage;
			this.Label20.Enabled = !useImage;
			this.txtDampingEdgeSize.Enabled = this._scene.DampingEdgeEnabled;
			this.Label1.Enabled = this._scene.DampingEdgeEnabled;
			this.numScaleImageSource.Enabled = this._scene.UseSources;
			this.lblScaleImgSourceIntensity.Enabled = this._scene.UseSources;
		}

		/// <summary>
		/// Shows either the scene image (zoomed) or a generated placeholder for the user-defined canvas
		/// size in <c>picImagePreview</c>, disposing the previous placeholder. The scene image itself
		/// is not disposed here (see <see cref="ReplaceSceneImage"/>).
		/// </summary>
		private void UpdateScenePreview()
		{
			this.UpdateImageLabel();
			if (this._scene.UseImage)
			{
				this.picImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
				this.picImagePreview.Image = this._sceneImage;
				this.DisposeEmptyPreview();
			}
			else
			{
				this.picImagePreview.SizeMode = PictureBoxSizeMode.CenterImage;
				Bitmap preview = this.MakeEmptyCanvasPreview(this._scene.UserWidth, this._scene.UserHeight);
				this.picImagePreview.Image = preview;
				this.DisposeEmptyPreview();
				this._emptyPreview = preview;
			}
		}

		/// <summary>Sets the image name/size label: no image, image configured but not loadable, or file name with pixel size.</summary>
		private void UpdateImageLabel()
		{
			if (this._scene.ImageFile.Length == 0)
			{
				this.lblImageNameAndSize.Text = "(no image loaded)";
			}
			else if (this._sceneImage == null)
			{
				this.lblImageNameAndSize.Text = "(image not found or not readable: " + Path.GetFileName(this._scene.ImageFile) + ")";
			}
			else
			{
				this.lblImageNameAndSize.Text = Path.GetFileName(this._scene.ImageFile) + "  (" + this._sceneImage.Width.ToString(CultureInfo.InvariantCulture) + " x " + this._sceneImage.Height.ToString(CultureInfo.InvariantCulture) + " px)";
			}
		}

		/// <summary>
		/// Draws a white placeholder bitmap with a grey frame and a "W x H px (empty scene)" caption,
		/// scaled with preserved aspect ratio to fit the preview box client area minus an 8 px margin
		/// (at least 2 x 2 px).
		/// </summary>
		/// <param name="canvasWidth">Canvas width in simulation pixels.</param>
		/// <param name="canvasHeight">Canvas height in simulation pixels.</param>
		/// <returns>A new bitmap; the caller stores it in <c>_emptyPreview</c> and disposes it.</returns>
		private Bitmap MakeEmptyCanvasPreview(int canvasWidth, int canvasHeight)
		{
			int maxWidth = Math.Max(16, this.picImagePreview.ClientSize.Width - 8);
			int maxHeight = Math.Max(16, this.picImagePreview.ClientSize.Height - 8);
			double scale = Math.Min((double)maxWidth / canvasWidth, (double)maxHeight / canvasHeight);
			int previewWidth = Math.Max(2, (int)Math.Round(canvasWidth * scale));
			int previewHeight = Math.Max(2, (int)Math.Round(canvasHeight * scale));
			Bitmap preview = new Bitmap(previewWidth, previewHeight, PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(preview))
			{
				g.Clear(Color.White);
				using (Pen pen = new Pen(Color.Gray))
				{
					g.DrawRectangle(pen, 0, 0, previewWidth - 1, previewHeight - 1);
				}
				using (Font font = new Font("Microsoft Sans Serif", 9f))
				using (StringFormat format = new StringFormat())
				{
					format.Alignment = StringAlignment.Center;
					format.LineAlignment = StringAlignment.Center;
					g.DrawString(canvasWidth.ToString(CultureInfo.InvariantCulture) + " x " + canvasHeight.ToString(CultureInfo.InvariantCulture) + " px (empty scene)", font, Brushes.Gray, new RectangleF(0f, 0f, previewWidth, previewHeight), format);
				}
			}
			return preview;
		}

		/// <summary>Disposes the placeholder preview bitmap, if any. Callers assign a new preview image first.</summary>
		private void DisposeEmptyPreview()
		{
			if (this._emptyPreview != null)
			{
				this._emptyPreview.Dispose();
				this._emptyPreview = null;
			}
		}

		/// <summary>
		/// Makes <paramref name="newImage"/> the current scene image, taking ownership. The previous
		/// image is detached from the preview box if it is shown there and disposed, and the glow-edges
		/// overlay is recomputed for the new image.
		/// </summary>
		/// <param name="newImage">The new scene image, or null to unload.</param>
		private void ReplaceSceneImage(Bitmap newImage)
		{
			Bitmap oldImage = this._sceneImage;
			this._sceneImage = newImage;
			if (oldImage != null)
			{
				if (object.ReferenceEquals(this.picImagePreview.Image, oldImage))
				{
					this.picImagePreview.Image = null;
				}
				oldImage.Dispose();
			}
			this.UpdateGlowEdgesImage();
		}

		/// <summary>Disposes the Sobel edge image and its resized cache.</summary>
		private void ClearGlowEdgesImage()
		{
			if (this._glowEdgesImage != null)
			{
				this._glowEdgesImage.Dispose();
				this._glowEdgesImage = null;
			}
			if (this._glowOverlayCache != null)
			{
				this._glowOverlayCache.Dispose();
				this._glowOverlayCache = null;
			}
		}

		/// <summary>
		/// Recomputes the Sobel edge image from <c>_sceneImage</c> (none when no image is loaded) and
		/// re-composes the field and intensity previews so the overlay follows the new scene.
		/// </summary>
		private void UpdateGlowEdgesImage()
		{
			this.ClearGlowEdgesImage();
			if (this._sceneImage != null)
			{
				this._glowEdgesImage = Form1.ComputeGlowingEdges(this._sceneImage);
			}
			this.RefreshPreviewOverlay(this._picField, this.FieldOverlayGain());
			this.RefreshPreviewOverlay(this._picIntensity, this.IntensityOverlayGain());
		}

		/// <summary>Overlay gain for the field preview: the brightness box value when the overlay checkbox is on, otherwise 0 (overlay off).</summary>
		private double FieldOverlayGain()
		{
			return this.chkDiplayOverlayInField.Checked ? Convert.ToDouble(this.numBrightnessFieldOverlay.Value) : 0.0;
		}

		/// <summary>Overlay gain for the intensity preview: the brightness box value when the overlay checkbox is on, otherwise 0 (overlay off).</summary>
		private double IntensityOverlayGain()
		{
			return this.chkDiplayOverlayInIntensity.Checked ? Convert.ToDouble(this.numBrightnessIntensityOverlay.Value) : 0.0;
		}

		/// <summary>
		/// Computes a per-channel 3x3 Sobel gradient magnitude (scaled by 0.25, clamped to 255) of
		/// <paramref name="source"/> on a black background, used as the glowing-edges preview overlay.
		/// The source is first drawn into a 24bpp copy so any pixel format works. The outermost pixel
		/// ring is always black, and images smaller than 3 x 3 give an all-black result.
		/// </summary>
		/// <returns>A new 24bpp bitmap of the source size, owned by the caller.</returns>
		private static Bitmap ComputeGlowingEdges(Bitmap source)
		{
			int width = source.Width;
			int height = source.Height;
			Bitmap edges = new Bitmap(width, height, PixelFormat.Format24bppRgb);
			using (Graphics g = Graphics.FromImage(edges))
			{
				g.Clear(Color.Black);
			}
			if (width < 3 || height < 3)
			{
				return edges;
			}
			using (Bitmap sourceCopy = new Bitmap(width, height, PixelFormat.Format24bppRgb))
			{
				using (Graphics g = Graphics.FromImage(sourceCopy))
				{
					g.DrawImageUnscaled(source, 0, 0);
				}
				Rectangle bounds = new Rectangle(0, 0, width, height);
				BitmapData sourceData = sourceCopy.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
				BitmapData edgesData = edges.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
				try
				{
					int sourceStride = sourceData.Stride;
					int edgesStride = edgesData.Stride;
					byte[] sourcePixels = new byte[sourceStride * height];
					byte[] edgePixels = new byte[edgesStride * height];
					Marshal.Copy(sourceData.Scan0, sourcePixels, 0, sourcePixels.Length);
					for (int y = 1; y < height - 1; y++)
					{
						int rowAbove = (y - 1) * sourceStride;
						int row = y * sourceStride;
						int rowBelow = (y + 1) * sourceStride;
						int edgeRow = y * edgesStride;
						for (int x = 1; x < width - 1; x++)
						{
							int above = rowAbove + (x - 1) * 3;
							int center = row + (x - 1) * 3;
							int below = rowBelow + (x - 1) * 3;
							int target = edgeRow + x * 3;
							for (int channel = 0; channel < 3; channel++)
							{
								int topLeft = sourcePixels[above + channel];
								int top = sourcePixels[above + 3 + channel];
								int topRight = sourcePixels[above + 6 + channel];
								int left = sourcePixels[center + channel];
								int right = sourcePixels[center + 6 + channel];
								int bottomLeft = sourcePixels[below + channel];
								int bottom = sourcePixels[below + 3 + channel];
								int bottomRight = sourcePixels[below + 6 + channel];
								int gx = topRight + 2 * right + bottomRight - (topLeft + 2 * left + bottomLeft);
								int gy = bottomLeft + 2 * bottom + bottomRight - (topLeft + 2 * top + topRight);
								// 0.25 keeps a full-contrast step edge (gradient 4 * 255) just within the byte range.
								double magnitude = Math.Sqrt((double)gx * gx + (double)gy * gy) * 0.25;
								edgePixels[target + channel] = (byte)Math.Round(Math.Min(255.0, magnitude));
							}
						}
					}
					Marshal.Copy(edgePixels, 0, edgesData.Scan0, edgePixels.Length);
				}
				finally
				{
					sourceCopy.UnlockBits(sourceData);
					edges.UnlockBits(edgesData);
				}
			}
			return edges;
		}

		/// <summary>
		/// Returns the edge image scaled (bilinear) to <paramref name="targetSize"/>, the preview
		/// frame size in picture-box pixels. The result is cached in <c>_glowOverlayCache</c> until a
		/// different size is requested or the scene image changes.
		/// </summary>
		/// <returns>The cached 24bpp bitmap (owned by the form; do not dispose), or null when there is no scene image.</returns>
		private Bitmap GetGlowOverlayResized(Size targetSize)
		{
			if (this._glowEdgesImage == null)
			{
				return null;
			}
			if (this._glowOverlayCache != null && this._glowOverlayCacheSize.Equals(targetSize))
			{
				return this._glowOverlayCache;
			}
			if (this._glowOverlayCache != null)
			{
				this._glowOverlayCache.Dispose();
			}
			Bitmap resized = new Bitmap(Math.Max(1, targetSize.Width), Math.Max(1, targetSize.Height), PixelFormat.Format24bppRgb);
			using (Graphics g = Graphics.FromImage(resized))
			{
				g.Clear(Color.Black);
				g.InterpolationMode = InterpolationMode.HighQualityBilinear;
				g.DrawImage(this._glowEdgesImage, new Rectangle(Point.Empty, targetSize));
			}
			this._glowOverlayCache = resized;
			this._glowOverlayCacheSize = targetSize;
			return resized;
		}

		/// <summary>
		/// Returns a copy of <paramref name="frame"/> with the glow overlay (resized to the frame)
		/// multiplied by <paramref name="gain"/> and added to each RGB channel, saturating at 255.
		/// Alpha is left untouched. Frames with fewer than 3 bytes per pixel, or no overlay, give a plain copy.
		/// </summary>
		/// <param name="frame">The raw preview frame; not modified.</param>
		/// <param name="gain">Overlay brightness multiplier; negative values count as 0.</param>
		/// <returns>A new bitmap owned by the caller.</returns>
		private Bitmap ComposeGlowOverlay(Bitmap frame, double gain)
		{
			Bitmap overlay = this.GetGlowOverlayResized(frame.Size);
			Bitmap result = new Bitmap(frame);
			if (overlay == null)
			{
				return result;
			}
			double overlayGain = Math.Max(0.0, gain);
			int bytesPerPixel = Image.GetPixelFormatSize(result.PixelFormat) / 8;
			if (bytesPerPixel < 3)
			{
				return result;
			}
			Rectangle bounds = new Rectangle(0, 0, frame.Width, frame.Height);
			BitmapData resultData = result.LockBits(bounds, ImageLockMode.ReadWrite, result.PixelFormat);
			BitmapData overlayData = overlay.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
			try
			{
				int resultStride = resultData.Stride;
				int overlayStride = overlayData.Stride;
				int height = frame.Height;
				int width = frame.Width;
				byte[] resultPixels = new byte[resultStride * height];
				byte[] overlayPixels = new byte[overlayStride * height];
				Marshal.Copy(resultData.Scan0, resultPixels, 0, resultPixels.Length);
				Marshal.Copy(overlayData.Scan0, overlayPixels, 0, overlayPixels.Length);
				for (int y = 0; y < height; y++)
				{
					int resultRow = y * resultStride;
					int overlayRow = y * overlayStride;
					for (int x = 0; x < width; x++)
					{
						int resultOffset = resultRow + x * bytesPerPixel;
						int overlayOffset = overlayRow + x * 3;
						resultPixels[resultOffset] = (byte)Math.Round(Math.Min(255.0, resultPixels[resultOffset] + overlayPixels[overlayOffset] * overlayGain));
						resultPixels[resultOffset + 1] = (byte)Math.Round(Math.Min(255.0, resultPixels[resultOffset + 1] + overlayPixels[overlayOffset + 1] * overlayGain));
						resultPixels[resultOffset + 2] = (byte)Math.Round(Math.Min(255.0, resultPixels[resultOffset + 2] + overlayPixels[overlayOffset + 2] * overlayGain));
					}
				}
				Marshal.Copy(resultPixels, 0, resultData.Scan0, resultPixels.Length);
			}
			finally
			{
				result.UnlockBits(resultData);
				overlay.UnlockBits(overlayData);
			}
			return result;
		}

		/// <summary>
		/// Shows <paramref name="frame"/> in <paramref name="box"/>, taking ownership of it. The raw
		/// frame is kept in <c>box.Tag</c> and <c>box.Image</c> receives either the same object or,
		/// when <paramref name="gain"/> &gt; 0 and an overlay exists, a composed copy. The previous raw
		/// frame and previous composed image (when distinct) are disposed. Called on the UI thread
		/// from <c>ShowPreview</c>, which <c>SimulationRunner</c> reaches through <c>PostToUi</c>.
		/// </summary>
		private void SetPreviewFrame(PictureBox box, Bitmap frame, double gain)
		{
			Image oldImage = box.Image;
			Bitmap oldFrame = box.Tag as Bitmap;
			box.Tag = frame;
			box.Image = ((gain > 0.0 && this._glowEdgesImage != null) ? this.ComposeGlowOverlay(frame, gain) : frame);
			if (oldImage != null && !object.ReferenceEquals(oldImage, oldFrame))
			{
				oldImage.Dispose();
			}
			if (oldFrame != null)
			{
				oldFrame.Dispose();
			}
		}

		/// <summary>
		/// Re-composes the overlay for a preview box from the raw frame kept in <c>box.Tag</c>
		/// (no-op when the box is null or holds no frame), disposing the previously composed image.
		/// Used when the overlay checkbox / brightness or the scene image changes.
		/// </summary>
		private void RefreshPreviewOverlay(PictureBox box, double gain)
		{
			if (box == null)
			{
				return;
			}
			Bitmap frame = box.Tag as Bitmap;
			if (frame == null)
			{
				return;
			}
			Image oldImage = box.Image;
			box.Image = ((gain > 0.0 && this._glowEdgesImage != null) ? this.ComposeGlowOverlay(frame, gain) : frame);
			if (oldImage != null && !object.ReferenceEquals(oldImage, frame))
			{
				oldImage.Dispose();
			}
		}

		/// <summary>Toggling the field overlay re-composes the field preview.</summary>
		private void chkDiplayOverlayInField_CheckedChanged(object sender, EventArgs e)
		{
			this.RefreshPreviewOverlay(this._picField, this.FieldOverlayGain());
		}

		/// <summary>Toggling the intensity overlay re-composes the intensity preview.</summary>
		private void chkDiplayOverlayInIntensity_CheckedChanged(object sender, EventArgs e)
		{
			this.RefreshPreviewOverlay(this._picIntensity, this.IntensityOverlayGain());
		}

		/// <summary>Changing the field overlay brightness re-composes the field preview.</summary>
		private void numBrightnessFieldOverlay_ValueChanged(object sender, EventArgs e)
		{
			this.RefreshPreviewOverlay(this._picField, this.FieldOverlayGain());
		}

		/// <summary>Changing the intensity overlay brightness re-composes the intensity preview.</summary>
		private void numBrightnessIntensityOverlay_ValueChanged(object sender, EventArgs e)
		{
			this.RefreshPreviewOverlay(this._picIntensity, this.IntensityOverlayGain());
		}

		/// <summary>
		/// Loads an image file into a new memory-backed 32bpp ARGB <see cref="Bitmap"/> so GDI+ does
		/// not keep the file locked. Unreadable files throw the usual IO / GDI+ exceptions.
		/// </summary>
		private static Bitmap LoadImageCopy(string fullPath)
		{
			using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (Image image = Image.FromStream(stream))
			{
				return new Bitmap(image);
			}
		}

		/// <summary>
		/// Size of the simulation canvas in canvas pixels: the scene image size in image mode
		/// (<see cref="Size.Empty"/> when no image is loaded), otherwise the user-defined width and height.
		/// </summary>
		private Size EffectiveCanvas()
		{
			if (this._scene.UseImage)
			{
				return (this._sceneImage == null) ? Size.Empty : this._sceneImage.Size;
			}
			return new Size(this._scene.UserWidth, this._scene.UserHeight);
		}

		/// <summary>
		/// Replaces the image in <paramref name="pic"/> (the source / probe placement boxes) with a copy
		/// of the scene image, or with a black canvas of the effective size in user-defined mode, and
		/// disposes the previous image. When the canvas size is not known yet a message is shown and
		/// the box is left unchanged.
		/// </summary>
		private void ImportSceneImageInto(PictureBox pic)
		{
			Image oldImage = pic.Image;
			Bitmap canvas;
			if (this._scene.UseImage && this._sceneImage != null)
			{
				canvas = new Bitmap(this._sceneImage);
			}
			else
			{
				Size size = this.EffectiveCanvas();
				if (size.Width <= 0 || size.Height <= 0)
				{
					MessageBox.Show(this, "The canvas size is not known yet\r\n(choose a scene image, or a user defined width and height, on the Configuration tab first).", "Import scene image", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					return;
				}
				canvas = new Bitmap(size.Width, size.Height);
				using (Graphics g = Graphics.FromImage(canvas))
				{
					g.Clear(Color.Black);
				}
			}
			pic.Image = canvas;
			pic.Invalidate();
			if (oldImage != null)
			{
				oldImage.Dispose();
			}
		}

		/// <summary>
		/// Like <see cref="ImportSceneImageInto"/> but only when <paramref name="pic"/> has no image yet,
		/// and silently does nothing when the canvas size is unknown. Called when the Sources / Probes
		/// tabs are shown.
		/// </summary>
		private void AutoLoadSceneImageIfEmpty(PictureBox pic)
		{
			if (pic == null || pic.Image != null)
			{
				return;
			}
			if (this._scene.UseImage && this._sceneImage != null)
			{
				pic.Image = new Bitmap(this._sceneImage);
				pic.Invalidate();
			}
			else
			{
				Size size = this.EffectiveCanvas();
				if (size.Width <= 0 || size.Height <= 0)
				{
					return;
				}
				Bitmap canvas = new Bitmap(size.Width, size.Height);
				using (Graphics g = Graphics.FromImage(canvas))
				{
					g.Clear(Color.Black);
				}
				pic.Image = canvas;
				pic.Invalidate();
			}
		}

		/// <summary>
		/// Parses <paramref name="text"/> as an unsigned decimal integer (invariant culture, digits only
		/// after trimming) and checks it lies in <paramref name="minValue"/>..<paramref name="maxValue"/>.
		/// </summary>
		/// <param name="text">Text box content; leading / trailing whitespace is trimmed.</param>
		/// <param name="minValue">Smallest accepted value, inclusive.</param>
		/// <param name="maxValue">Largest accepted value, inclusive.</param>
		/// <param name="value">Receives the parsed number (0 when parsing fails), even when the range check fails.</param>
		/// <returns>True when the text parsed and is within range.</returns>
		private static bool TryParseInt(string text, int minValue, int maxValue, ref int value)
		{
			return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= minValue && value <= maxValue;
		}

		/// <summary>
		/// Validates the damping-edge text: it must be a whole number 1..8192 and, when the canvas
		/// size is known, less than half of the smaller canvas side.
		/// </summary>
		/// <param name="text">Content of the damping-edge text box.</param>
		/// <param name="value">Receives the parsed number.</param>
		/// <returns>An empty string when acceptable, otherwise the message to show the user.</returns>
		private string DampingProblem(string text, ref int value)
		{
			if (!Form1.TryParseInt(text, 1, 8192, ref value))
			{
				return "Enter a whole number of pixels, 1 or more.";
			}
			Size size = this.EffectiveCanvas();
			int smallerSide = Math.Min(size.Width, size.Height);
			if (size.Width > 0 && 2 * value >= smallerSide)
			{
				return "The damping edge must be less than half of the smaller canvas side (" + smallerSide.ToString(CultureInfo.InvariantCulture) + " px).";
			}
			return "";
		}

		/// <summary>Colours the damping-edge text box red when its text is not acceptable for the current canvas size, without changing the stored value.</summary>
		private void ShowDampingValidity()
		{
			int value = 0;
			bool valid = this.DampingProblem(this.txtDampingEdgeSize.Text, ref value).Length == 0;
			this.txtDampingEdgeSize.ForeColor = valid ? SystemColors.WindowText : Color.Red;
		}

		/// <summary>
		/// Switching between "use scene image" and "user defined" stores the mode, rebuilds or clears
		/// the glow overlay, updates control enabling, the preview and the damping validation, and
		/// marks the project changed. Ignored for the radio being unchecked and while <c>_loadingScene</c> is set.
		/// </summary>
		private void SceneMode_CheckedChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			RadioButton radio = (RadioButton)sender;
			if (!radio.Checked)
			{
				return;
			}
			this._scene.UseImage = object.ReferenceEquals(radio, this.radUseSceneImage);
			if (this._scene.UseImage)
			{
				this.UpdateGlowEdgesImage();
			}
			else
			{
				this.ClearGlowEdgesImage();
				this.RefreshPreviewOverlay(this._picField, this.FieldOverlayGain());
				this.RefreshPreviewOverlay(this._picIntensity, this.IntensityOverlayGain());
			}
			this.UpdateSceneControlState();
			this.UpdateScenePreview();
			this.ShowDampingValidity();
			this.MarkProjectChanged();
		}

		/// <summary>
		/// Lets the user pick an image file, loads it as the scene image, copies it into the project's
		/// scene-image folder when it lies outside the project folder, stores the project-relative path,
		/// switches to image mode and marks the project changed. Unreadable files are reported and nothing
		/// changes. Also invoked from the Output tab's "load external" button.
		/// </summary>
		private void btnLOadSceneImage_Click(object sender, EventArgs e)
		{
			string projectFolder = ((this._project == null) ? "" : this._project.FolderPath);
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "Choose the scene image";
				dialog.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files (*.*)|*.*";
				if (this._project != null)
				{
					if (Directory.Exists(this._project.SceneImageFolder))
					{
						dialog.InitialDirectory = this._project.SceneImageFolder;
					}
					else
					{
						dialog.InitialDirectory = this._project.FolderPath;
					}
				}
				if (dialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				Bitmap image;
				try
				{
					image = Form1.LoadImageCopy(dialog.FileName);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "The file could not be read as an image:\r\n\r\n" + ex.Message, "Scene image", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return;
				}
				this.ReplaceSceneImage(image);
				string imagePath = dialog.FileName;
				if (this._project != null && !Form1.IsInsideFolder(imagePath, this._project.FolderPath))
				{
					imagePath = Form1.CopyIntoSceneImageFolder(this._project, imagePath);
				}
				this._scene.ImageFile = SceneSettings.ToStoredPath(projectFolder, imagePath);
				this._scene.UseImage = true;
			}
			this._loadingScene = true;
			try
			{
				this.radUseSceneImage.Checked = true;
			}
			finally
			{
				this._loadingScene = false;
			}
			this.UpdateSceneControlState();
			this.UpdateScenePreview();
			this.ShowDampingValidity();
			this.MarkProjectChanged();
		}

		/// <summary>
		/// Typing a user-defined width or height (8..16384) stores it, refreshes the preview and damping
		/// validation and marks the project changed; invalid text turns the box red and shows a tooltip
		/// without changing the stored value.
		/// </summary>
		private void CanvasSize_TextChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			TextBox box = (TextBox)sender;
			int size = 0;
			if (Form1.TryParseInt(box.Text, 8, 16384, ref size))
			{
				box.ForeColor = SystemColors.WindowText;
				this._sceneTip.Hide(box);
				if (object.ReferenceEquals(box, this.txtXSize))
				{
					this._scene.UserWidth = size;
				}
				else
				{
					this._scene.UserHeight = size;
				}
				this.UpdateScenePreview();
				this.ShowDampingValidity();
				this.MarkProjectChanged();
			}
			else
			{
				box.ForeColor = Color.Red;
				this._sceneTip.Show("Enter a whole number of pixels from 8 to 16384.", box, 0, box.Height + 2, 3000);
			}
		}

		/// <summary>Stores the damping-edge on/off flag, enables or disables the size box and marks the project changed.</summary>
		private void chkDampingEdge_CheckedChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			this._scene.DampingEdgeEnabled = this.chkDampingEdge.Checked;
			this.UpdateSceneControlState();
			this.MarkProjectChanged();
		}

		/// <summary>
		/// Stores the damping-edge size when <see cref="DampingProblem"/> accepts the text and marks the
		/// project changed; otherwise turns the box red and shows the problem as a tooltip.
		/// </summary>
		private void DampingSize_TextChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			int value = 0;
			string problem = this.DampingProblem(this.txtDampingEdgeSize.Text, ref value);
			if (problem.Length == 0)
			{
				this._scene.DampingEdgeSize = value;
				this.txtDampingEdgeSize.ForeColor = SystemColors.WindowText;
				this._sceneTip.Hide(this.txtDampingEdgeSize);
				this.MarkProjectChanged();
			}
			else
			{
				this.txtDampingEdgeSize.ForeColor = Color.Red;
				this._sceneTip.Show(problem, this.txtDampingEdgeSize, 0, this.txtDampingEdgeSize.Height + 2, 3000);
			}
		}

		/// <summary>
		/// Leaving a canvas-size or damping-size text box restores the last accepted value from
		/// <c>_scene</c> (under <c>_loadingScene</c> so no change handler runs), resets the text colour
		/// and re-validates the damping edge.
		/// </summary>
		private void SceneText_Leave(object sender, EventArgs e)
		{
			TextBox box = (TextBox)sender;
			this._sceneTip.Hide(box);
			this._loadingScene = true;
			try
			{
				if (object.ReferenceEquals(box, this.txtXSize))
				{
					box.Text = this._scene.UserWidth.ToString(CultureInfo.InvariantCulture);
				}
				else if (object.ReferenceEquals(box, this.txtYsize))
				{
					box.Text = this._scene.UserHeight.ToString(CultureInfo.InvariantCulture);
				}
				else
				{
					box.Text = this._scene.DampingEdgeSize.ToString(CultureInfo.InvariantCulture);
				}
				box.ForeColor = SystemColors.WindowText;
			}
			finally
			{
				this._loadingScene = false;
			}
			this.ShowDampingValidity();
		}

		/// <summary>Stores whether green pixels of the scene image act as sources, enables or disables the scale box and marks the project changed.</summary>
		private void chkUseSources_CheckedChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			this._scene.UseSources = this.chkUseImageSources.Checked;
			this.UpdateSceneControlState();
			this.MarkProjectChanged();
		}

		/// <summary>Stores whether the object sources from the source list are used and marks the project changed.</summary>
		private void chkUseObjectSources_CheckedChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			this._scene.UseObjectSources = this.chkUseObjectSources.Checked;
			this.MarkProjectChanged();
		}

		/// <summary>Stores the image-source intensity scale (0..1000) and marks the project changed.</summary>
		private void ImageSourceScale_ValueChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			this._scene.ImageSourceScale = Convert.ToDouble(this.numScaleImageSource.Value);
			this.MarkProjectChanged();
		}

		/// <summary>Stores the background refractive index of the user-defined (empty) scene and marks the project changed.</summary>
		private void RefractiveIndex_ValueChanged(object sender, EventArgs e)
		{
			if (this._loadingScene)
			{
				return;
			}
			this._scene.BackgroundRefractiveIndex = Convert.ToDouble(this.numRefractiveIndex.Value);
			this.MarkProjectChanged();
		}

		/// <summary>
		/// Converts <paramref name="value"/> to a decimal clamped to the <see cref="NumericUpDown.Minimum"/>..
		/// <see cref="NumericUpDown.Maximum"/> range of <paramref name="box"/>, so that assigning it to
		/// <see cref="NumericUpDown.Value"/> cannot throw. Used by all tabs when loading settings.
		/// </summary>
		private static decimal ClampToRange(NumericUpDown box, double value)
		{
			return (value < Convert.ToDouble(box.Minimum)) ? box.Minimum : ((value > Convert.ToDouble(box.Maximum)) ? box.Maximum : new decimal(value));
		}

		/// <summary>Jumps to the source list tab.</summary>
		private void btnEditSourceList_Click(object sender, EventArgs e)
		{
			this.tabMainFunctions.SelectedTab = this.tabSourceList;
		}
	}
}

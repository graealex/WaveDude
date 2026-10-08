using System;
using System.Drawing;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The Info &gt; Help window: a structured description of what each tab of <c>tabMainFunctions</c>
	/// does, which controls on it matter most, and what those controls influence elsewhere in the
	/// program, plus a dedicated section on the propagation speed c, since its correct value depends
	/// on both the Laplacian order and the source frequency and is easy to get wrong.
	/// Designer-editable: the layout (<c>rtbHelp</c>, the Close button) lives in
	/// <c>HelpForm.Designer.cs</c>; this file only fills <c>rtbHelp</c>'s text, in
	/// <see cref="FillHelpText"/> (headings, paragraphs and bullet points built in code). Opened
	/// modally from <see cref="Form1.HelpToolStripMenuItem_Click"/>.
	/// </summary>
	public partial class HelpForm : Form
	{
		/// <summary>Builds the help text and moves the caret to the top so the box is not scrolled to the end.</summary>
		public HelpForm()
		{
			base.Load += HelpForm_Load;
			InitializeComponent();
			FillHelpText(rtbHelp);
			rtbHelp.SelectionStart = 0;
			rtbHelp.SelectionLength = 0;
		}

		/// <summary>
		/// Writes the actual help text into <paramref name="rtb"/>, one section per tab/topic (the tabs
		/// of the main window in UI order, followed by the explanation of the c-value and the menu),
		/// using <see cref="AppendHeading"/>/<see cref="AppendBody"/>/<see cref="AppendBullet"/> for
		/// simple formatting (bold headings, indented bullets).
		/// </summary>
		private void FillHelpText(RichTextBox rtb)
		{
			AppendHeading(rtb, "Wave Dude 2D - Help");
			AppendHeading(rtb, "Disclaimer");
			AppendBody(rtb, "The current software comes with no warranty or support and is used at your own risk. It is free software, released under the GNU General Public License, version 3 or later: you may use, modify and redistribute it under the terms of that license (see the LICENSE file that comes with the source code). Bugs or suggestions for future improvements can be send to the email address in the 'Info > About' screen");
			AppendHeading(rtb, "Getting started: the project root");
			AppendBody(rtb, "Every project, its settings, scene, sources, probes and exported files, lives in its own subfolder under one chosen 'project root' folder. Set it with File > Choose project root, or the 'Browse root' button on the Settings tab; the current root is shown above the tabs.");
			AppendBody(rtb, "The 'Projects in current root' list (Settings tab) shows every project found there: New project, Load, Rename, Delete and Save as all act on the selected one. File > New / Open / Save and 'Save changes to project' act on whichever project is currently open. Nothing is written to disk until you save your project.Projects can be shared between users by sharing the project folder and copy it to their root folder ");
			AppendBody(rtb, "Load, Rename, Delete and Save as all act on the selected one. File > New / Open / Save and 'Save changes to project' act on whichever project is currently open. Nothing is written to disk until you save.");
			AppendHeading(rtb, "Settings tab");
			AppendBody(rtb, "The main control panel: engine and rendering settings, the medium/scene, and starting a run.");
			AppendBullet(rtb, "Use GPU (generally faster)", "switches the simulation engine between CPU and GPU (OpenCL). Faster for larger grids or step counts, but needs a working OpenCL GPU; the CPU engine is the safe fallback.");
			AppendBullet(rtb, "Use 4th order Laplacian + sub-sampling", "switches the numerical scheme used to step the wave equation each frame. It runs somewhat slower but strongly reduces numerical dispersion (see 'Propagation speed c' below) - and changes what c should be set to.");
			AppendBullet(rtb, "Simulation steps / Visualize every", "how many steps a run performs, and how often (in steps) the live preview and probes are refreshed while it runs. A larger 'visualize every' makes a run finish faster at the cost of a choppier preview.");
			AppendBullet(rtb, "Intensity decay factor", "how quickly the accumulated Intensity view forgets past field energy, i.e. how 'persistent' the intensity build-up looks.");
			AppendBullet(rtb, "Field/Intensity color scheme, scale and offset", "which color table renders each view (see the Colors tab), and how the raw field/intensity values are stretched (scale) and shifted (offset) into that table's brightness range before coloring. The same scale controls also appear, linked, on the Preview tab.");
			AppendBullet(rtb, "Preview video of / Export video of (Field/Intensity)", "which of the two views is shown live while running, and which is written to a video file when the run finishes - independent choices.");
			AppendBullet(rtb, "Video quality (%)", "the compression quality used when writing exported video files.");
			AppendBullet(rtb, "Refractive index / Damping edge (Scene properties)", "the refractive index shapes the medium the wave travels through (a higher index locally slows the wave - it is what actually sets the per-pixel propagation speed the simulation uses, not c; see below). Damping edge adds an absorbing border so waves leave the grid instead of reflecting off its edges.");
			AppendBullet(rtb, "User defined (X/Y) scene vs. Scene image", "whether the medium's layout comes from a simple, hand-set shape or from an imported image (its brightness mapped to refractive index).");
			AppendBullet(rtb, "Use object sources / Use image sources, Edit source list", "whether wave sources are the ones built on the Sources tab, or amplitude taken from an image; 'Edit source list' jumps to the Sources tab.");
			AppendBody(rtb, "Found at the bottom of everly screen: Quick navigate (jump to Preview/Probes/Sources/Settings), Simulation control (Start Simulation / Pause-Resume), and Share with source/probe (send the current scene, field or intensity picture into the Sources or Probes tools as a background image).");
			AppendHeading(rtb, "About Scene Images");
			AppendBody(rtb, "Currently, scene images can only be created externally in a program like for example Photoshop. Save the images in a lossless format like .png . To add objects to a scene that the waves can interact with, you can use the red and green channels in the image color palette.");
			AppendBullet(rtb, "Red channel (Refractive index)", "can effectively use values ranging from 100 to 255, which represent refractive index values ranging from 1.00 to 2.55. Values lower than 100 will be set to 100 as a minimum. The red channel can be used to refract or reflect waves and create for example lenses.");
			AppendBullet(rtb, "Blue channel (Damping)", "reserved for damping - use values between 0 and 255, where zero means no damping and 255 heavy damping. It can be used to create regions that absorb waves like for example borders.");
			AppendBullet(rtb, "Green channel (Sources)", "reserved for creating wave sources, where the values 1-255 represent the frequency of the waves emitted by a pixel. If you use in-scene sources, do not use anti-aliasing to create them since this can cause the pixels next to a source to also contain a (lower) value in the green channel. When you want to use in-image sources, you must have 'use image sources' checked in the setup tab.");
			AppendHeading(rtb, "Sources tab");
			AppendBody(rtb, "Defines every object-type wave source placed in the scene; what Is set here directly drives what the simulation emits. When using  ");
			AppendBullet(rtb, "Main type: Single vs. composite", "a single source is one emitter; a composite source is a whole array of them, laid out with a chosen spacing and array size - useful for e.g. simulating an extended or phased source.");
			AppendBullet(rtb, "Mode: Continuous vs. Pulsed, Frequency, Amplitude, Phase", "a continuous source oscillates forever at the set frequency; a pulsed one also uses the pulse shape/width to taper on and off. Frequency (via the estimated wavelength label) is one of the two things that determine the right value for c - see below.");
			AppendBullet(rtb, "Composite array: Spacing, Array size, Randomize (Phase/Position/Frequency/Off state)", "how densely and how far the individual emitters in a composite source are spread, and how much each one's properties should vary at random rather than being identical.");
			AppendBullet(rtb, "Place current source / Pick / Copy / Delete / Save source data", "positions a source in the scene, duplicates or removes it, and can save its data separately; 'Detach after' stops further edits in this panel from affecting an already-placed source.");
			AppendHeading(rtb, "Probes tab");
			AppendBody(rtb, "Two independent probes (Probe 1, Probe 2), each measuring the field or intensity over the course of a run and graphing it.");
			AppendBullet(rtb, "Place on: Field / Intensity, Measurement / Probe type", "which picture the probe samples, and what it reports from it (e.g. a value at a point, or an integrated quantity over a box).");
			AppendBullet(rtb, "Position A / Position B, Box size, Pick", "where the probe samples - a single point, or a box defined by two corners - set either numerically or by picking on the preview.");
			AppendBullet(rtb, "Full scale vs. Running scale, Auto scale X / Y", "whether the probe's graph shows the whole run at once or only a recent 'running' window, and whether its axes rescale automatically as new data comes in.");
			AppendBullet(rtb, "Start recording at step #", "delays when the probe starts collecting data, useful for skipping an initial transient before the wave pattern settles.");
			AppendHeading(rtb, "Preview tab");
			AppendBody(rtb, "Shows the live Field and Intensity pictures while a simulation runs, and is where finished pictures/video are exported.");
			AppendBullet(rtb, "Field/intensity scale factor", "the same brightness scale as on the Settings tab - the two are linked, so changing one updates the other.");
			AppendBullet(rtb, "Display overlay, overlay brightness", "blends a 'glowing edges' outline (derived from the scene image) on top of either preview, purely as a visual aid to see the scene layout under the field/intensity. This overlay is also baked into an exported frame or video when it is switched on, not just shown live.");
			AppendBullet(rtb, "Export video of (Field/Intensity), Export frame as image", "start a video export for the checked view(s) once a run starts, or immediately save the current picture as a PNG. Exported file names end in a plain running number (e.g. _0001) - one higher than the highest such number already in that output folder - rather than a date/time stamp.");
			AppendBullet(rtb, "Open image/video folder, View video in media player", "quick access to what has already been exported for this project.");
			AppendHeading(rtb, "Manage output tab");
			AppendBody(rtb, "An overview of everything a project has produced: scenes, exported images and exported videos, each with its creation date. From here files can be renamed, deleted, previewed, opened in their folder, viewed in the media player, copied to the clipboard, or shared back into the Sources/Probes tabs as a background image; external scene files can also be loaded in.");
			AppendHeading(rtb, "Colors tab");
			AppendBody(rtb, "Manages the color schemes used to render the Field and Intensity pictures everywhere else in the program.");
			AppendBullet(rtb, "Scheme list", "every scheme found in the scheme folder, sortable by clicking the Scheme or Type column header; Type marks whether a scheme is meant for the Field or the Intensity view (the Settings/Preview tabs' color-scheme pickers use this).");
			AppendBullet(rtb, "Color Scheme Editor: Color 1..9, color picker", "builds a 9-stop scheme by picking a color for each stop; Save, Save As, Rename and Delete act on the scheme currently being edited.");
			AppendBullet(rtb, "Scheme folder, Choose...", "where schemes are read from and saved to; changing it reloads the list.");
			AppendHeading(rtb, "Propagation speed (c): the Laplacian order and source frequency");
			AppendBody(rtb, "c (File > Set c-value, in pixels per simulation step) is only a display/estimate value: it is used solely to compute the estimated wavelength shown next to a source's frequency on the Sources tab (lambda = c / frequency). The wave speed the simulation actually uses at each point comes from the scene's refractive index instead, and can vary from point to point - c does not change how the run itself behaves, only what that wavelength readout says.");
			AppendBody(rtb, "Getting that readout right, however, depends on both the Laplacian order and the source's frequency, because of numerical dispersion - the standard (3rd order) Laplacian makes longer wavelengths propagate faster through the grid than shorter ones, purely as a numerical artifact of the scheme, not a real physical effect.");
			AppendBullet(rtb, "4th order Laplacian + sub-sampling (on)", "greatly reduces this dispersion. With it on, set c to 1.00 - the estimate then stays accurate across frequencies.");
			AppendBullet(rtb, "Standard 3rd order Laplacian (4th order off)", "dispersion is significant, so the correct c depends strongly on frequency: lower frequencies need a higher c, higher frequencies need a lower c. For example, at a source frequency where the true wavelength is 15 pixels, c must be set to about 0.56 (not 1.00) for the readout to show that; leaving c at 1.00 there would read out roughly 9 pixels instead.");
			AppendBody(rtb, "In short: when 4th order is off, re-check (or reduce) c whenever the source frequency changes noticeably. The '(Info)' label next to the 4th order checkbox on the Settings tab repeats this explanation in the program itself.");
			AppendHeading(rtb, "Menu");
			AppendBody(rtb, "File: New, Open, Choose project root, Set c-value, Save, Exit.\r\nInfo: this Help page, and About.");
			AppendBody(rtb, "Enjoy! Jeroen \r\n");
		}

		/// <summary>Appends a blank line and then <paramref name="text"/> in bold, near-black type, as a section heading.</summary>
		private void AppendHeading(RichTextBox rtb, string text)
		{
			rtb.SelectionStart = rtb.TextLength;
			rtb.SelectionLength = 0;
			rtb.SelectionFont = new Font(rtb.Font, FontStyle.Bold);
			rtb.SelectionColor = Color.FromArgb(20, 20, 20);
			rtb.AppendText("\r\n" + text + "\r\n");
		}

		/// <summary>Appends <paramref name="text"/> as a regular black paragraph ending in a line break.</summary>
		private void AppendBody(RichTextBox rtb, string text)
		{
			rtb.SelectionStart = rtb.TextLength;
			rtb.SelectionLength = 0;
			rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
			rtb.SelectionColor = Color.Black;
			rtb.AppendText(text + "\r\n");
		}

		/// <summary>
		/// Appends a single indented "- Label: explanation" line: a dash, <paramref name="label"/> in
		/// bold followed by a colon, then <paramref name="explanation"/> in regular type. Used for the
		/// per-control breakdowns within a tab's section.
		/// </summary>
		private void AppendBullet(RichTextBox rtb, string label, string explanation)
		{
			rtb.SelectionStart = rtb.TextLength;
			rtb.SelectionLength = 0;
			rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
			rtb.SelectionColor = Color.Black;
			rtb.AppendText("   - ");
			rtb.SelectionStart = rtb.TextLength;
			rtb.SelectionLength = 0;
			rtb.SelectionFont = new Font(rtb.Font, FontStyle.Bold);
			rtb.AppendText(label + ": ");
			rtb.SelectionStart = rtb.TextLength;
			rtb.SelectionLength = 0;
			rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
			rtb.AppendText(explanation + "\r\n");
		}

		/// <summary>Does nothing; still wired to <see cref="Form.Load"/> by the constructor.</summary>
		private void HelpForm_Load(object sender, EventArgs e)
		{
		}
	}
}

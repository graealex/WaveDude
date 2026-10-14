import re

def edit(path, pairs):
    s = open(path, encoding='utf-8').read()
    for old, new in pairs:
        if old not in s:
            raise SystemExit('not found in ' + path + ': ' + old[:70])
        s = s.replace(old, new, 1)
    open(path, 'w', encoding='utf-8', newline='').write(s)

edit('WaveDude/Model/Probe.cs', [
('''	/// <summary>
	/// X of the probe pixel, or of the start of the line, in canvas pixels. While the graph box sits at the
	/// same x (<see cref="BoxX"/> equal), the box moves along; a box dragged elsewhere stays put
	/// (the user's rule, 2026-10-14).
	/// </summary>''', '''	/// <summary>
	/// X of the probe pixel, or of the start of the line, in canvas pixels. While the graph box is attached
	/// (<see cref="IsBoxAttached"/>: its anchor on this point) the box moves along; a box dragged away stays
	/// put (the user's rule, 2026-10-14: one attachment, not one per axis).
	/// </summary>'''),
('''		set
		{
			bool boxFollows = _boxX == _aX;
			if (SetProperty(ref _aX, value) && boxFollows)
			{
				BoxX = value;
			}
		}
	}

	/// <summary>Y of the probe pixel, or of the start of the line, in canvas pixels. The graph box follows it like <see cref="AX"/>.</summary>''', '''		set
		{
			bool attached = IsBoxAttached;
			if (SetProperty(ref _aX, value) && attached)
			{
				BoxX = value;
			}
		}
	}

	/// <summary>Y of the probe pixel, or of the start of the line, in canvas pixels. An attached graph box follows it like <see cref="AX"/>.</summary>'''),
('''		set
		{
			bool boxFollows = _boxY == _aY;
			if (SetProperty(ref _aY, value) && boxFollows)
			{
				BoxY = value;
			}
		}
	}''', '''		set
		{
			bool attached = IsBoxAttached;
			if (SetProperty(ref _aY, value) && attached)
			{
				BoxY = value;
			}
		}
	}

	/// <summary>
	/// Whether the graph box is attached to the probe: its anchor (<see cref="BoxX"/>, <see cref="BoxY"/>) sits on
	/// point A, so the box moves with the probe. The preview snaps a box dragged close to the probe onto it and
	/// detaches it when it is dragged away (2026-10-14).
	/// </summary>
	[XmlIgnore]
	[Browsable(false)]
	public bool IsBoxAttached => _boxX == _aX && _boxY == _aY;'''),
('''	/// <summary>Left edge of the graph box in canvas pixels; while it equals <see cref="AX"/>, the box moves with the probe.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box x")]
	[Description("Left edge of the graph box in canvas pixels; while it equals A x, the box moves with the probe.")]''',
 '''	/// <summary>Left edge of the graph box in canvas pixels; with the box on point A (<see cref="IsBoxAttached"/>) it moves with the probe.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box x")]
	[Description("Left edge of the graph box in canvas pixels. A box placed on the probe's point A moves with the probe; dragging it close snaps it on.")]'''),
('''	/// <summary>Top edge of the graph box in canvas pixels; while it equals <see cref="AY"/>, the box moves with the probe.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box y")]
	[Description("Top edge of the graph box in canvas pixels; while it equals A y, the box moves with the probe.")]''',
 '''	/// <summary>Top edge of the graph box in canvas pixels; with the box on point A (<see cref="IsBoxAttached"/>) it moves with the probe.</summary>
	[XmlAttribute]
	[DefaultValue(0)]
	[Category("Graph")]
	[DisplayName("Box y")]
	[Description("Top edge of the graph box in canvas pixels. A box placed on the probe's point A moves with the probe; dragging it close snaps it on.")]'''),
])

edit('WaveDude.WPF/Controls/ProbeGraphOverlay.cs', [
('''	/// <summary>Pixels a focused box moves or grows per arrow key, and with Shift.</summary>
	private const int KeyStep = 1;
	private const int KeyStepFast = 10;''', '''	/// <summary>Pixels a focused box moves or grows per arrow key, and with Shift.</summary>
	private const int KeyStep = 1;
	private const int KeyStepFast = 10;

	/// <summary>Screen distance within which a dragged box snaps onto its probe's point A and attaches to it.</summary>
	private const double SnapRadius = 14.0;'''),
('''		else
		{
			SetBoxPosition(probe, x, y);
		}
	}

	/// <summary>Moves the box, kept inside the canvas.</summary>
	private void SetBoxPosition(Probe probe, int x, int y)
	{
		BoxRect box = ProbeGraph.BoxInCanvas(probe, CanvasWidth, CanvasHeight);
		probe.BoxX = Math.Clamp(x, 0, Math.Max(0, CanvasWidth - box.Width));
		probe.BoxY = Math.Clamp(y, 0, Math.Max(0, CanvasHeight - box.Height));
	}''', '''		else
		{
			SetBoxPosition(probe, x, y, snap: true);
		}
	}

	/// <summary>
	/// Moves the box, kept inside the canvas. With <paramref name="snap"/> (a drag, not a keyboard nudge) a box
	/// whose anchor comes within <see cref="SnapRadius"/> screen pixels of the probe's point A lands on it and is
	/// attached from then on (<see cref="Probe.IsBoxAttached"/>); dragging it further away detaches it. One radius,
	/// not a snap per axis (the user's rule, 2026-10-14).
	/// </summary>
	private void SetBoxPosition(Probe probe, int x, int y, bool snap)
	{
		if (snap)
		{
			double scale = ScaleFor(RenderSize);
			double dx = (x - probe.AX) * scale;
			double dy = (y - probe.AY) * scale;
			if (dx * dx + dy * dy <= SnapRadius * SnapRadius)
			{
				x = probe.AX;
				y = probe.AY;
			}
		}
		BoxRect box = ProbeGraph.BoxInCanvas(probe, CanvasWidth, CanvasHeight);
		probe.BoxX = Math.Clamp(x, 0, Math.Max(0, CanvasWidth - box.Width));
		probe.BoxY = Math.Clamp(y, 0, Math.Max(0, CanvasHeight - box.Height));
	}'''),
('''			SetBoxPosition(probe, probe.BoxX + dx, probe.BoxY + dy);''', '''			SetBoxPosition(probe, probe.BoxX + dx, probe.BoxY + dy, snap: false);'''),
])

hints = {
 'EN': ('Left edge of the graph box in canvas pixels. A box placed on the probe\'s point A moves with the probe; dragging it close snaps it on.',
        'Top edge of the graph box in canvas pixels. A box placed on the probe\'s point A moves with the probe; dragging it close snaps it on.'),
 'DE': ('Linker Rand des Diagrammkastens in Leinwandpixeln. Ein Kasten auf Punkt A der Sonde wandert mit ihr; in die Nähe gezogen rastet er dort ein.',
        'Oberer Rand des Diagrammkastens in Leinwandpixeln. Ein Kasten auf Punkt A der Sonde wandert mit ihr; in die Nähe gezogen rastet er dort ein.'),
 'FR': ('Bord gauche du cadre du graphique en pixels de toile. Un cadre posé sur le point A de la sonde la suit ; approché en le glissant, il s\'y accroche.',
        'Bord supérieur du cadre du graphique en pixels de toile. Un cadre posé sur le point A de la sonde la suit ; approché en le glissant, il s\'y accroche.'),
 'NL': ('Linkerrand van het grafiekkader in canvaspixels. Een kader op punt A van de sonde beweegt met haar mee; dichtbij gesleept klikt het daar vast.',
        'Bovenrand van het grafiekkader in canvaspixels. Een kader op punt A van de sonde beweegt met haar mee; dichtbij gesleept klikt het daar vast.'),
}
for lang, (x, y) in hints.items():
    path = 'WaveDude.WPF/Strings/Strings.%s.xaml' % lang
    s = open(path, encoding='utf-8').read()
    s, n1 = re.subn(r'(<s:String x:Key="Hint.Probe.BoxX">)[^<]*(</s:String>)', lambda m: m.group(1) + x + m.group(2), s)
    s, n2 = re.subn(r'(<s:String x:Key="Hint.Probe.BoxY">)[^<]*(</s:String>)', lambda m: m.group(1) + y + m.group(2), s)
    if n1 != 1 or n2 != 1:
        raise SystemExit('hint not found in ' + path)
    open(path, 'w', encoding='utf-8', newline='').write(s)

edit('_tools/library_check/Program.cs', [
('''		var follower = new Probe { Name = "F" };
		follower.AX = 50;
		follower.AY = 60;
		Check(follower.BoxX == 50 && follower.BoxY == 60, "the graph box moves with the probe while they share its position");
		follower.BoxX = 10;
		follower.AX = 70;
		follower.AY = 61;
		Check(follower.BoxX == 10 && follower.BoxY == 61, "a box moved away stays put on that axis; the other axis still follows");''',
 '''		var follower = new Probe { Name = "F" };
		follower.AX = 50;
		follower.AY = 60;
		Check(follower.IsBoxAttached && follower.BoxX == 50 && follower.BoxY == 60, "an attached graph box moves with the probe");
		follower.BoxX = 10;
		follower.AX = 70;
		follower.AY = 61;
		Check(!follower.IsBoxAttached && follower.BoxX == 10 && follower.BoxY == 60, "a box moved away is detached on both axes and stays put");
		follower.BoxX = 70;
		follower.BoxY = 61;
		follower.AX = 75;
		Check(follower.IsBoxAttached && follower.BoxX == 75 && follower.BoxY == 61, "a box put back on the probe is attached again");'''),
])

edit('CLAUDE.md', [
('''**The box follows the
  probe while they share a position** (the user's rule): `Probe.AX`/`AY` move `BoxX`/`BoxY` along when they
  were equal (in the model, so every editor does it), and a box dragged elsewhere stays put per axis.''',
 '''**The box follows the
  probe while it is attached** (the user's rule, one attachment, not one per axis, 2026-10-14):
  `Probe.IsBoxAttached` is the box anchor sitting on point A; `Probe.AX`/`AY` then move `BoxX`/`BoxY` along
  (in the model, so every editor does it). Dragging the box within 14 screen pixels of point A snaps it on
  (`ProbeGraphOverlay.SetBoxPosition`, drags only; keyboard nudges stay exact), dragging it further away
  detaches it.'''),
])
print('ok')

# Drives the WPF preview pane with keystrokes posted to its window (WM_KEYDOWN/WM_CHAR) and reads
# the result back through UI Automation. Nothing here activates the app window or uses the mouse,
# so it can run while the machine is in use. UI Automation *patterns* (Invoke, SetValue, Select)
# are deliberately not used: on this app each one focuses its target and brings the window to the
# foreground. Prints PASS/FAIL per check and exits with the failure count.
#
#   powershell -ExecutionPolicy Bypass -File _tools\ui_check\PreviewCheck.ps1 [-Exe <path>] [-Shot <png>] [-Trace]

param(
	[string]$Exe = (Join-Path $PSScriptRoot "..\..\WaveDude.WPF\bin\Debug\net10.0-windows\WaveDude.WPF.exe"),
	[string]$Shot = (Join-Path $env:TEMP "wavedude_preview_check.png"),
	[switch]$Trace   # append foreground state and focused element to every line
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Native
{
	[DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
	[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
	[DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
	[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

	// PW_RENDERFULLCONTENT: ask DWM for the composed content, which a hardware-rendered WPF window needs
	public static void Capture(IntPtr hwnd, string path)
	{
		RECT r; GetWindowRect(hwnd, out r);
		using (var bmp = new Bitmap(r.Right - r.Left, r.Bottom - r.Top))
		{
			using (var g = Graphics.FromImage(bmp))
			{
				IntPtr hdc = g.GetHdc();
				PrintWindow(hwnd, hdc, 2);
				g.ReleaseHdc(hdc);
			}
			bmp.Save(path, ImageFormat.Png);
		}
	}

	// WM_KEYDOWN / WM_KEYUP posted straight to the window: no SendInput, no focus change
	public static void Key(IntPtr hwnd, int vk)
	{
		PostMessage(hwnd, 0x0100, (IntPtr)vk, (IntPtr)1);
		PostMessage(hwnd, 0x0101, (IntPtr)vk, (IntPtr)(1 | (1 << 30) | (1 << 31)));
	}

	// WM_CHAR: text input independent of the keyboard layout
	public static void Char(IntPtr hwnd, char c) { PostMessage(hwnd, 0x0102, (IntPtr)c, (IntPtr)1); }
}
"@

$A = [System.Windows.Automation.AutomationElement]
$VK_BACK = 0x08; $VK_TAB = 0x09; $VK_SPACE = 0x20; $VK_END = 0x23; $VK_HOME = 0x24
$VK_LEFT = 0x25; $VK_RIGHT = 0x27; $VK_PRIOR = 0x21; $VK_RETURN = 0x0D; $VK_ESCAPE = 0x1B; $VK_F4 = 0x73
$script:failures = 0

function Check([string]$name, [bool]$ok, [string]$detail = "") {
	$tag = if ($ok) { "PASS" } else { $script:failures++; "FAIL" }
	if ($Trace) { $detail += ("   [fg={0} focus={1}]" -f (Foreground), (FocusedId)) }
	Write-Output ("{0}  {1}  {2}" -f $tag, $name, $detail)
}
function Foreground() {
	$fg = [Native]::GetForegroundWindow()
	if ($fg -eq $hwnd) { "APP" } elseif ($fg -eq $park.Handle) { "harness" } else { "other" }
}
function Find([string]$id) {
	$c = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
	$e = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
	if ($e -eq $null) { throw "element '$id' not found" }
	return $e
}
function Pattern($e, [string]$name) {
	$type = [System.Windows.Automation.InvokePattern].Assembly.GetType("System.Windows.Automation." + $name + "Pattern", $true)
	return $e.GetCurrentPattern($type.GetField("Pattern").GetValue($null))
}
function Frame() { return (Find "PreviewFrame").Current.Name }
function Position() { return [int](((Frame) -split " / ")[0]) }
function Playing() { return (Find "PreviewPause").Current.IsEnabled }
function Selected([string]$id) { return (Pattern (Find $id) "SelectionItem").Current.IsSelected }
function SpeedText() { return (Pattern (Find "PreviewSpeed") "Value").Current.Value }
function Focused([string]$id) { return (Find $id).Current.HasKeyboardFocus }
function FocusedId() {
	foreach ($id in "PreviewPlay", "PreviewPause", "PreviewScrub", "PreviewSpeed", "PreviewSideBySide", "PreviewStacked", "PreviewSplitter") {
		if (Focused $id) { return $id }
	}
	# the editable combo keeps focus in its inner text box
	$c = New-Object System.Windows.Automation.PropertyCondition($A::HasKeyboardFocusProperty, $true)
	if ((Find "PreviewSpeed").FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c) -ne $null) { return "PreviewSpeed" }
	$other = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
	if ($other -ne $null) { return "(other: " + $other.Current.ControlType.ProgrammaticName + " '" + $other.Current.Name + "')" }
	return "(none)"
}
function Key([int]$vk, [int]$settle = 150) { [Native]::Key($hwnd, $vk); Start-Sleep -Milliseconds $settle }
# Tab (posted) until the wanted element holds focus; the cycle runs through the whole window
function FocusOn([string]$id) {
	for ($i = 0; $i -lt 60; $i++) {
		if ((FocusedId) -eq $id) { return }
		Key $VK_TAB
	}
	throw "could not reach '$id' with Tab; focus is at $(FocusedId)"
}

# ---------------------------------------------------------------- launch
$p = Start-Process -FilePath $Exe -PassThru
$win = $null
for ($i = 0; $i -lt 100 -and $win -eq $null; $i++) {
	Start-Sleep -Milliseconds 200
	$c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)
	$win = $A::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
}
if ($win -eq $null) { throw "main window not found" }
$p.Refresh()
$hwnd = $p.MainWindowHandle
Start-Sleep -Milliseconds 800

# Windows may hand a new window the foreground. Park it on an off-screen window of our own (when
# the user is busy elsewhere this silently fails and the foreground stays theirs, which is just as
# good): every check below must then leave the app inactive.
$park = New-Object System.Windows.Forms.Form
$park.StartPosition = "Manual"
$park.Location = New-Object System.Drawing.Point(-3000, -3000)
$park.Size = New-Object System.Drawing.Size(10, 10)
$park.ShowInTaskbar = $false
$park.Show(); $park.Activate(); [System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 500
Write-Output ("window: '{0}'  foreground: {1}" -f $win.Current.Name, (Foreground))

try {
	Check "initial readout" ((Frame) -eq "0 / 1249") (Frame)
	Check "play enabled, pause disabled" ((Find "PreviewPlay").Current.IsEnabled -and -not (Playing))

	# ------------------------------------------------------------ transport
	Write-Output ("focus before any key: {0}" -f (FocusedId))
	FocusOn "PreviewPlay"
	Check "Tab reaches play while the window stays inactive" (((FocusedId) -eq "PreviewPlay") -and ((Foreground) -ne "APP")) (FocusedId)
	$order = @()
	for ($i = 0; $i -lt 5; $i++) { Key $VK_TAB 250; $order += (FocusedId) }
	# pause is disabled while not playing and is skipped; the split group is one stop (its checked radio); the splitter; then the pane is left
	Check "tab order play > scrubber > speed > split > splitter > out of the pane" (($order[0..3] -join ",") -eq "PreviewScrub,PreviewSpeed,PreviewSideBySide,PreviewSplitter" -and $order[4] -like "(other*") ($order -join ",")

	FocusOn "PreviewPlay"
	Key $VK_SPACE 2000
	$pos = Position
	Check "Space on play: ~60 frames in 2 s at speed 1" ($pos -ge 45 -and $pos -le 75) "position $pos"
	Check "pause enabled while playing, focus handed to pause" ((Playing) -and -not (Find "PreviewPlay").Current.IsEnabled -and (Focused "PreviewPause")) (FocusedId)
	Key $VK_SPACE 300
	$held = Position
	Start-Sleep -Milliseconds 700
	Check "Space on pause: position holds, focus handed back to play" (-not (Playing) -and (Position) -eq $held -and (Focused "PreviewPlay")) ("position {0}, focus {1}" -f $held, (FocusedId))

	# ------------------------------------------------------------ speed
	FocusOn "PreviewSpeed"
	# the drop-down lists the presets in the user's locale ("0,25" where the decimal separator is a comma)
	$quarter = (0.25).ToString([System.Globalization.CultureInfo]::CurrentCulture)
	Key $VK_F4 400
	$item = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $quarter)))
	Key $VK_ESCAPE 300
	Check "drop-down lists the presets in the current locale" ($item -ne $null -and $item.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) "looked for '$quarter'"
	$onePointFive = (1.5).ToString([System.Globalization.CultureInfo]::CurrentCulture)
	for ($i = 0; $i -lt 6; $i++) { Key $VK_BACK 30 }
	foreach ($ch in $onePointFive.ToCharArray()) { [Native]::Char($hwnd, $ch) }
	Start-Sleep -Milliseconds 300
	Check "typed 1.5 in the locale's spelling is accepted and shown as typed" ((SpeedText) -eq $onePointFive) ("typed '{0}', shows '{1}'" -f $onePointFive, (SpeedText))
	for ($i = 0; $i -lt 6; $i++) { Key $VK_BACK 30 }
	[Native]::Char($hwnd, [char]"4"); Start-Sleep -Milliseconds 300
	Check "typed speed 4 accepted" ((SpeedText) -eq "4") (SpeedText)
	Key $VK_SPACE 300
	Check "Space in the speed box neither plays nor types" (-not (Playing) -and (SpeedText) -eq "4") (SpeedText)
	$before = Position
	FocusOn "PreviewPlay"
	Key $VK_SPACE 2000
	Key $VK_SPACE 300   # focus was handed to pause
	$delta = (Position) - $before
	Check "speed 4: ~240 frames in 2 s" ($delta -ge 190 -and $delta -le 290) "delta $delta"

	# ------------------------------------------------------------ scrubber
	FocusOn "PreviewScrub"
	Key $VK_END 300
	Check "scrubber End goes to the last frame" ((Frame) -eq "1249 / 1249") (Frame)
	Key $VK_HOME 200
	Key $VK_RIGHT; Key $VK_RIGHT 300
	Check "scrubber Home then Right twice gives frame 2" ((Position) -eq 2) (Frame)
	Key $VK_PRIOR 300
	Check "scrubber PageUp steps 10 frames" ((Position) -eq 12) (Frame)
	Key $VK_SPACE 1000
	$playing = Playing
	Key $VK_SPACE 300
	Check "Space on the scrubber starts playback, Space again pauses" ($playing -and -not (Playing)) (Frame)

	# ------------------------------------------------------------ split
	FocusOn "PreviewSideBySide"
	Key $VK_RIGHT 400
	Check "Right arrow focuses and selects the stacked radio" ((Focused "PreviewStacked") -and (Selected "PreviewStacked") -and -not (Selected "PreviewSideBySide")) (FocusedId)
	# stacked: the intensity picture sits below the field picture, both centred on the same axis
	$fr = (Find "PreviewFieldImage").Current.BoundingRectangle; $ir = (Find "PreviewIntensityImage").Current.BoundingRectangle
	Check "stacked layout: intensity picture below the field picture, same left" ($ir.Top -gt $fr.Bottom -and [math]::Abs($ir.Left - $fr.Left) -lt 2) ("field {0},{1} intensity {2},{3}" -f $fr.Left, $fr.Top, $ir.Left, $ir.Top)
	$cell = (Find "PreviewSplitter").Current.BoundingRectangle
	Check "stacked layout: splitter is a horizontal bar between them" ($cell.Width -gt $cell.Height -and $cell.Top -ge $fr.Bottom -and $cell.Bottom -le $ir.Top) ("splitter {0}x{1} at {2},{3}" -f $cell.Width, $cell.Height, $cell.Left, $cell.Top)
	[Native]::Capture($hwnd, $Shot)
	Write-Output "screenshot: $Shot"
	# Tab must enter the group at the checked radio, which is now the second one
	FocusOn "PreviewScrub"
	Key $VK_TAB; Key $VK_TAB 300
	Check "Tab enters the split group at the checked (stacked) radio" (Focused "PreviewStacked") (FocusedId)
	Key $VK_LEFT 400
	$fr = $field.Current.BoundingRectangle; $ir = $intensity.Current.BoundingRectangle
	$fr = (Find "PreviewFieldImage").Current.BoundingRectangle; $ir = (Find "PreviewIntensityImage").Current.BoundingRectangle
	Check "Left arrow selects side by side again, pictures on one row, same top" ((Selected "PreviewSideBySide") -and (Focused "PreviewSideBySide") -and [math]::Abs($ir.Top - $fr.Top) -lt 2 -and $ir.Left -gt $fr.Right) ("field {0},{1} intensity {2},{3}" -f $fr.Left, $fr.Top, $ir.Left, $ir.Top)

	# ------------------------------------------------------------ splitter
	FocusOn "PreviewSplitter"
	$fw0 = (Find "PreviewFieldImage").Current.BoundingRectangle.Width
	Key $VK_RIGHT; Key $VK_RIGHT; Key $VK_RIGHT 400
	$fw1 = (Find "PreviewFieldImage").Current.BoundingRectangle.Width
	Check "Right arrow on the splitter widens the field picture" ($fw1 -gt $fw0 + 20) ("width {0} -> {1}" -f $fw0, $fw1)
	Key $VK_RETURN 400
	$fw2 = (Find "PreviewFieldImage").Current.BoundingRectangle.Width
	$iw2 = (Find "PreviewIntensityImage").Current.BoundingRectangle.Width
	Check "Enter on the splitter resets to half" ([math]::Abs($fw2 - $iw2) -lt 2 -and [math]::Abs($fw2 - $fw0) -lt 2) ("field {0} intensity {1}" -f $fw2, $iw2)

	Check "the app never took the foreground" ((Foreground) -ne "APP") (Foreground)
}
finally {
	$p.CloseMainWindow() | Out-Null
	if (-not $p.WaitForExit(5000)) { $p.Kill() }
	$park.Close()
	Write-Output ("{0} failure(s)" -f $script:failures)
}
exit $script:failures

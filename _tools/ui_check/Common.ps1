# Shared part of the UI harnesses (dot-source it): the native helpers, UI Automation lookups and the
# launch/park/stop logic. Keystrokes are posted to the app window (WM_KEYDOWN/WM_CHAR), results are
# read back through UI Automation *properties*. Nothing here activates the app window or uses the
# mouse, so a harness can run while the machine is in use. UI Automation *patterns* (Invoke,
# SetValue, Select, SetFocus) are deliberately not called: on this app each one focuses its target
# and brings the window to the foreground. Modifier keys cannot be posted either (WPF reads Shift/
# Ctrl/Alt from the real keyboard state), so every shortcut a harness needs must have an unmodified key.
#
# Expects the caller to have declared $Trace (switch) before dot-sourcing; sets $script:p, $win, $hwnd, $park.

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
	[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
	[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

	// PW_RENDERFULLCONTENT: ask DWM for the composed content, which a hardware-rendered WPF window needs
	static Bitmap Grab(IntPtr hwnd, out RECT r)
	{
		GetWindowRect(hwnd, out r);
		var bmp = new Bitmap(r.Right - r.Left, r.Bottom - r.Top);
		using (var g = Graphics.FromImage(bmp))
		{
			IntPtr hdc = g.GetHdc();
			PrintWindow(hwnd, hdc, 2);
			g.ReleaseHdc(hdc);
		}
		return bmp;
	}

	public static void Capture(IntPtr hwnd, string path)
	{
		RECT r;
		using (var bmp = Grab(hwnd, out r)) bmp.Save(path, ImageFormat.Png);
	}

	// MD5 of a screen-rectangle crop of the window's content, for "the picture changed" checks
	public static string Hash(IntPtr hwnd, int x, int y, int w, int h)
	{
		RECT r;
		using (var bmp = Grab(hwnd, out r))
		using (var crop = bmp.Clone(new Rectangle(x - r.Left, y - r.Top, w, h), bmp.PixelFormat))
		using (var ms = new System.IO.MemoryStream())
		{
			crop.Save(ms, ImageFormat.Bmp);
			using (var md5 = System.Security.Cryptography.MD5.Create())
				return BitConverter.ToString(md5.ComputeHash(ms.ToArray()));
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
$VK_LEFT = 0x25; $VK_RIGHT = 0x27; $VK_PRIOR = 0x21; $VK_NEXT = 0x22; $VK_RETURN = 0x0D; $VK_ESCAPE = 0x1B
$VK_F4 = 0x73; $VK_F5 = 0x74; $VK_F6 = 0x75
$VK_F = 0x46; $VK_I = 0x49; $VK_O = 0x4F; $VK_X = 0x58; $VK_L = 0x4C; $VK_COMMA = 0xBC; $VK_PERIOD = 0xBE
$script:failures = 0

function Check([string]$name, [bool]$ok, [string]$detail = "") {
	$tag = if ($ok) { "PASS" } else { $script:failures++; "FAIL" }
	if ($Trace) { $detail += ("   [fg={0} focus={1}]" -f (Foreground), (FocusedId)) }
	Write-Output ("{0}  {1}  {2}" -f $tag, $name, $detail)
}
function Foreground() {
	$fg = [Native]::GetForegroundWindow()
	if ($fg -eq $script:hwnd) { "APP" } elseif ($fg -eq $script:park.Handle) { "harness" } else { "other" }
}
# UI Automation lookups can miss while the UI thread is busy, so retry
function Find([string]$id) {
	$c = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
	for ($i = 0; $i -lt 10; $i++) {
		$e = $script:win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
		if ($e -ne $null) { return $e }
		Start-Sleep -Milliseconds 200
	}
	throw "element '$id' not found"
}
# a pattern object, used only to *read* its Current properties (never call its methods)
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
function StepText() { return (Find "PreviewStep").Current.Name }
function ToggleOn([string]$id) { return (Pattern (Find $id) "Toggle").Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On }
function LoopOn() { return ToggleOn "PreviewLoop" }
function LiveOn() { return ToggleOn "PreviewLive" }
function RunStatus() { return (Find "StatusRun").Current.Name }
# MD5 of the field picture as drawn (overlay included), for "the picture changed" checks
function FieldHash() {
	$r = (Find "PreviewFieldImage").Current.BoundingRectangle
	return [Native]::Hash($script:hwnd, [int]$r.Left, [int]$r.Top, [int]$r.Width, [int]$r.Height)
}
# the centre of an element's screen rectangle
function CentreOf([string]$id) { $r = (Find $id).Current.BoundingRectangle; return @(($r.Left + $r.Width / 2), ($r.Top + $r.Height / 2)) }
# where canvas pixel (x, y) of a w x h canvas lands inside a picture element (uniform scale, centred), as the overlay maps it
function CanvasPoint([string]$imageId, [int]$w, [int]$h, [double]$x, [double]$y) {
	$r = (Find $imageId).Current.BoundingRectangle
	$scale = [math]::Min($r.Width / $w, $r.Height / $h)
	$ox = $r.Left + ($r.Width - $w * $scale) / 2; $oy = $r.Top + ($r.Height - $h * $scale) / 2
	return @(($ox + ($x + 0.5) * $scale), ($oy + ($y + 0.5) * $scale), $scale)
}
# whether the tree item with this name is selected (reads the SelectionItem pattern's property only)
function TreeItemSelected([string]$name) {
	$c = New-Object System.Windows.Automation.AndCondition(
		(New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::TreeItem)),
		(New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $name)))
	$e = $script:win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
	if ($e -eq $null) { return $false }
	return (Pattern $e "SelectionItem").Current.IsSelected
}
function FocusedId() {
	foreach ($id in "PreviewPlay", "PreviewPause", "PreviewStepBack", "PreviewStepForward", "PreviewSetIn", "PreviewSetOut", "PreviewClearRange", "PreviewLoop",
	                "PreviewLive", "PreviewSpeed", "PreviewSideBySide", "PreviewStacked", "PreviewTimeline", "PreviewSplitter") {
		if (Focused $id) { return $id }
	}
	# the editable combo keeps focus in its inner text box
	$c = New-Object System.Windows.Automation.PropertyCondition($A::HasKeyboardFocusProperty, $true)
	if ((Find "PreviewSpeed").FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c) -ne $null) { return "PreviewSpeed" }
	$other = $script:win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
	if ($other -ne $null) { return "(other: " + $other.Current.ControlType.ProgrammaticName + " '" + $other.Current.Name + "')" }
	# nothing in the window reports focus: ask the system (only meaningful while the app is foreground)
	$fe = $A::FocusedElement
	if ($fe -ne $null -and $fe.Current.ProcessId -eq $script:p.Id) { return "(focused: " + $fe.Current.ControlType.ProgrammaticName + " '" + $fe.Current.Name + "' " + $fe.Current.ClassName + ")" }
	return "(none)"
}
function Key([int]$vk, [int]$settle = 150) { [Native]::Key($script:hwnd, $vk); Start-Sleep -Milliseconds $settle }
# Tab (posted) until the wanted element holds focus; the cycle runs through the whole window
function FocusOn([string]$id) {
	for ($i = 0; $i -lt 80; $i++) {
		if ((Focused $id) -or (FocusedId) -eq $id) { return }
		Key $VK_TAB
	}
	throw "could not reach '$id' with Tab; focus is at $(FocusedId)"
}
# polls a condition; returns whether it became true within the timeout
function WaitFor([scriptblock]$condition, [int]$timeoutMs = 10000, [int]$pollMs = 100) {
	$deadline = [DateTime]::Now.AddMilliseconds($timeoutMs)
	while ([DateTime]::Now -lt $deadline) {
		if (& $condition) { return $true }
		Start-Sleep -Milliseconds $pollMs
	}
	return (& $condition)
}

# Launches the app (optionally with a project file), finds its window and parks the foreground elsewhere.
# Windows may hand a new window the foreground. Park it on an off-screen window of our own (when the user
# is busy elsewhere this silently fails and the foreground stays theirs, which is just as good): every
# check must then leave the app inactive.
function Start-App([string]$Exe, [string]$ProjectFile = "") {
	if ($ProjectFile -ne "") { $script:p = Start-Process -FilePath $Exe -ArgumentList ('"' + $ProjectFile + '"') -PassThru }
	else { $script:p = Start-Process -FilePath $Exe -PassThru }
	$script:win = $null
	for ($i = 0; $i -lt 100 -and $script:win -eq $null; $i++) {
		Start-Sleep -Milliseconds 200
		$c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $script:p.Id)
		$script:win = $A::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
	}
	if ($script:win -eq $null) { throw "main window not found" }
	$script:p.Refresh()
	$script:hwnd = $script:p.MainWindowHandle
	Start-Sleep -Milliseconds 800

	$script:park = New-Object System.Windows.Forms.Form
	$script:park.StartPosition = "Manual"
	$script:park.Location = New-Object System.Drawing.Point(-3000, -3000)
	$script:park.Size = New-Object System.Drawing.Size(10, 10)
	$script:park.ShowInTaskbar = $false
	$script:park.Show(); $script:park.Activate(); [System.Windows.Forms.Application]::DoEvents()
	Start-Sleep -Milliseconds 500
	for ($i = 0; $i -lt 3 -and (Foreground) -eq "APP"; $i++) {
		[Native]::ShowWindow($script:hwnd, 6) | Out-Null         # SW_MINIMIZE: the OS activates something else
		Start-Sleep -Milliseconds 300
		$script:park.Activate(); [System.Windows.Forms.Application]::DoEvents()
		Start-Sleep -Milliseconds 300
		[Native]::ShowWindow($script:hwnd, 4) | Out-Null         # SW_SHOWNOACTIVATE
		Start-Sleep -Milliseconds 500
	}
	Write-Output ("window: '{0}'  foreground: {1}" -f $script:win.Current.Name, (Foreground))
}

function Stop-App() {
	if ($script:p -ne $null) {
		$script:p.CloseMainWindow() | Out-Null
		if (-not $script:p.WaitForExit(8000)) { $script:p.Kill() }
		$script:p = $null
	}
	if ($script:park -ne $null) { $script:park.Close(); $script:park = $null }
}

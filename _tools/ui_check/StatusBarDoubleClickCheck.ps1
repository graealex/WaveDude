# UI harness: double-clicking the status bar opens the Messages pane when hidden and focuses
# it when shown. Hides the pane first via View > Panes if needed.
param(
	[string]$Dir = (Resolve-Path (Join-Path $PSScriptRoot "..\..\WaveDude.WPF\bin\Debug\net10.0-windows")).Path
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
	[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
	[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
}
"@
$A = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function ByName($scope, $name) { $scope.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $name))) }
function ById($scope, $id) { $scope.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id))) }
function Expand($e) { $e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 600 }
function DoubleClickStatus {
	$r = (ById $win "txtStatus").Current.BoundingRectangle
	[W]::SetCursorPos([int]($r.Left + 20), [int]($r.Top + $r.Height / 2)) | Out-Null
	foreach ($i in 1..2) { [W]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [W]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80 }
	Start-Sleep -Seconds 2
}
function PaneShown { (ByName $win "Search") -ne $null }
function Focused {
	$f = $A::FocusedElement
	$f.Current.ControlType.ProgrammaticName + " '" + $f.Current.Name.Substring(0, [Math]::Min(40, $f.Current.Name.Length)) + "'"
}

$p = Start-Process -FilePath (Join-Path $Dir "WaveDude.WPF.exe") -PassThru
Start-Sleep -Seconds 5
$p.Refresh()
[W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
$win = $A::RootElement.FindFirst($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id)))

if (PaneShown) {
	Expand (ByName $win "View")
	Expand (ByName $win "Panes")
	(ByName $win "Messages").GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
	Start-Sleep -Seconds 1
}
"before:        pane shown = " + (PaneShown)
DoubleClickStatus
"after 1st dbl: pane shown = " + (PaneShown) + ", focus = " + (Focused)
(ById $win "txtStatus") | Out-Null
(ByName $win "Search").SetFocus()
Start-Sleep -Milliseconds 500
"focus moved:   " + (Focused)
DoubleClickStatus
"after 2nd dbl: pane shown = " + (PaneShown) + ", focus = " + (Focused)

$null = $p.CloseMainWindow()
if (-not $p.WaitForExit(5000)) { Stop-Process -Id $p.Id -Force -Confirm:$false }
"exit: " + $p.ExitCode

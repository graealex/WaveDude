using System.Windows.Shell;

namespace WaveDude.WPF;

/// <summary>
/// Mirrors the status bar's busy indicator on the window's taskbar button (the Windows 7 taskbar progress,
/// which WPF exposes as <see cref="TaskbarItemInfo"/>; the user's rule, 2026-10-14): an export writing frames
/// fills the button as the frames go, an export waiting for its run or a scratch frame being computed shows
/// the indeterminate marquee, and an idle application shows nothing. Fed by <see cref="UpdateBusy"/>. UI thread.
/// </summary>
public partial class MainWindow
{
	/// <summary>Puts the busy state on the taskbar button; the element is declared in the window's XAML.</summary>
	private void UpdateTaskbar()
	{
		if (TaskbarItemInfo is not { } taskbar)
		{
			return;
		}
		if (!IsBusy)
		{
			taskbar.ProgressState = TaskbarItemProgressState.None;
			taskbar.ProgressValue = 0;
		}
		else if (IsBusyIndeterminate)
		{
			taskbar.ProgressState = TaskbarItemProgressState.Indeterminate;
		}
		else
		{
			int total = Export?.FrameCount ?? 0;
			taskbar.ProgressValue = total > 0 ? Math.Clamp((double)(Export?.FramesDone ?? 0) / total, 0.0, 1.0) : 0.0;
			taskbar.ProgressState = TaskbarItemProgressState.Normal;
		}
	}
}

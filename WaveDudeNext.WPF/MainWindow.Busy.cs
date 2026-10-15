using System.Windows;
using WaveDudeNext.Export;

namespace WaveDudeNext.WPF;

/// <summary>
/// The status bar's busy indicator (2026-10-14, the user's rule: whenever the application computes something
/// the user waits for, a bar shows it): an export writing frames (determinate, frames done of the range), an
/// export waiting for its run (indeterminate, the timeline shows that run's progress), and a scratch frame
/// being computed for the preview after a scrub or an edit (indeterminate, "Computing frame n"). One text
/// and one bar, fed by <see cref="UpdateBusy"/> from wherever the state changes.
/// </summary>
public partial class MainWindow
{
	/// <summary>Identifies the <see cref="IsBusy"/> property.</summary>
	public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
		nameof(IsBusy), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="IsBusyIndeterminate"/> property.</summary>
	public static readonly DependencyProperty IsBusyIndeterminateProperty = DependencyProperty.Register(
		nameof(IsBusyIndeterminate), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="BusyText"/> property.</summary>
	public static readonly DependencyProperty BusyTextProperty = DependencyProperty.Register(
		nameof(BusyText), typeof(string), typeof(MainWindow), new PropertyMetadata(""));

	/// <summary>The scratch frame being computed for the preview, or -1.</summary>
	private int _scratchBusyFrame = -1;

	/// <summary>Whether the bar is shown.</summary>
	public bool IsBusy
	{
		get => (bool)GetValue(IsBusyProperty);
		private set => SetValue(IsBusyProperty, value);
	}

	/// <summary>Whether the bar has no measure of progress (a run being waited for, a scratch frame).</summary>
	public bool IsBusyIndeterminate
	{
		get => (bool)GetValue(IsBusyIndeterminateProperty);
		private set => SetValue(IsBusyIndeterminateProperty, value);
	}

	/// <summary>What the application is busy with, in the current language; empty when idle.</summary>
	public string BusyText
	{
		get => (string)GetValue(BusyTextProperty);
		private set => SetValue(BusyTextProperty, value);
	}

	/// <summary>Recomputes the indicator from the export and the scratch state. UI thread.</summary>
	private void UpdateBusy()
	{
		if (Export is { IsActive: true } job)
		{
			BusyText = string.Format((string)FindResource("ExportStatusFormat"), job.FramesDone, job.FrameCount);
			IsBusyIndeterminate = false;
			IsBusy = true;
		}
		else if (IsExportWaiting)
		{
			BusyText = (string)FindResource("ExportStatusWaiting");
			IsBusyIndeterminate = true;
			IsBusy = true;
		}
		else if (_scratchBusyFrame >= 0)
		{
			BusyText = string.Format((string)FindResource("StatusComputingFormat"), _scratchBusyFrame);
			IsBusyIndeterminate = true;
			IsBusy = true;
		}
		else
		{
			BusyText = "";
			IsBusyIndeterminate = false;
			IsBusy = false;
		}
		UpdateTaskbar();
	}

	/// <summary>A scratch frame was requested (or the request completed: -1).</summary>
	private void SetScratchBusy(int frame)
	{
		_scratchBusyFrame = frame;
		UpdateBusy();
	}

	/// <summary>Follows an export job's progress and state into the indicator.</summary>
	private void WatchExport(ExportJob job)
	{
		job.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName is nameof(ExportJob.FramesDone) or nameof(ExportJob.State) && ReferenceEquals(job, Export))
			{
				UpdateBusy();
			}
		};
	}
}

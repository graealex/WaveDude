// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Alexander Graef <alex@graef.cc>
//
// This file is part of WaveDude Next, a 2D wave simulator.
//
// Based on the work of
//   JJM Vleggaar (Huygens Optics), author of Wave Dude 2D, Copyright (C) 2026
//     <https://www.youtube.com/@HuygensOptics>, <https://www.huygensoptics.com>
//   Manuel (0x23, Diffraction Limited), author of WaveSimulator2D
//     <https://www.youtube.com/@DiffractionLimited>, <https://github.com/0x23/WaveSimulator2D>
//
// WaveDude Next is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY. See the LICENSE.md file or
// <https://www.gnu.org/licenses/>.

using System.Diagnostics;
using System.Windows.Threading;
using NLog;

namespace WaveDudeNext.WPF;

public partial class MainWindow
{
	/// <summary>How often the watchdog timers tick; a tick that comes much later than this is a stall.</summary>
	private const int WatchdogIntervalMs = 100;
	/// <summary>A tick this much later than due is a stall.</summary>
	private const int StallThresholdMs = 150;
	/// <summary>An episode this long is a warning (it reaches the status bar); shorter ones, the start-up compile among them, are Debug lines.</summary>
	private const double WarnEpisodeSeconds = 1.0;

	private readonly StallEpisode _normalStall = new("normal"), _inputStall = new("input");

	/// <summary>
	/// The UI thread watchdog (2026-10-16, after the user found the window unresponsive during a run): two timers,
	/// one at normal and one at input priority, each noting when its tick arrives more than <see cref="StallThresholdMs"/>
	/// late. A late normal tick means the thread itself was blocked (rendering waiting for a saturated GPU, a long
	/// handler, CPU starvation); a late input tick with the normal one on time means work above input priority is
	/// starving the input queue. Consecutive late ticks are one episode, logged as one line when the ticks are back
	/// on time: its length, the worst lateness, and the run's state when it began; an episode of a second or more is
	/// a warning (the user's call, 2026-10-16: a stall is a fault nobody would otherwise know about), shorter ones
	/// are Debug lines, so the start-up blip while the OpenCL engine compiles stays out of the status bar.
	/// </summary>
	private void StartUiWatchdog()
	{
		var normal = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(WatchdogIntervalMs) };
		normal.Tick += (_, _) => _normalStall.Tick(this);
		normal.Start();
		var input = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(WatchdogIntervalMs) };
		input.Tick += (_, _) => _inputStall.Tick(this);
		input.Start();
	}

	/// <summary>One priority's stall bookkeeping: ticks are timed, late ones gathered into an episode, the episode logged once.</summary>
	private sealed class StallEpisode(string priority)
	{
		private long _last = Stopwatch.GetTimestamp();
		private long _began;
		private double _worstMs, _sumMs;
		private int _lateTicks;
		private string _state = "";

		public void Tick(MainWindow window)
		{
			long now = Stopwatch.GetTimestamp();
			double lateMs = (now - _last) * 1000.0 / Stopwatch.Frequency - WatchdogIntervalMs;
			_last = now;
			if (lateMs > StallThresholdMs)
			{
				if (_lateTicks == 0)
				{
					_began = now;
					_worstMs = 0;
					_sumMs = 0;
					_state = $"run {window.Run?.State.ToString() ?? "none"}, scratch frame computing: {window.IsComputingFrame}";
				}
				_lateTicks++;
				_sumMs += lateMs;
				_worstMs = Math.Max(_worstMs, lateMs);
			}
			else if (_lateTicks > 0)
			{
				double seconds = (now - _began) / (double)Stopwatch.Frequency;
				Logger.Log(seconds >= WarnEpisodeSeconds ? LogLevel.Warn : LogLevel.Debug,
					"UI thread: {Priority}-priority ticks late for {Seconds:0.0} s ({Count} ticks, worst {Worst:0} ms, {Sum:0} ms in all; {State} when it began)",
					priority, seconds, _lateTicks, _worstMs, _sumMs, _state);
				_lateTicks = 0;
			}
		}
	}
}

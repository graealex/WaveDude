namespace WaveDude.Runs;

/// <summary>Where a <see cref="SimulationRun"/> is in its life.</summary>
public enum RunState
{
	/// <summary>Constructed, <see cref="SimulationRun.Start"/> not called yet.</summary>
	Created,
	/// <summary>The worker is building the scene and the engine.</summary>
	Starting,
	/// <summary>Stepping.</summary>
	Running,
	/// <summary>Parked at the pause gate; <see cref="SimulationRun.Resume"/> continues.</summary>
	Paused,
	/// <summary>Finished all steps.</summary>
	Completed,
	/// <summary>Stopped by <see cref="SimulationRun.Stop"/> before the last step.</summary>
	Cancelled,
	/// <summary>Ended with an error (<see cref="SimulationRun.Error"/>).</summary>
	Failed,
}

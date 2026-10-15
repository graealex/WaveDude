namespace WaveDudeNext.Probes;

/// <summary>
/// Everything recorded for one probe during a run: the time series (one value per step) and, for line probes,
/// one profile per recorded frame (so a scrubbed frame shows the profile of that instant, 2026-10-14).
/// Two independent sets exist per run: <see cref="ProbeCollector.Traces"/> is owned by the run's worker; the UI
/// keeps its own copies, filled from <see cref="ProbeUpdate"/> batches on the UI thread. An instance is never
/// shared between threads.
/// </summary>
public sealed class ProbeTrace
{
	/// <summary>Number of simulation steps completed when this trace was last updated; gates "start after step" line probes in the graph.</summary>
	public int StepsSampled { get; set; }

	/// <summary>Line-probe profiles (one value per pixel along the line, A to B), one per recorded frame in frame order; stays empty for point probes.</summary>
	public IReadOnlyList<float[]> Profiles => _profiles;

	/// <summary>The newest profile, or null until the first one arrives and always for point probes.</summary>
	public float[]? Profile => _profiles.Count == 0 ? null : _profiles[^1];

	/// <summary>Length of the probe line in canvas pixels (distance A to B); 0 for point probes. Used as the x-axis span of the profile graph.</summary>
	public double ProfileLength => _profileLength;

	/// <summary>Appends the profile of the next frame. The array is kept by reference, not copied, so callers must not modify it afterwards.</summary>
	/// <param name="values">Sampled values along the line, A to B.</param>
	/// <param name="length">Line length in canvas pixels.</param>
	public void AddProfile(float[] values, double length)
	{
		_profiles.Add(values);
		_profileLength = length;
	}

	/// <summary>The profiles in frame order.</summary>
	private readonly List<float[]> _profiles = [];

	/// <summary>Line length in canvas pixels belonging to the profiles.</summary>
	private double _profileLength;

	/// <summary>Time series of a point probe, one value per sampled step; stays empty for line probes.</summary>
	public ProbeSeries Series { get; } = new();
}

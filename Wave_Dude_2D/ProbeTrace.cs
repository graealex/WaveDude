namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Everything recorded for one probe during a run: the time series and, for line probes, the latest profile.
	/// Two independent sets exist per run: <see cref="ProbeCollector.Traces"/> is owned by the simulation worker
	/// thread (and read by <see cref="ProbeOverlay"/> for exported frames); the main form keeps its own copies,
	/// filled from <see cref="ProbeUpdate"/> batches on the UI thread and painted by <see cref="ProbeGraphPainter"/>.
	/// An instance is never shared between threads.
	/// </summary>
	internal sealed class ProbeTrace
	{
		public ProbeTrace()
		{
			this.Series = new ProbeSeries();
		}

		/// <summary>Number of simulation steps completed when this trace was last updated; used to gate "start after step" line probes in the painter.</summary>
		public int StepsSampled { get; set; }

		/// <summary>Latest line-probe profile (one value per pixel along the line, A to B), or null until the first profile arrives and always for point probes.</summary>
		public float[] Profile
		{
			get
			{
				return this._profile;
			}
		}

		/// <summary>Length of the probe line in canvas pixels (distance A to B); 0 for point probes. Used as the x-axis span of the profile graph.</summary>
		public double ProfileLength
		{
			get
			{
				return this._profileLength;
			}
		}

		/// <summary>Replaces the current profile. The array is kept by reference, not copied, so callers must not modify it afterwards.</summary>
		/// <param name="values">Sampled values along the line, A to B.</param>
		/// <param name="length">Line length in canvas pixels.</param>
		public void SetProfile(float[] values, double length)
		{
			this._profile = values;
			this._profileLength = length;
		}

		/// <summary>Latest profile array; null until <see cref="SetProfile"/> is first called.</summary>
		private float[] _profile;

		/// <summary>Line length in canvas pixels belonging to <see cref="_profile"/>.</summary>
		private double _profileLength;

		/// <summary>Time series of a point probe, one value per sampled step; stays empty for line probes.</summary>
		public readonly ProbeSeries Series;
	}
}

using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// One point source as the wave engines consume it: a resolved, per-element copy of the
	/// user's <see cref="SourceSettings"/> with all units already converted (phase in radians,
	/// amplitude 0..1). Produced by <see cref="SourceSettings.GenerateElements"/> on the UI
	/// thread, stored in the project file, and handed by reference to
	/// <c>SimulationJob.ObjectSources</c>, where <c>WaveSimulator</c> / <c>GpuWaveSimulator</c>
	/// read it from the <c>SimulationRunner</c> worker thread. The engines never write to it
	/// (they keep their own per-element off-width state), so it must simply not be mutated
	/// while a run is in progress. Generating rolls the randomisation once, so a run is
	/// reproducible: the engine never re-randomises the elements themselves.
	/// </summary>
	public sealed class SourceElement
	{
		/// <summary>Creates an element at (0, 0) with the fallback values: level 100, amplitude 0.5, no phase, continuous, 10-cycle on/off widths, never detached.</summary>
		public SourceElement()
		{
			Level = 100;
			Amplitude = 0.5;
			PhaseRad = 0.0;
			Mode = SourceMode.Continuous;
			PulseShape = SourcePulseShape.Gaussian;
			OnWidthCycles = 10;
			OffWidthCycles = 10;
			OffWidthBase = 10;
			RandomizeOffWidthAmount = 0;
			DetachAfterStep = -1;
		}

		/// <summary>X position in canvas pixels (0 = left edge). Elements outside the canvas are dropped by the engines.</summary>
		public int PosX { get; set; }

		/// <summary>Y position in canvas pixels (0 = top edge). Elements outside the canvas are dropped by the engines.</summary>
		public int PosY { get; set; }

		/// <summary>
		/// Frequency as a source level 1..255, the same scale as the brightness of image-based
		/// sources. The engines map it to an angular frequency of
		/// <c>Level / 255 * 0.5 * SimulationSettings.SourceFrequencyScale</c> radians per step.
		/// </summary>
		public int Level { get; set; }

		/// <summary>Peak field amplitude 0..1 (the settings' 0..100 % divided by 100).</summary>
		public double Amplitude { get; set; }

		/// <summary>Phase offset in radians added to the sine argument (<c>sin(PhaseRad + omega * t)</c>); positive values lead.</summary>
		public double PhaseRad { get; set; }

		/// <summary>Continuous or pulsed emission.</summary>
		public SourceMode Mode { get; set; }

		/// <summary>Envelope shape of each burst; only used when <see cref="Mode"/> is <see cref="SourceMode.Pulsed"/>.</summary>
		public SourcePulseShape PulseShape { get; set; }

		/// <summary>Length of the "on" burst in cycles of this element's own frequency (pulsed mode only).</summary>
		public int OnWidthCycles { get; set; }

		/// <summary>
		/// Length of the "off" gap in cycles (pulsed mode only). With a fixed gap
		/// (<see cref="RandomizeOffWidthAmount"/> is 0) the engines shift the period so the first
		/// burst starts at step 0 and the gap follows each burst. With a randomised gap the first
		/// period begins with a gap of this length before the first burst, and every later gap is
		/// re-drawn by the engine around <see cref="OffWidthBase"/>.
		/// </summary>
		public int OffWidthCycles { get; set; }

		/// <summary>Nominal off gap in cycles that the engine randomises around for every new period when <see cref="RandomizeOffWidthAmount"/> is positive.</summary>
		public int OffWidthBase { get; set; }

		/// <summary>Half-range in cycles of the per-period random off-width offset (gap = max(1, OffWidthBase +/- amount)); 0 keeps <see cref="OffWidthCycles"/> fixed.</summary>
		public int RandomizeOffWidthAmount { get; set; }

		/// <summary>
		/// Simulation step at which this element stops driving the field (releasing its hold on it, so it no
		/// longer diffracts waves passing through its position), fading out linearly over
		/// <see cref="DetachRampSteps"/> steps; -1 means it stays on for the whole run. Copied from the parent
		/// <see cref="SourceSettings.DetachSource"/> / <see cref="SourceSettings.DetachAfterSteps"/> at generate time.
		/// </summary>
		public int DetachAfterStep { get; set; }

		/// <summary>Shallow copy; sufficient because every property is a value type.</summary>
		public SourceElement Clone()
		{
			return (SourceElement)base.MemberwiseClone();
		}

		/// <summary>
		/// Number of simulation steps over which a detaching element's drive is faded out (linearly, down to 0)
		/// rather than cut instantly at <see cref="DetachAfterStep"/>: an abrupt cutoff otherwise leaves a small
		/// kink in the forcing at that point, which can ring. Shared by both engines
		/// (<c>WaveSimulator.ApplySources</c> and the <c>apply_object_sources</c> kernel in
		/// <c>GpuWaveSimulator</c>, which splices it into the kernel text), so the two stay in exact agreement.
		/// </summary>
		public const int DetachRampSteps = 30;
	}
}

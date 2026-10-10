using WaveDude.Model;

namespace WaveDude.Probes;

/// <summary>
/// What a run needs to know about one probe: an immutable copy of the measurement part of a
/// <see cref="Probe"/> (the graph box and scaling stay in the model). Built on the UI thread by
/// <see cref="From"/>, consumed by <see cref="ProbeSampler.Build"/> on the run's worker. Positions are in
/// canvas pixels, <see cref="StartAfter"/> in steps.
/// </summary>
/// <param name="Name">The probe's name, for labelling its trace.</param>
/// <param name="Kind">Point (A only) or line (A to B).</param>
/// <param name="Measurement">Field amplitude, intensity or intensity integrated over the run.</param>
/// <param name="AX">X of point A in canvas pixels.</param>
/// <param name="AY">Y of point A in canvas pixels.</param>
/// <param name="BX">X of point B in canvas pixels (line probes).</param>
/// <param name="BY">Y of point B in canvas pixels (line probes).</param>
/// <param name="StartAfter">Step from which an integrated probe records; 0 records from the first step.</param>
public sealed record ProbeSpec(
	string Name,
	ProbeKind Kind,
	ProbeMeasurement Measurement,
	int AX,
	int AY,
	int BX,
	int BY,
	int StartAfter)
{
	/// <summary>The spec of a model probe.</summary>
	public static ProbeSpec From(Probe probe) =>
		new(probe.Name, probe.Kind, probe.Measurement, probe.AX, probe.AY, probe.BX, probe.BY, probe.StartAfter);
}

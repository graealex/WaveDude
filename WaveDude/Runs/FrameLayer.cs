namespace WaveDude.Runs;

/// <summary>One plane of a stored frame.</summary>
public enum FrameLayer
{
	/// <summary>The wave field, signed amplitude.</summary>
	Field,
	/// <summary>The averaged intensity, non-negative.</summary>
	Intensity,
}

/// <summary>The set of planes a <see cref="FrameStore"/> holds per frame.</summary>
[Flags]
public enum FrameLayers
{
	/// <summary>No plane.</summary>
	None = 0,
	/// <summary><see cref="FrameLayer.Field"/>.</summary>
	Field = 1,
	/// <summary><see cref="FrameLayer.Intensity"/>.</summary>
	Intensity = 2,
}

using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// How the elements of a <see cref="SourceMainType.Composite"/> array derive their properties
	/// from the single-source settings. Ignored for simple sources. Stored in the project as
	/// <c>"inherit"</c> / <c>"randomize"</c>.
	/// </summary>
	public enum SourceCompositeMode
	{
		/// <summary>Every element is an exact copy of the single-source settings; only its position differs.</summary>
		InheritFromSingle,
		/// <summary>Elements inherit the settings, and each enabled "randomize" option (frequency, phase, off-width, position) adds an independent random offset per element when the elements are generated.</summary>
		InheritAndRandomize
	}
}

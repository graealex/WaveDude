using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The measured values of one point probe: a growable buffer with one value per simulation step, in step
	/// order (value i belongs to step i + 1). Not thread-safe: each instance belongs to one
	/// <see cref="ProbeTrace"/> and is only touched by that trace's owning thread.
	/// </summary>
	internal sealed class ProbeSeries
	{
		public ProbeSeries()
		{
			_values = new float[1024];
		}

		/// <summary>Number of valid values; only the first <see cref="Count"/> entries of <see cref="Values"/> are meaningful.</summary>
		public int Count => _count;

		/// <summary>
		/// Backing array, exposed without copying. Its length is the capacity, not the sample count, so read only
		/// indices below <see cref="Count"/>. The reference changes whenever the buffer grows. Do not change.
		/// </summary>
		public float[] Values => _values;

		/// <summary>Most recently added value, or NaN when the series is empty.</summary>
		public float LastValue => _count == 0 ? float.NaN : _values[_count - 1];

		/// <summary>Appends values in step order, at least doubling the backing array when it is full. Null or empty input is ignored.</summary>
		public void Add(float[] items)
		{
			if (items == null || items.Length == 0)
			{
				return;
			}
			int newCount = _count + items.Length;
			if (newCount > _values.Length)
			{
				float[] grown = new float[Math.Max(newCount, _values.Length * 2)];
				Array.Copy(_values, grown, _count);
				_values = grown;
			}
			Array.Copy(items, 0, _values, _count, items.Length);
			_count = newCount;
		}

		/// <summary>Discards all values but keeps the allocated storage.</summary>
		public void Clear()
		{
			_count = 0;
		}

		/// <summary>Backing storage; starts at 1024 entries, grows by at least doubling and never shrinks.</summary>
		private float[] _values;

		/// <summary>Number of valid entries at the start of <see cref="_values"/>.</summary>
		private int _count;
	}
}

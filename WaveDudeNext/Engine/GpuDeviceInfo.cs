namespace WaveDudeNext.Engine;

/// <summary>
/// Describes one OpenCL GPU device and enumerates the available ones. Produced by <see cref="FindAll"/> and
/// consumed by <see cref="GpuWaveSimulator"/>, which tries the devices in the returned order. The platform and
/// device handles are driver-owned identifiers that need no release.
/// </summary>
public sealed class GpuDeviceInfo
{
	/// <summary>
	/// Lists every GPU on every OpenCL platform, best candidate first: discrete devices before those sharing
	/// host memory, then by descending <see cref="Score"/>. Platforms whose enumeration fails are skipped;
	/// a failing platform query yields an empty list. Throws the usual P/Invoke exceptions
	/// (<see cref="DllNotFoundException"/> etc.) when OpenCL.dll cannot be loaded.
	/// </summary>
	public static List<GpuDeviceInfo> FindAll()
	{
		var devices = new List<GpuDeviceInfo>();
		uint platformCount = 0U;
		if (OpenClApi.clGetPlatformIDs(0U, null, ref platformCount) != 0 || platformCount == 0U)
		{
			return devices;
		}
		IntPtr[] platforms = new IntPtr[platformCount];
		uint platformsReturned = 0U;
		if (OpenClApi.clGetPlatformIDs(platformCount, platforms, ref platformsReturned) != 0)
		{
			return devices;
		}
		foreach (IntPtr platform in platforms)
		{
			uint deviceCount = 0U;
			if (OpenClApi.clGetDeviceIDs(platform, OpenClApi.DeviceTypeGpu, 0U, null, ref deviceCount) != 0 || deviceCount == 0U)
			{
				continue;
			}
			IntPtr[] deviceIds = new IntPtr[deviceCount];
			uint devicesReturned = 0U;
			if (OpenClApi.clGetDeviceIDs(platform, OpenClApi.DeviceTypeGpu, deviceCount, deviceIds, ref devicesReturned) != 0)
			{
				continue;
			}
			foreach (IntPtr deviceId in deviceIds)
			{
				devices.Add(GpuDeviceInfo.Describe(platform, deviceId));
			}
		}
		devices.Sort((a, b) =>
		{
			// discrete GPUs first, then by score descending
			if (a.Discrete != b.Discrete)
			{
				return a.Discrete ? -1 : 1;
			}
			return b.Score.CompareTo(a.Score);
		});
		return devices;
	}

	/// <summary>
	/// Queries the device properties. Failed queries read as 0 or "", so an unnamed device is called "GPU" and a
	/// device whose host-unified-memory flag cannot be read counts as discrete.
	/// </summary>
	private static GpuDeviceInfo Describe(IntPtr plat, IntPtr id)
	{
		string name = OpenClApi.DeviceString(id, OpenClApi.DeviceName);
		double computeUnits = OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxComputeUnits);
		double clockMhz = OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxClockFrequency);
		return new GpuDeviceInfo
		{
			PlatformId = plat,
			DeviceId = id,
			Name = name.Length == 0 ? "GPU" : name,
			Vendor = OpenClApi.DeviceString(id, OpenClApi.DeviceVendor),
			Discrete = OpenClApi.DeviceNumber(id, OpenClApi.DeviceHostUnifiedMemory) == 0UL,
			Score = computeUnits * Math.Max(1.0, clockMhz),
			// Mask the sign bit so an odd 64-bit value cannot turn into a negative size.
			GlobalMemory = (long)(OpenClApi.DeviceNumber(id, OpenClApi.DeviceGlobalMemSize) & 0x7FFFFFFFFFFFFFFFUL),
			MaxAllocation = (long)(OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxMemAllocSize) & 0x7FFFFFFFFFFFFFFFUL),
		};
	}

	/// <summary>OpenCL platform handle (cl_platform_id) the device belongs to.</summary>
	public IntPtr PlatformId { get; private init; }

	/// <summary>OpenCL device handle (cl_device_id).</summary>
	public IntPtr DeviceId { get; private init; }

	/// <summary>CL_DEVICE_NAME, or "GPU" when the query fails.</summary>
	public string Name { get; private init; } = "";

	/// <summary>CL_DEVICE_VENDOR, or "" when the query fails.</summary>
	public string Vendor { get; private init; } = "";

	/// <summary>True for a card with its own memory (the NVIDIA GeForce; CL_DEVICE_HOST_UNIFIED_MEMORY is 0), false for the graphics of the processor (Intel UHD).</summary>
	public bool Discrete { get; private init; }

	/// <summary>Compute units times clock (MHz, at least 1): a rough measure of the speed.</summary>
	public double Score { get; private init; }

	/// <summary>CL_DEVICE_GLOBAL_MEM_SIZE in bytes.</summary>
	public long GlobalMemory { get; private init; }

	/// <summary>CL_DEVICE_MAX_MEM_ALLOC_SIZE in bytes: the largest single buffer the device allows.</summary>
	public long MaxAllocation { get; private init; }
}

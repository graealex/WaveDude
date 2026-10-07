using System;
using System.Collections.Generic;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Describes one OpenCL GPU device and enumerates the available ones. Produced by <see cref="FindAll"/> and
	/// consumed by <see cref="GpuWaveSimulator"/>, which tries the devices in the returned order. The platform and
	/// device handles are driver-owned identifiers that need no release.
	/// </summary>
	internal sealed class GpuDeviceInfo
	{
		public GpuDeviceInfo()
		{
			this.Name = "";
			this.Vendor = "";
		}

		/// <summary>
		/// Lists every GPU on every OpenCL platform, best candidate first: discrete devices before those sharing
		/// host memory, then by descending <see cref="Score"/>. Platforms whose enumeration fails are skipped;
		/// a failing platform query yields an empty list. Throws the usual P/Invoke exceptions
		/// (<see cref="DllNotFoundException"/> etc.) when OpenCL.dll cannot be loaded.
		/// </summary>
		public static List<GpuDeviceInfo> FindAll()
		{
			List<GpuDeviceInfo> devices = new List<GpuDeviceInfo>();
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
			GpuDeviceInfo info = new GpuDeviceInfo();
			info.PlatformId = plat;
			info.DeviceId = id;
			info.Name = OpenClApi.DeviceString(id, OpenClApi.DeviceName);
			info.Vendor = OpenClApi.DeviceString(id, OpenClApi.DeviceVendor);
			info.Discrete = OpenClApi.DeviceNumber(id, OpenClApi.DeviceHostUnifiedMemory) == 0UL;
			double computeUnits = OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxComputeUnits);
			double clockMhz = OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxClockFrequency);
			info.Score = computeUnits * Math.Max(1.0, clockMhz);
			// Mask the sign bit so an odd 64-bit value cannot turn into a negative size.
			info.GlobalMemory = (long)(OpenClApi.DeviceNumber(id, OpenClApi.DeviceGlobalMemSize) & 0x7FFFFFFFFFFFFFFFUL);
			info.MaxAllocation = (long)(OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxMemAllocSize) & 0x7FFFFFFFFFFFFFFFUL);
			if (info.Name.Length == 0)
			{
				info.Name = "GPU";
			}
			return info;
		}

		/// <summary>OpenCL platform handle (cl_platform_id) the device belongs to.</summary>
		public IntPtr PlatformId;

		/// <summary>OpenCL device handle (cl_device_id).</summary>
		public IntPtr DeviceId;

		/// <summary>CL_DEVICE_NAME, or "GPU" when the query fails.</summary>
		public string Name;

		/// <summary>CL_DEVICE_VENDOR, or "" when the query fails.</summary>
		public string Vendor;

		/// <summary>True when the device does not share memory with the host (CL_DEVICE_HOST_UNIFIED_MEMORY is 0), i.e. a discrete card.</summary>
		public bool Discrete;

		/// <summary>Rough throughput rank: compute units × max clock in MHz (clock at least 1).</summary>
		public double Score;

		/// <summary>CL_DEVICE_GLOBAL_MEM_SIZE in bytes.</summary>
		public long GlobalMemory;

		/// <summary>CL_DEVICE_MAX_MEM_ALLOC_SIZE in bytes: the largest single buffer the device allows.</summary>
		public long MaxAllocation;
	}
}

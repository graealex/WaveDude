using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Thin P/Invoke layer over OpenCL.dll (the ICD loader installed with the graphics driver) plus a few query
	/// helpers. Used by <see cref="GpuDeviceInfo"/> and <see cref="GpuWaveSimulator"/>; every call returns an
	/// OpenCL error code (0 = CL_SUCCESS) that callers route through <see cref="Check"/>. Loading the DLL is
	/// deferred to the first call, so <see cref="DllNotFoundException"/> and friends surface there.
	/// </summary>
	internal sealed class OpenClApi
	{
		/// <summary>clGetPlatformIDs: with <paramref name="platforms"/> null only counts the platforms into <paramref name="numPlatforms"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clGetPlatformIDs(uint numEntries, IntPtr[] platforms, ref uint numPlatforms);

		/// <summary>clGetDeviceIDs: lists the devices of a platform matching <paramref name="deviceType"/> (<see cref="DeviceTypeGpu"/>); with <paramref name="devices"/> null only counts them.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clGetDeviceIDs(IntPtr platform, ulong deviceType, uint numEntries, IntPtr[] devices, ref uint numDevices);

		/// <summary>clGetDeviceInfo: raw bytes of a device property; with <paramref name="paramValue"/> null only reports the size in <paramref name="sizeRet"/>. Wrapped by <see cref="DeviceInfoBytes"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clGetDeviceInfo(IntPtr device, uint paramName, IntPtr paramSize, byte[] paramValue, ref IntPtr sizeRet);

		/// <summary>clCreateContext: creates a context for the given devices; <paramref name="properties"/> is a zero-terminated key/value list (see <see cref="ContextPlatformProperty"/>). Release with <see cref="clReleaseContext"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr clCreateContext(IntPtr[] properties, uint numDevices, IntPtr[] devices, IntPtr notify, IntPtr userData, ref int errcode);

		/// <summary>clCreateCommandQueue (OpenCL 1.x entry point): an in-order queue on one device. Release with <see cref="clReleaseCommandQueue"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr clCreateCommandQueue(IntPtr context, IntPtr device, ulong properties, ref int errcode);

		/// <summary>clCreateProgramWithSource: a program from ANSI source strings (<paramref name="lengths"/> null = zero-terminated). Release with <see cref="clReleaseProgram"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr clCreateProgramWithSource(IntPtr context, uint count, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] strings, IntPtr[] lengths, ref int errcode);

		/// <summary>clBuildProgram: compiles the program for the given devices; <paramref name="options"/> is passed as an ANSI string (VB-style by-ref marshalling, not modified). On failure see <see cref="BuildLog"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clBuildProgram(IntPtr program, uint numDevices, IntPtr[] devices, [MarshalAs(UnmanagedType.VBByRefStr)] ref string options, IntPtr notify, IntPtr userData);

		/// <summary>clGetProgramBuildInfo: raw bytes of a build property such as <see cref="ProgramBuildLog"/>; with <paramref name="paramValue"/> null only reports the size.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clGetProgramBuildInfo(IntPtr program, IntPtr device, uint paramName, IntPtr paramSize, byte[] paramValue, ref IntPtr sizeRet);

		/// <summary>clCreateKernel: a kernel object for the named <c>__kernel</c> function of a built program. Release with <see cref="clReleaseKernel"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr clCreateKernel(IntPtr program, [MarshalAs(UnmanagedType.VBByRefStr)] ref string kernelName, ref int errcode);

		/// <summary>clSetKernelArg for a buffer argument (cl_mem handle; <paramref name="argSize"/> is the pointer size).</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clSetKernelArg", ExactSpelling = true, SetLastError = true)]
		internal static extern int clSetKernelArgMem(IntPtr kernel, uint argIndex, IntPtr argSize, ref IntPtr argValue);

		/// <summary>clSetKernelArg for an <c>int</c> argument (<paramref name="argSize"/> = 4).</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clSetKernelArg", ExactSpelling = true, SetLastError = true)]
		internal static extern int clSetKernelArgInt(IntPtr kernel, uint argIndex, IntPtr argSize, ref int argValue);

		/// <summary>clSetKernelArg for a <c>float</c> argument (<paramref name="argSize"/> = 4).</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clSetKernelArg", ExactSpelling = true, SetLastError = true)]
		internal static extern int clSetKernelArgFloat(IntPtr kernel, uint argIndex, IntPtr argSize, ref float argValue);

		/// <summary>clCreateBuffer: a device buffer of <paramref name="size"/> bytes with the given CL_MEM flags (<see cref="MemReadWrite"/>). Release with <see cref="clReleaseMemObject"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr clCreateBuffer(IntPtr context, ulong flags, IntPtr size, IntPtr hostPtr, ref int errcode);

		/// <summary>clEnqueueWriteBuffer from a float array; <paramref name="blocking"/> 1 waits for completion, <paramref name="offset"/>/<paramref name="size"/> are in bytes.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clEnqueueWriteBuffer", ExactSpelling = true, SetLastError = true)]
		internal static extern int clEnqueueWriteBufferFloat(IntPtr queue, IntPtr buffer, uint blocking, IntPtr offset, IntPtr size, float[] data, uint numEvents, IntPtr waitList, IntPtr evt);

		/// <summary>clEnqueueWriteBuffer from an int array; <paramref name="blocking"/> 1 waits for completion, <paramref name="offset"/>/<paramref name="size"/> are in bytes.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clEnqueueWriteBuffer", ExactSpelling = true, SetLastError = true)]
		internal static extern int clEnqueueWriteBufferInt(IntPtr queue, IntPtr buffer, uint blocking, IntPtr offset, IntPtr size, int[] data, uint numEvents, IntPtr waitList, IntPtr evt);

		/// <summary>clEnqueueReadBuffer into a float array; <paramref name="blocking"/> 1 waits for completion, <paramref name="offset"/>/<paramref name="size"/> are in bytes.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, EntryPoint = "clEnqueueReadBuffer", ExactSpelling = true, SetLastError = true)]
		internal static extern int clEnqueueReadBufferFloat(IntPtr queue, IntPtr buffer, uint blocking, IntPtr offset, IntPtr size, float[] data, uint numEvents, IntPtr waitList, IntPtr evt);

		/// <summary>clEnqueueNDRangeKernel: launches the kernel over a <paramref name="workDim"/>-dimensional range; <paramref name="localSize"/> null lets the driver pick the work-group size.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clEnqueueNDRangeKernel(IntPtr queue, IntPtr kernel, uint workDim, IntPtr[] globalOffset, IntPtr[] globalSize, IntPtr[] localSize, uint numEvents, IntPtr waitList, IntPtr evt);

		/// <summary>clFinish: blocks until every command queued so far has completed.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clFinish(IntPtr queue);

		/// <summary>clReleaseMemObject: drops a reference to a buffer created by <see cref="clCreateBuffer"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clReleaseMemObject(IntPtr memObj);

		/// <summary>clReleaseKernel: drops a reference to a kernel created by <see cref="clCreateKernel"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clReleaseKernel(IntPtr kernel);

		/// <summary>clReleaseProgram: drops a reference to a program created by <see cref="clCreateProgramWithSource"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clReleaseProgram(IntPtr program);

		/// <summary>clReleaseCommandQueue: drops a reference to a queue created by <see cref="clCreateCommandQueue"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clReleaseCommandQueue(IntPtr queue);

		/// <summary>clReleaseContext: drops a reference to a context created by <see cref="clCreateContext"/>.</summary>
		[DllImport("OpenCL.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		internal static extern int clReleaseContext(IntPtr context);

		/// <summary>Throws if an OpenCL call did not return CL_SUCCESS.</summary>
		/// <param name="code">Return value or errcode output of the OpenCL call.</param>
		/// <param name="what">Name of the call, used in the exception message.</param>
		/// <exception cref="InvalidOperationException"><paramref name="code"/> is not 0; the message includes <see cref="ErrorName"/>.</exception>
		internal static void Check(int code, string what)
		{
			if (code != 0)
			{
				throw new InvalidOperationException(what + " failed: " + OpenClApi.ErrorName(code));
			}
		}

		/// <summary>Human-readable name of an OpenCL error code, e.g. "CL_INVALID_VALUE (-30)"; unknown codes give "error (code)".</summary>
		internal static string ErrorName(int code)
		{
			string name;
			switch (code)
			{
			case -1:
				name = "CL_DEVICE_NOT_FOUND";
				break;
			case -2:
				name = "CL_DEVICE_NOT_AVAILABLE";
				break;
			case -3:
				name = "CL_COMPILER_NOT_AVAILABLE";
				break;
			case -4:
				name = "CL_MEM_OBJECT_ALLOCATION_FAILURE (out of GPU memory)";
				break;
			case -5:
				name = "CL_OUT_OF_RESOURCES";
				break;
			case -6:
				name = "CL_OUT_OF_HOST_MEMORY";
				break;
			case -11:
				name = "CL_BUILD_PROGRAM_FAILURE";
				break;
			case -30:
				name = "CL_INVALID_VALUE";
				break;
			case -33:
				name = "CL_INVALID_DEVICE";
				break;
			case -34:
				name = "CL_INVALID_CONTEXT";
				break;
			case -36:
				name = "CL_INVALID_COMMAND_QUEUE";
				break;
			case -38:
				name = "CL_INVALID_MEM_OBJECT";
				break;
			case -45:
				name = "CL_INVALID_PROGRAM_EXECUTABLE";
				break;
			case -46:
				name = "CL_INVALID_KERNEL_NAME";
				break;
			case -48:
				name = "CL_INVALID_KERNEL";
				break;
			case -49:
				name = "CL_INVALID_ARG_INDEX";
				break;
			case -50:
				name = "CL_INVALID_ARG_VALUE";
				break;
			case -51:
				name = "CL_INVALID_ARG_SIZE";
				break;
			case -52:
				name = "CL_INVALID_KERNEL_ARGS";
				break;
			case -54:
				name = "CL_INVALID_WORK_GROUP_SIZE";
				break;
			case -55:
				name = "CL_INVALID_WORK_ITEM_SIZE";
				break;
			case -61:
				name = "CL_INVALID_BUFFER_SIZE";
				break;
			case -1001:
				// Returned by the ICD loader when no vendor driver is registered.
				name = "no OpenCL platform (CL_PLATFORM_NOT_FOUND_KHR)";
				break;
			default:
				name = "error";
				break;
			}
			return name + " (" + code.ToString(CultureInfo.InvariantCulture) + ")";
		}

		/// <summary>Raw bytes of a clGetDeviceInfo query (size probed first), or null if either call fails or the value is empty.</summary>
		/// <param name="device">OpenCL device handle.</param>
		/// <param name="paramName">A CL_DEVICE_* constant such as <see cref="DeviceName"/>.</param>
		internal static byte[] DeviceInfoBytes(IntPtr device, uint paramName)
		{
			IntPtr sizeRet = IntPtr.Zero;
			if (OpenClApi.clGetDeviceInfo(device, paramName, IntPtr.Zero, null, ref sizeRet) != 0)
			{
				return null;
			}
			int size = sizeRet.ToInt32();
			if (size <= 0)
			{
				return null;
			}
			byte[] value = new byte[size];
			IntPtr ignored = IntPtr.Zero;
			if (OpenClApi.clGetDeviceInfo(device, paramName, new IntPtr(size), value, ref ignored) != 0)
			{
				return null;
			}
			return value;
		}

		/// <summary>A string device property decoded as ASCII with the terminating NUL and padding trimmed; "" when the query fails.</summary>
		internal static string DeviceString(IntPtr device, uint paramName)
		{
			byte[] bytes = OpenClApi.DeviceInfoBytes(device, paramName);
			return bytes == null ? "" : Encoding.ASCII.GetString(bytes).Trim(new char[] { '\0', ' ' });
		}

		/// <summary>
		/// A numeric device property: 8-byte values (cl_ulong) are returned as is, 4-byte values (cl_uint, cl_bool)
		/// are widened; 0 when the query fails or the value is shorter than 4 bytes.
		/// </summary>
		internal static ulong DeviceNumber(IntPtr device, uint paramName)
		{
			byte[] bytes = OpenClApi.DeviceInfoBytes(device, paramName);
			if (bytes == null)
			{
				return 0UL;
			}
			if (bytes.Length >= 8)
			{
				return BitConverter.ToUInt64(bytes, 0);
			}
			if (bytes.Length >= 4)
			{
				return BitConverter.ToUInt32(bytes, 0);
			}
			return 0UL;
		}

		/// <summary>The compiler log for a program on a device (CL_PROGRAM_BUILD_LOG, trimmed); empty if unavailable or if the query throws.</summary>
		internal static string BuildLog(IntPtr program, IntPtr device)
		{
			try
			{
				IntPtr sizeRet = IntPtr.Zero;
				if (OpenClApi.clGetProgramBuildInfo(program, device, OpenClApi.ProgramBuildLog, IntPtr.Zero, null, ref sizeRet) != 0)
				{
					return "";
				}
				int size = sizeRet.ToInt32();
				if (size <= 0)
				{
					return "";
				}
				byte[] log = new byte[size];
				IntPtr ignored = IntPtr.Zero;
				if (OpenClApi.clGetProgramBuildInfo(program, device, OpenClApi.ProgramBuildLog, new IntPtr(size), log, ref ignored) != 0)
				{
					return "";
				}
				return Encoding.ASCII.GetString(log).Trim(new char[] { '\0', ' ', '\r', '\n' });
			}
			catch (Exception)
			{
				return "";
			}
		}

		/// <summary>CL_DEVICE_TYPE_GPU (1 &lt;&lt; 2).</summary>
		internal const ulong DeviceTypeGpu = 4UL;

		/// <summary>CL_MEM_READ_WRITE (1 &lt;&lt; 0) buffer flag.</summary>
		internal const ulong MemReadWrite = 1UL;

		/// <summary>CL_CONTEXT_PLATFORM (0x1084): property key for the platform in <see cref="clCreateContext"/>.</summary>
		internal const int ContextPlatformProperty = 4228;

		/// <summary>CL_PLATFORM_NAME (0x0902).</summary>
		internal const uint PlatformNameInfo = 2306U;

		/// <summary>CL_DEVICE_MAX_COMPUTE_UNITS (0x1002), cl_uint.</summary>
		internal const uint DeviceMaxComputeUnits = 4098U;

		/// <summary>CL_DEVICE_MAX_CLOCK_FREQUENCY (0x100C), cl_uint in MHz.</summary>
		internal const uint DeviceMaxClockFrequency = 4108U;

		/// <summary>CL_DEVICE_MAX_MEM_ALLOC_SIZE (0x1010), cl_ulong bytes.</summary>
		internal const uint DeviceMaxMemAllocSize = 4112U;

		/// <summary>CL_DEVICE_GLOBAL_MEM_SIZE (0x101F), cl_ulong bytes.</summary>
		internal const uint DeviceGlobalMemSize = 4127U;

		/// <summary>CL_DEVICE_NAME (0x102B), string.</summary>
		internal const uint DeviceName = 4139U;

		/// <summary>CL_DEVICE_VENDOR (0x102C), string.</summary>
		internal const uint DeviceVendor = 4140U;

		/// <summary>CL_DEVICE_HOST_UNIFIED_MEMORY (0x1035), cl_bool: non-zero for devices sharing host memory (integrated GPUs).</summary>
		internal const uint DeviceHostUnifiedMemory = 4149U;

		/// <summary>CL_PROGRAM_BUILD_LOG (0x1183), string.</summary>
		internal const uint ProgramBuildLog = 4483U;
	}
}

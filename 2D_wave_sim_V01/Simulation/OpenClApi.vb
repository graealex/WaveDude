' Wave Dude 2D - interactive 2D wave simulator
' Copyright (C) 2026 JJM Vleggaar (Huygens Optics)
' SPDX-License-Identifier: GPL-3.0-or-later
'
' This file is part of Wave Dude 2D.
'
' Wave Dude 2D is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' Wave Dude 2D is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
' GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License
' along with Wave Dude 2D. If not, see <https://www.gnu.org/licenses/>.

Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>
''' The part of the OpenCL 1.2 C interface that the GPU simulation needs, called directly from OpenCL.dll.
''' OpenCL.dll is installed with the driver of the graphics card (NVIDIA, Intel and AMD all install it), so no
''' extra package is needed. Names and parameters follow the C header (cl.h); "size_t" is an IntPtr,
''' "cl_bitfield" a ULong, "cl_uint" a UInteger and "cl_bool" a UInteger.
''' </summary>
Friend Module OpenClApi

    Friend Const DeviceTypeGpu As ULong = 4UL
    Friend Const MemReadWrite As ULong = 1UL
    Friend Const ContextPlatformProperty As Integer = &H1084

    Friend Const PlatformNameInfo As UInteger = &H902UI
    Friend Const DeviceMaxComputeUnits As UInteger = &H1002UI
    Friend Const DeviceMaxClockFrequency As UInteger = &H100CUI
    Friend Const DeviceMaxMemAllocSize As UInteger = &H1010UI
    Friend Const DeviceGlobalMemSize As UInteger = &H101FUI
    Friend Const DeviceName As UInteger = &H102BUI
    Friend Const DeviceVendor As UInteger = &H102CUI
    Friend Const DeviceHostUnifiedMemory As UInteger = &H1035UI
    Friend Const ProgramBuildLog As UInteger = &H1183UI

    ' ---- platforms and devices

    Friend Declare Ansi Function clGetPlatformIDs Lib "OpenCL.dll" (numEntries As UInteger, platforms As IntPtr(), ByRef numPlatforms As UInteger) As Integer

    Friend Declare Ansi Function clGetDeviceIDs Lib "OpenCL.dll" (platform As IntPtr, deviceType As ULong, numEntries As UInteger, devices As IntPtr(), ByRef numDevices As UInteger) As Integer

    Friend Declare Ansi Function clGetDeviceInfo Lib "OpenCL.dll" (device As IntPtr, paramName As UInteger, paramSize As IntPtr, paramValue As Byte(), ByRef sizeRet As IntPtr) As Integer

    ' ---- context, queue, program, kernels

    Friend Declare Ansi Function clCreateContext Lib "OpenCL.dll" (properties As IntPtr(), numDevices As UInteger, devices As IntPtr(), notify As IntPtr, userData As IntPtr, ByRef errcode As Integer) As IntPtr

    Friend Declare Ansi Function clCreateCommandQueue Lib "OpenCL.dll" (context As IntPtr, device As IntPtr, properties As ULong, ByRef errcode As Integer) As IntPtr

    Friend Declare Ansi Function clCreateProgramWithSource Lib "OpenCL.dll" (context As IntPtr, count As UInteger, <MarshalAs(UnmanagedType.LPArray, ArraySubType:=UnmanagedType.LPStr)> strings As String(), lengths As IntPtr(), ByRef errcode As Integer) As IntPtr

    Friend Declare Ansi Function clBuildProgram Lib "OpenCL.dll" (program As IntPtr, numDevices As UInteger, devices As IntPtr(), options As String, notify As IntPtr, userData As IntPtr) As Integer

    Friend Declare Ansi Function clGetProgramBuildInfo Lib "OpenCL.dll" (program As IntPtr, device As IntPtr, paramName As UInteger, paramSize As IntPtr, paramValue As Byte(), ByRef sizeRet As IntPtr) As Integer

    Friend Declare Ansi Function clCreateKernel Lib "OpenCL.dll" (program As IntPtr, kernelName As String, ByRef errcode As Integer) As IntPtr

    Friend Declare Ansi Function clSetKernelArgMem Lib "OpenCL.dll" Alias "clSetKernelArg" (kernel As IntPtr, argIndex As UInteger, argSize As IntPtr, ByRef argValue As IntPtr) As Integer

    Friend Declare Ansi Function clSetKernelArgInt Lib "OpenCL.dll" Alias "clSetKernelArg" (kernel As IntPtr, argIndex As UInteger, argSize As IntPtr, ByRef argValue As Integer) As Integer

    Friend Declare Ansi Function clSetKernelArgFloat Lib "OpenCL.dll" Alias "clSetKernelArg" (kernel As IntPtr, argIndex As UInteger, argSize As IntPtr, ByRef argValue As Single) As Integer

    ' ---- memory and execution

    Friend Declare Ansi Function clCreateBuffer Lib "OpenCL.dll" (context As IntPtr, flags As ULong, size As IntPtr, hostPtr As IntPtr, ByRef errcode As Integer) As IntPtr

    Friend Declare Ansi Function clEnqueueWriteBufferFloat Lib "OpenCL.dll" Alias "clEnqueueWriteBuffer" (queue As IntPtr, buffer As IntPtr, blocking As UInteger, offset As IntPtr, size As IntPtr, data As Single(), numEvents As UInteger, waitList As IntPtr, evt As IntPtr) As Integer

    Friend Declare Ansi Function clEnqueueWriteBufferInt Lib "OpenCL.dll" Alias "clEnqueueWriteBuffer" (queue As IntPtr, buffer As IntPtr, blocking As UInteger, offset As IntPtr, size As IntPtr, data As Integer(), numEvents As UInteger, waitList As IntPtr, evt As IntPtr) As Integer

    Friend Declare Ansi Function clEnqueueReadBufferFloat Lib "OpenCL.dll" Alias "clEnqueueReadBuffer" (queue As IntPtr, buffer As IntPtr, blocking As UInteger, offset As IntPtr, size As IntPtr, data As Single(), numEvents As UInteger, waitList As IntPtr, evt As IntPtr) As Integer

    Friend Declare Ansi Function clEnqueueNDRangeKernel Lib "OpenCL.dll" (queue As IntPtr, kernel As IntPtr, workDim As UInteger, globalOffset As IntPtr(), globalSize As IntPtr(), localSize As IntPtr(), numEvents As UInteger, waitList As IntPtr, evt As IntPtr) As Integer

    Friend Declare Ansi Function clFinish Lib "OpenCL.dll" (queue As IntPtr) As Integer

    ' ---- release

    Friend Declare Ansi Function clReleaseMemObject Lib "OpenCL.dll" (memObj As IntPtr) As Integer
    Friend Declare Ansi Function clReleaseKernel Lib "OpenCL.dll" (kernel As IntPtr) As Integer
    Friend Declare Ansi Function clReleaseProgram Lib "OpenCL.dll" (program As IntPtr) As Integer
    Friend Declare Ansi Function clReleaseCommandQueue Lib "OpenCL.dll" (queue As IntPtr) As Integer
    Friend Declare Ansi Function clReleaseContext Lib "OpenCL.dll" (context As IntPtr) As Integer

    ' ---- helpers

    ''' <summary>Throws when an OpenCL call returned an error code (0 = CL_SUCCESS).</summary>
    Friend Sub Check(code As Integer, what As String)
        If code <> 0 Then Throw New InvalidOperationException(what & " failed: " & ErrorName(code))
    End Sub

    Friend Function ErrorName(code As Integer) As String
        Dim name As String
        Select Case code
            Case -1 : name = "CL_DEVICE_NOT_FOUND"
            Case -2 : name = "CL_DEVICE_NOT_AVAILABLE"
            Case -3 : name = "CL_COMPILER_NOT_AVAILABLE"
            Case -4 : name = "CL_MEM_OBJECT_ALLOCATION_FAILURE (out of GPU memory)"
            Case -5 : name = "CL_OUT_OF_RESOURCES"
            Case -6 : name = "CL_OUT_OF_HOST_MEMORY"
            Case -11 : name = "CL_BUILD_PROGRAM_FAILURE"
            Case -30 : name = "CL_INVALID_VALUE"
            Case -33 : name = "CL_INVALID_DEVICE"
            Case -34 : name = "CL_INVALID_CONTEXT"
            Case -36 : name = "CL_INVALID_COMMAND_QUEUE"
            Case -38 : name = "CL_INVALID_MEM_OBJECT"
            Case -45 : name = "CL_INVALID_PROGRAM_EXECUTABLE"
            Case -46 : name = "CL_INVALID_KERNEL_NAME"
            Case -48 : name = "CL_INVALID_KERNEL"
            Case -49 : name = "CL_INVALID_ARG_INDEX"
            Case -50 : name = "CL_INVALID_ARG_VALUE"
            Case -51 : name = "CL_INVALID_ARG_SIZE"
            Case -52 : name = "CL_INVALID_KERNEL_ARGS"
            Case -54 : name = "CL_INVALID_WORK_GROUP_SIZE"
            Case -55 : name = "CL_INVALID_WORK_ITEM_SIZE"
            Case -61 : name = "CL_INVALID_BUFFER_SIZE"
            Case -1001 : name = "no OpenCL platform (CL_PLATFORM_NOT_FOUND_KHR)"
            Case Else : name = "error"
        End Select
        Return name & " (" & code.ToString(System.Globalization.CultureInfo.InvariantCulture) & ")"
    End Function

    ''' <summary>The bytes of a device info item, or Nothing when the item is not available.</summary>
    Friend Function DeviceInfoBytes(device As IntPtr, paramName As UInteger) As Byte()
        Dim needed As IntPtr = IntPtr.Zero
        If clGetDeviceInfo(device, paramName, IntPtr.Zero, Nothing, needed) <> 0 Then Return Nothing
        Dim n As Integer = needed.ToInt32()
        If n <= 0 Then Return Nothing
        Dim buffer(n - 1) As Byte
        Dim written As IntPtr = IntPtr.Zero
        If clGetDeviceInfo(device, paramName, New IntPtr(n), buffer, written) <> 0 Then Return Nothing
        Return buffer
    End Function

    Friend Function DeviceString(device As IntPtr, paramName As UInteger) As String
        Dim buffer As Byte() = DeviceInfoBytes(device, paramName)
        If buffer Is Nothing Then Return ""
        Return Encoding.ASCII.GetString(buffer).Trim(ChrW(0), " "c)
    End Function

    ''' <summary>A number (32 or 64 bits) of a device info item, 0 when it is not available.</summary>
    Friend Function DeviceNumber(device As IntPtr, paramName As UInteger) As ULong
        Dim buffer As Byte() = DeviceInfoBytes(device, paramName)
        If buffer Is Nothing Then Return 0UL
        If buffer.Length >= 8 Then Return BitConverter.ToUInt64(buffer, 0)
        If buffer.Length >= 4 Then Return BitConverter.ToUInt32(buffer, 0)
        Return 0UL
    End Function

    ''' <summary>The build log of a program (the compiler messages).</summary>
    Friend Function BuildLog(program As IntPtr, device As IntPtr) As String
        Try
            Dim needed As IntPtr = IntPtr.Zero
            If clGetProgramBuildInfo(program, device, ProgramBuildLog, IntPtr.Zero, Nothing, needed) <> 0 Then Return ""
            Dim n As Integer = needed.ToInt32()
            If n <= 0 Then Return ""
            Dim buffer(n - 1) As Byte
            Dim written As IntPtr = IntPtr.Zero
            If clGetProgramBuildInfo(program, device, ProgramBuildLog, New IntPtr(n), buffer, written) <> 0 Then Return ""
            Return Encoding.ASCII.GetString(buffer).Trim(ChrW(0), " "c, ChrW(13), ChrW(10))
        Catch ex As Exception
            Return ""
        End Try
    End Function

End Module

''' <summary>A GPU that OpenCL can use.</summary>
Friend NotInheritable Class GpuDeviceInfo

    Public PlatformId As IntPtr
    Public DeviceId As IntPtr
    Public Name As String = ""
    Public Vendor As String = ""

    ''' <summary>True for a card with its own memory (the NVIDIA GeForce), False for the graphics of the processor (Intel UHD).</summary>
    Public Discrete As Boolean

    ''' <summary>Compute units times clock (MHz): a rough measure of the speed.</summary>
    Public Score As Double

    Public GlobalMemory As Long
    Public MaxAllocation As Long

    ''' <summary>
    ''' All GPUs that OpenCL knows, the best first: cards with their own memory come before the graphics of the
    ''' processor. Throws DllNotFoundException when there is no OpenCL driver at all.
    ''' </summary>
    Public Shared Function FindAll() As List(Of GpuDeviceInfo)
        Dim found As New List(Of GpuDeviceInfo)()

        Dim platformCount As UInteger = 0UI
        If OpenClApi.clGetPlatformIDs(0UI, Nothing, platformCount) <> 0 OrElse platformCount = 0UI Then Return found

        Dim platforms(CInt(platformCount) - 1) As IntPtr
        Dim returned As UInteger = 0UI
        If OpenClApi.clGetPlatformIDs(platformCount, platforms, returned) <> 0 Then Return found

        For Each plat As IntPtr In platforms
            Dim deviceCount As UInteger = 0UI
            If OpenClApi.clGetDeviceIDs(plat, OpenClApi.DeviceTypeGpu, 0UI, Nothing, deviceCount) <> 0 OrElse deviceCount = 0UI Then Continue For

            Dim ids(CInt(deviceCount) - 1) As IntPtr
            Dim got As UInteger = 0UI
            If OpenClApi.clGetDeviceIDs(plat, OpenClApi.DeviceTypeGpu, deviceCount, ids, got) <> 0 Then Continue For

            For Each id As IntPtr In ids
                found.Add(Describe(plat, id))
            Next
        Next

        found.Sort(Function(a As GpuDeviceInfo, b As GpuDeviceInfo) As Integer
                       If a.Discrete <> b.Discrete Then Return If(a.Discrete, -1, 1)
                       Return b.Score.CompareTo(a.Score)
                   End Function)
        Return found
    End Function

    Private Shared Function Describe(plat As IntPtr, id As IntPtr) As GpuDeviceInfo
        Dim g As New GpuDeviceInfo()
        g.PlatformId = plat
        g.DeviceId = id
        g.Name = OpenClApi.DeviceString(id, OpenClApi.DeviceName)
        g.Vendor = OpenClApi.DeviceString(id, OpenClApi.DeviceVendor)
        g.Discrete = (OpenClApi.DeviceNumber(id, OpenClApi.DeviceHostUnifiedMemory) = 0UL)
        Dim units As Double = CDbl(OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxComputeUnits))
        Dim clock As Double = CDbl(OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxClockFrequency))
        g.Score = units * Math.Max(1.0, clock)
        g.GlobalMemory = CLng(OpenClApi.DeviceNumber(id, OpenClApi.DeviceGlobalMemSize) And &H7FFFFFFFFFFFFFFFUL)
        g.MaxAllocation = CLng(OpenClApi.DeviceNumber(id, OpenClApi.DeviceMaxMemAllocSize) And &H7FFFFFFFFFFFFFFFUL)
        If g.Name.Length = 0 Then g.Name = "GPU"
        Return g
    End Function

End Class

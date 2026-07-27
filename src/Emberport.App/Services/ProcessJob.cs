using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Emberport.Services;

/// <summary>
/// A Windows job object that owns every server Emberport starts. Windows kills the whole
/// job as soon as this application ends, so no server can survive a crash or a debugger stop.
/// </summary>
public sealed class ProcessJob
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x00002000;

    private static readonly Lazy<ProcessJob> Instance = new(() => new ProcessJob());

    private readonly IntPtr _handle;

    private ProcessJob()
    {
        _handle = Create();
    }

    public static ProcessJob Current => Instance.Value;

    /// <summary>False when the job could not be created, in which case nothing is enforced.</summary>
    public bool IsActive => _handle != IntPtr.Zero;

    /// <summary>Puts a freshly started process under the job. Failures are not fatal.</summary>
    public void Assign(Process process)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AssignProcessToJobObject(_handle, process.Handle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process may have exited already; the normal Stop path still applies.
        }
    }

    private static IntPtr Create()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);

        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var information = new JobObjectExtendedLimit
        {
            BasicLimitInformation = new JobObjectBasicLimit { LimitFlags = KillOnJobClose },
        };

        var size = Marshal.SizeOf<JobObjectExtendedLimit>();
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(information, buffer, fDeleteOld: false);

            if (!SetInformationJobObject(handle, ExtendedLimitInformation, buffer, (uint)size))
            {
                CloseHandle(handle);

                return IntPtr.Zero;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimit
    {
        public JobObjectBasicLimit BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr information, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
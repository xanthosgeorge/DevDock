using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class WindowsSystemMetricsService : ISystemMetricsService
{
    private readonly object _gate = new();
    private readonly CpuUsageSampler _cpu = new();
    public Task<SystemMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows system metrics require Windows.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var unavailable = new List<string>();
            double? cpu = null;
            try
            {
                if (!GetSystemTimes(out var idle, out var kernel, out var user)) throw new Win32Exception(Marshal.GetLastWin32Error());
                cpu = _cpu.Sample(new(idle, kernel, user));
            }
            catch (Win32Exception ex) { Trace.WriteLine(ex); _cpu.Reset(); unavailable.Add("CPU unavailable"); }

            CapacityUsage? memory = null;
            try
            {
                var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
                if (!GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (status.TotalPhysical == 0 || status.AvailablePhysical > status.TotalPhysical) throw new IOException("Invalid physical RAM counters.");
                memory = new(status.TotalPhysical - status.AvailablePhysical, status.TotalPhysical);
            }
            catch (Exception ex) when (ex is Win32Exception or IOException) { Trace.WriteLine(ex); unavailable.Add("RAM unavailable"); }

            CapacityUsage? disk = null;
            try
            {
                // The requested disk is always C:, regardless of the executable/project location.
                var drive = new DriveInfo(@"C:\");
                if (!drive.IsReady) throw new IOException("Drive C: is not ready.");
                var total = drive.TotalSize; var free = drive.TotalFreeSpace;
                if (total <= 0 || free < 0 || free > total) throw new IOException("Invalid disk capacity counters.");
                disk = new((ulong)(total - free), (ulong)total);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { Trace.WriteLine(ex); unavailable.Add("Disk C: unavailable"); }
            cancellationToken.ThrowIfCancellationRequested();
            return new SystemMetricsSnapshot(cpu, memory, disk, DateTimeOffset.Now,
                unavailable.Count == 0 ? null : string.Join(" · ", unavailable));
        }
    }, cancellationToken);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}

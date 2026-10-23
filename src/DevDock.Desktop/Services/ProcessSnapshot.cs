using System.Diagnostics;
using System.Runtime.InteropServices;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

internal static class ProcessSnapshot
{
    public static IReadOnlyList<ProcessIdentity> Read(IReadOnlyDictionary<int, int>? parents = null)
    {
        parents ??= OperatingSystem.IsWindows() ? WindowsParents() : new Dictionary<int, int>();
        var result = new List<ProcessIdentity>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    DateTimeOffset? started = null;
                    try { started = new DateTimeOffset(process.StartTime.ToUniversalTime()); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
                    result.Add(new(process.Id, process.ProcessName, started, parents.GetValueOrDefault(process.Id)));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { /* One inaccessible/exited process cannot abort the scan. */ }
            }
        }
        return result;
    }

    private static Dictionary<int, int> WindowsParents()
    {
        var result = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return result;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (Process32FirstW(snapshot, ref entry))
                do { result[(int)entry.Pid] = (int)entry.ParentPid; } while (Process32NextW(snapshot, ref entry));
        }
        finally { CloseHandle(snapshot); }
        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Pid;
        public UIntPtr Heap;
        public uint Module, Threads, ParentPid;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}

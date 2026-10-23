using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class WindowsPortService : IPortService
{
    public Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows port discovery requires Windows.");
        var processes = ProcessSnapshot.Read();
        var byPid = processes.ToDictionary(p => p.Pid);
        var ports = new List<PortInfo>();
        foreach (var family in new[] { 2, 23 }) // AF_INET / AF_INET6
        {
            cancellationToken.ThrowIfCancellationRequested();
            var table = ReadTable(family);
            var count = BitConverter.ToInt32(table, 0);
            var rowSize = family == 2 ? 24 : 56;
            if (count < 0 || 4L + (long)count * rowSize > table.Length) throw new IOException("The TCP table was incomplete. Refresh to try again.");
            for (var row = 0; row < count; row++)
            {
                var offset = 4 + row * rowSize;
                var portOffset = offset + (family == 2 ? 8 : 20);
                var port = table[portOffset] * 256 + table[portOffset + 1];
                var address = family == 2 ? new IPAddress(table.AsSpan(offset + 4, 4))
                    : new IPAddress(table.AsSpan(offset, 16), BitConverter.ToUInt32(table, offset + 16));
                var pid = BitConverter.ToInt32(table, offset + (family == 2 ? 20 : 52));
                ports.Add(new(port, address.ToString(), byPid.GetValueOrDefault(pid) ?? new ProcessIdentity(pid, "Unknown", null)));
            }
        }
        return new PortScanResult(ports.Distinct().OrderBy(p => p.Port).ThenBy(p => p.LocalAddress).ToArray(), processes);
    }, cancellationToken);

    private static byte[] ReadTable(int family)
    {
        uint size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, true, family, 3, 0); // OWNER_PID_LISTENER
        if (result != 122 && result != 0) throw new Win32Exception((int)result);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (size is < 4 or > 64 * 1024 * 1024) throw new IOException("Unexpected TCP table size.");
            var memory = Marshal.AllocHGlobal((int)size);
            try
            {
                var capacity = size;
                result = GetExtendedTcpTable(memory, ref size, true, family, 3, 0);
                if (result == 122) continue; // Listeners changed during the scan.
                if (result != 0) throw new Win32Exception((int)result);
                var bytes = new byte[capacity]; Marshal.Copy(memory, bytes, 0, bytes.Length); return bytes;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        throw new IOException("The TCP table kept changing. Refresh to try again.");
    }
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint GetExtendedTcpTable(IntPtr table, ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool order, int family, int tableClass, uint reserved);
}

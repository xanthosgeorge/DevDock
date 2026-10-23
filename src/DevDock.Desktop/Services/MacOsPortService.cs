using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class MacOsPortService(ICommandRunner commands) : IPortService
{
    public async Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default)
    {
        var result = await commands.ExecuteAsync("/usr/sbin/lsof", ["-nP", "-a", "-iTCP", "-sTCP:LISTEN", "-Fpcn"], "/", cancellationToken: cancellationToken);
        if (result.OutputTruncated || result.ExitCode is not (0 or 1) || (result.ExitCode == 1 && result.StdErr.Length > 0))
            throw new IOException("Unable to inspect local ports with lsof. " + result.StdErr);
        var parentResult = await commands.ExecuteAsync("/bin/ps", ["-axo", "pid=,ppid="], "/", cancellationToken: cancellationToken);
        var parents = new Dictionary<int, int>();
        if (parentResult.ExitCode == 0)
            foreach (var line in parentResult.StdOut.Split('\n'))
            {
                var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 2 && int.TryParse(fields[0], out var pid) && int.TryParse(fields[1], out var parent)) parents[pid] = parent;
            }
        var processes = await Task.Run(() => ProcessSnapshot.Read(parents), cancellationToken);
        return new(Parse(result.StdOut, processes), processes, "macOS shows listeners visible to your account; protected processes may be omitted.");
    }

    public static IReadOnlyList<PortInfo> Parse(string output, IReadOnlyList<ProcessIdentity> processes)
    {
        var byPid = processes.ToDictionary(p => p.Pid);
        var result = new List<PortInfo>();
        int pid = 0; string name = "Unknown";
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line[0] == 'p') { int.TryParse(line.AsSpan(1), out pid); name = "Unknown"; }
            else if (line[0] == 'c') name = line[1..];
            else if (line[0] == 'n')
            {
                var address = line[1..].Replace(" (LISTEN)", ""); var colon = address.LastIndexOf(':');
                if (colon < 0 || !int.TryParse(address.AsSpan(colon + 1), out var port) || port is < 1 or > 65535) continue;
                result.Add(new(port, address[..colon].Trim('[', ']'), byPid.GetValueOrDefault(pid) ?? new(pid, name, null)));
            }
        }
        return result.Distinct().OrderBy(p => p.Port).ToArray();
    }
}

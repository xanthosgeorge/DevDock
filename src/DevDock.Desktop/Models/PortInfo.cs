namespace DevDock.Desktop.Models;

public sealed record ProcessIdentity(int Pid, string Name, DateTimeOffset? StartedAt, int ParentPid = 0);
public sealed record PortInfo(int Port, string LocalAddress, ProcessIdentity? Process)
{
    public string Protocol => "TCP";
    public string State => "Listening";
    public int? ProcessId => Process?.Pid;
    public string ProcessName => Process?.Name ?? "Unknown";
}
public sealed record PortScanResult(IReadOnlyList<PortInfo> Ports, IReadOnlyList<ProcessIdentity> Processes, string? Notice = null);

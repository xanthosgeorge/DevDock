using System.Net;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed record TrackedPortProject(string Name, ProjectProcess Process);
public sealed record AssociatedPort(PortInfo Port, TrackedPortProject? Project, Uri? Endpoint, bool CanStop);

public static class PortAssociation
{
    public static AssociatedPort Associate(PortInfo port, IReadOnlyList<ProcessIdentity> processes, IReadOnlyList<TrackedPortProject> projects)
    {
        var byPid = processes.ToDictionary(p => p.Pid);
        var chain = GetChain(port.Process, byPid);
        var project = projects.FirstOrDefault(p => p.Process.IsRunning && p.Process.OsStartTime is { } started
            && chain.Any(i => i.Pid == p.Process.Pid && i.StartedAt == started));
        var endpoint = project?.Process.GetEndpoints().Where(e => e.IsRuntimeDetected && e.Url.Port == port.Port && MatchesAddress(e.Url, port.LocalAddress))
            .OrderBy(e => IsSpecialPath(e.Url)).Select(e => e.Url).FirstOrDefault();
        return new(port, project, endpoint, CanStop(port.Process, processes));
    }

    public static IReadOnlyList<ProcessIdentity> GetChain(ProcessIdentity? process, IReadOnlyDictionary<int, ProcessIdentity> byPid)
    {
        var chain = new List<ProcessIdentity>(); var seen = new HashSet<int>();
        while (process is { StartedAt: not null } && seen.Add(process.Pid) && chain.Count < 64)
        {
            chain.Add(process);
            if (!byPid.TryGetValue(process.ParentPid, out var parent) || parent.StartedAt is null || parent.StartedAt > process.StartedAt) break;
            process = parent;
        }
        return chain;
    }

    public static bool CanStop(ProcessIdentity? process, IReadOnlyList<ProcessIdentity> processes)
    {
        if (process is not { Pid: > 4, StartedAt: not null }) return false;
        string[] protectedNames = ["system", "registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon", "launchd", "kernel_task"];
        if (protectedNames.Contains(process.Name.ToLowerInvariant())) return false;
        var byPid = processes.ToDictionary(p => p.Pid);
        return !GetChain(byPid.GetValueOrDefault(Environment.ProcessId), byPid).Any(p => p.Pid == process.Pid);
    }

    public static bool IsSpecialPath(Uri uri) => uri.AbsolutePath.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    private static bool MatchesAddress(Uri uri, string address)
    {
        var host = uri.Host.Trim('[', ']');
        if (IPAddress.TryParse(host, out var ip)) return address == ip.ToString() || address is "0.0.0.0" or "::" or "*";
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && (address is "0.0.0.0" or "::" or "*" || IPAddress.TryParse(address, out var local) && IPAddress.IsLoopback(local));
    }
}

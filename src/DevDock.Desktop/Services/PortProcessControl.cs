using System.Diagnostics;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IPortProcessControl
{
    Task StopAsync(AssociatedPort port, CancellationToken cancellationToken = default);
}

public sealed class PortProcessControl(IPortService ports, IProjectRunnerService runner) : IPortProcessControl
{
    public async Task StopAsync(AssociatedPort row, CancellationToken cancellationToken = default)
    {
        var current = await ports.GetListeningPortsAsync(cancellationToken);
        var identity = row.Port.Process;
        if (identity?.StartedAt is null || !current.Ports.Any(p => p.Port == row.Port.Port && p.LocalAddress == row.Port.LocalAddress && p.Process?.Pid == identity.Pid && p.Process.StartedAt == identity.StartedAt)
            || !PortAssociation.CanStop(identity, current.Processes))
            throw new InvalidOperationException("The listener or process changed, exited, or is protected. Refresh before trying again.");
        if (row.Project is { } project)
        {
            var verified = PortAssociation.Associate(row.Port, current.Processes, [project]);
            if (verified.Project is null) throw new InvalidOperationException("The DevDock process is no longer running. Refresh the ports.");
            await runner.StopAsync(project.Process);
            return;
        }
        await Task.Run(async () =>
        {
            using var process = Process.GetProcessById(identity.Pid);
            if (process.HasExited || new DateTimeOffset(process.StartTime.ToUniversalTime()) != identity.StartedAt)
                throw new InvalidOperationException("The process changed or exited. Refresh the ports.");
            process.Kill(entireProcessTree: false); // External processes: only the explicitly selected process.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);
        }, cancellationToken);
    }
}

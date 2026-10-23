using System.Diagnostics;
using System.Text;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class ProjectRunnerService : IProjectRunnerService
{
    private sealed record Entry(ProjectProcess Session, Process Process)
    {
        public Task Completion { get; set; } = Task.CompletedTask;
    }
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _owned = [];
    private bool _shuttingDown;

    public Task<ProjectProcess> StartAsync(ProjectRunConfiguration configuration) => Task.Run(() =>
    {
        lock (_gate)
        {
            if (_shuttingDown) throw new InvalidOperationException("DevDock is shutting down; no new project processes can be started.");
            if (_owned.Values.Any(p => p.Session.ProjectId == configuration.ProjectId && p.Session.IsRunning))
                throw new InvalidOperationException("This project is already running in DevDock.");
            var process = new Process { StartInfo = ProcessLaunch.Create(configuration.Executable, configuration.Arguments, configuration.WorkingDirectory) };
            try { process.Start(); }
            catch { process.Dispose(); throw; }
            var session = new ProjectProcess(process.Id, configuration);
            var entry = new Entry(session, process);
            _owned.Add(session.Id, entry);
            session.AddOutput("> " + configuration.DisplayCommand);
            entry.Completion = WatchAsync(entry);
            return session;
        }
    });

    private async Task WatchAsync(Entry entry)
    {
        try
        {
            var stdout = PumpAsync(entry.Process.StandardOutput, line => entry.Session.AddOutput(line));
            var stderr = PumpAsync(entry.Process.StandardError, line => entry.Session.AddOutput(line, true));
            await entry.Process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            entry.Session.Complete(entry.Process.ExitCode);
        }
        catch (Exception ex)
        {
            entry.Session.AddOutput("Unable to read process output: " + ex.Message, true);
            entry.Session.Complete(-1);
        }
        finally
        {
            lock (_gate) _owned.Remove(entry.Session.Id);
            entry.Process.Dispose();
        }
    }

    public async Task StopAsync(ProjectProcess process)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_owned.TryGetValue(process.Id, out entry) || !ReferenceEquals(process, entry.Session))
            {
                if (!process.IsRunning) return;
                throw new InvalidOperationException("DevDock can only stop processes that it started and owns.");
            }
            try { if (!entry.Process.HasExited) entry.Process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { } // Natural exit raced with Stop.
        }
        await entry.Completion;
    }

    public async Task StopAllAsync()
    {
        ProjectProcess[] sessions;
        lock (_gate) { _shuttingDown = true; sessions = _owned.Values.Select(p => p.Session).ToArray(); }
        await Task.WhenAll(sessions.Select(StopAsync));
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> publish)
    {
        var line = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
            for (var index = 0; index < read; index++)
            {
                var value = buffer[index];
                if (value == '\n')
                {
                    publish(line.ToString().TrimEnd('\r') + (truncated ? " … [line truncated]" : ""));
                    line.Clear(); truncated = false;
                }
                else if (line.Length < 8192) line.Append(value);
                else truncated = true;
            }
        if (line.Length > 0 || truncated) publish(line + (truncated ? " … [line truncated]" : ""));
    }
}

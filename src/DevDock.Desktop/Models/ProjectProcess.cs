namespace DevDock.Desktop.Models;

public sealed record ProjectOutputLine(long Sequence, DateTimeOffset Time, string Text, bool IsError)
{
    public string Display => $"[{Time.ToLocalTime():HH:mm:ss}] {Text}";
}

/// <summary>A session created by the runner. The public model never exposes a process handle.</summary>
public sealed class ProjectProcess
{
    private readonly object _gate = new();
    private readonly Queue<ProjectOutputLine> _output = new();
    private long _sequence;
    private bool _running = true;
    private int? _exitCode;
    public Guid Id { get; } = Guid.NewGuid();
    public int Pid { get; }
    public ProjectRunConfiguration Configuration { get; }
    public Guid ProjectId => Configuration.ProjectId;
    public DateTimeOffset StartTime { get; } = DateTimeOffset.Now;
    public bool IsRunning { get { lock (_gate) return _running; } }
    public int? ExitCode { get { lock (_gate) return _exitCode; } }
    public event Action? Updated;

    internal ProjectProcess(int pid, ProjectRunConfiguration configuration) { Pid = pid; Configuration = configuration; }
    public IReadOnlyList<ProjectOutputLine> GetOutput() { lock (_gate) return _output.ToArray(); }
    public void ClearOutput() { lock (_gate) _output.Clear(); Updated?.Invoke(); }
    internal void AddOutput(string text, bool error = false)
    {
        lock (_gate)
        {
            _output.Enqueue(new(++_sequence, DateTimeOffset.Now, text, error));
            while (_output.Count > 2000) _output.Dequeue();
        }
        Updated?.Invoke();
    }
    internal void Complete(int exitCode)
    {
        lock (_gate) { _running = false; _exitCode = exitCode; }
        AddOutput($"Process exited with code {exitCode}.", exitCode != 0);
    }
}

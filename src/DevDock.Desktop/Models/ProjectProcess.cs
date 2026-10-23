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
    private readonly List<ProjectEndpoint> _endpoints = [];
    public Guid Id { get; } = Guid.NewGuid();
    public int Pid { get; }
    public ProjectRunConfiguration Configuration { get; }
    public Guid ProjectId => Configuration.ProjectId;
    public DateTimeOffset StartTime { get; } = DateTimeOffset.Now;
    public DateTimeOffset? OsStartTime { get; }
    public bool IsRunning { get { lock (_gate) return _running; } }
    public int? ExitCode { get { lock (_gate) return _exitCode; } }
    public event Action? Updated;

    internal ProjectProcess(int pid, ProjectRunConfiguration configuration, DateTimeOffset? osStartTime = null) { Pid = pid; Configuration = configuration; OsStartTime = osStartTime; }
    public IReadOnlyList<ProjectOutputLine> GetOutput() { lock (_gate) return _output.ToArray(); }
    public IReadOnlyList<ProjectEndpoint> GetEndpoints() { lock (_gate) return _endpoints.ToArray(); }
    internal void SetExpectedEndpoints(IEnumerable<ProjectEndpoint> endpoints)
    {
        lock (_gate) if (!_endpoints.Any(e => e.IsRuntimeDetected)) _endpoints.AddRange(endpoints);
    }
    public void ClearOutput() { lock (_gate) _output.Clear(); Updated?.Invoke(); }
    internal void AddOutput(string text, bool error = false)
    {
        lock (_gate)
        {
            if (Services.RuntimeEndpointDetector.Parse(text) is { } endpoint)
            {
                _endpoints.RemoveAll(e => !e.IsRuntimeDetected);
                if (_endpoints.All(e => e.Url != endpoint.Url) && _endpoints.Count < 20) _endpoints.Add(endpoint);
            }
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

namespace DevDock.Desktop.Services;

internal readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

internal sealed class CpuUsageSampler
{
    private CpuTimes? _previous;
    public void Reset() => _previous = null;

    public double? Sample(CpuTimes current)
    {
        var previous = _previous;
        _previous = current;
        if (previous is not { } before || current.Idle < before.Idle || current.Kernel < before.Kernel || current.User < before.User) return null;
        // Windows kernel time already includes idle time. Use interval deltas, never uptime averages.
        var total = (double)(current.Kernel - before.Kernel) + (current.User - before.User);
        var idle = (double)(current.Idle - before.Idle);
        return total <= 0 || idle > total ? null : Math.Clamp(100 * (total - idle) / total, 0, 100);
    }
}

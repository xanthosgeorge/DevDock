using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public static class SystemMetricsServiceFactory
{
    public static ISystemMetricsService Create() => OperatingSystem.IsWindows() ? new WindowsSystemMetricsService() : new UnavailableMetrics();
    private sealed class UnavailableMetrics : ISystemMetricsService
    {
        public Task<SystemMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemMetricsSnapshot(null, null, null, DateTimeOffset.Now, "System metrics currently require Windows."));
    }
}

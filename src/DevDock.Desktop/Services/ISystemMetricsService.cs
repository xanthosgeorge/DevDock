using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface ISystemMetricsService
{
    Task<SystemMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

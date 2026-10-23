using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IProjectDiscoveryService
{
    Task<ProjectDiscoveryResult> DiscoverAsync(string projectPath);
}

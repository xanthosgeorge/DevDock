using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IProjectRunnerService
{
    Task<ProjectProcess> StartAsync(ProjectRunConfiguration configuration);
    Task StopAsync(ProjectProcess process);
    Task StopAllAsync();
}

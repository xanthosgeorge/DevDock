using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IProjectRunConfigurationService
{
    Task<IReadOnlyList<ProjectRunConfiguration>> DetectAsync(Guid projectId, string rootPath);
}

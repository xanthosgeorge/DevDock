using DevDock.Desktop.Models;
using DevDock.Desktop.ViewModels;

namespace DevDock.Desktop.Services;

public interface IProjectWorkspaceInteraction
{
    Task<bool> ConfirmRemovalAsync(string name, bool running);
    Task<ProjectRunConfiguration?> ChooseRunConfigurationAsync(string name, IReadOnlyList<ProjectRunConfiguration> configurations);
    void OpenProject(ProjectWorkspaceViewModel workspace);
    void CloseProject(Guid projectId);
}

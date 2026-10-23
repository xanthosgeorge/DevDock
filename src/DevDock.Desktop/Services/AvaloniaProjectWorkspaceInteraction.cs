using Avalonia.Controls;
using DevDock.Desktop.Models;
using DevDock.Desktop.ViewModels;
using DevDock.Desktop.Views;

namespace DevDock.Desktop.Services;

public sealed class AvaloniaProjectWorkspaceInteraction(Window owner) : IProjectWorkspaceInteraction
{
    private readonly Dictionary<Guid, ProjectDetailsWindow> _windows = [];
    public Task<bool> ConfirmRemovalAsync(string name, bool running) => new WorkspaceDialogWindow(new()
    {
        Title = $"Remove {name} from DevDock?", ConfirmLabel = "Remove Project",
        Description = "This only removes the project from your DevDock dashboard. No files will be deleted from your computer."
            + (running ? "\n\nDevDock will stop the process it started for this project before removing the entry." : "")
    }).ShowDialog<bool>(owner);

    public async Task<ProjectRunConfiguration?> ChooseRunConfigurationAsync(string name, IReadOnlyList<ProjectRunConfiguration> configurations)
    {
        var model = new WorkspaceDialogViewModel
        {
            Title = $"Choose a launch target for {name}", Description = "Multiple runnable targets were detected. Your choice is remembered for this application session.",
            ConfirmLabel = "Use this target", IsChoice = true, Configurations = configurations
        };
        return await new WorkspaceDialogWindow(model).ShowDialog<bool>(owner) ? model.SelectedConfiguration : null;
    }

    public void OpenProject(ProjectWorkspaceViewModel workspace)
    {
        if (_windows.TryGetValue(workspace.Project.Id, out var existing)) { existing.Activate(); return; }
        var window = new ProjectDetailsWindow(workspace);
        _windows[workspace.Project.Id] = window;
        window.Closed += (_, _) => _windows.Remove(workspace.Project.Id);
        window.Show(owner);
    }

    public void CloseProject(Guid projectId)
    {
        if (_windows.TryGetValue(projectId, out var window)) window.Close();
    }
}

using Avalonia.Controls;
using DevDock.Desktop.Models;
using DevDock.Desktop.ViewModels;
using DevDock.Desktop.Views;

namespace DevDock.Desktop.Services;

public sealed class AvaloniaProjectWorkspaceInteraction(Window owner) : IProjectWorkspaceInteraction, IPortInteraction
{
    public Task<bool> ConfirmStopAsync(PortInfo port, bool owned) => new WorkspaceDialogWindow(new()
    {
        Title = "Stop process?", ConfirmLabel = "Stop Process",
        Description = $"{port.ProcessName}\nPID {port.ProcessId}\n\nListening on port {port.Port}.\n\n"
            + (owned ? "This will terminate the DevDock-owned project process tree." : "This will terminate this external process. No elevation will be requested.")
    }).ShowDialog<bool>(owner);
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

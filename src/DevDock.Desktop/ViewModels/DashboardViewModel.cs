using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Services;
using System.Collections.ObjectModel;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IProjectDiscoveryService _discovery;
    private readonly IProjectImportInteraction? _importInteraction;
    private readonly IProjectWorkspaceInteraction? _workspaceInteraction;
    private readonly IGitService _git;
    private readonly IProjectRunnerService _runner;
    private readonly IProjectRunConfigurationService _runConfigurations;
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];
    public string RunningSummary => $"{Projects.Count(p => p.IsRunning)} Running";
    public bool IsEmpty => Projects.Count == 0;
    public string ProjectSummary => $"{Projects.Count} Projects";
    public string ProjectCountLabel => $"{Projects.Count} projects";
    [ObservableProperty] private string _feedback = "Add a local project to get started · projects are kept for this session";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isScanning;
    public string AddProjectLabel => IsScanning ? "Scanning project…" : "＋  Add Project";
    public event Action? ProjectImported;
    public string RefreshLabel => IsRefreshing ? "↻  Refreshing…" : "↻  Refresh";
    partial void OnIsRefreshingChanged(bool value) => OnPropertyChanged(nameof(RefreshLabel));
    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(AddProjectLabel));
    public DashboardViewModel(IProjectDiscoveryService? discovery = null, IProjectImportInteraction? importInteraction = null,
        IProjectWorkspaceInteraction? workspaceInteraction = null, IGitService? git = null,
        IProjectRunnerService? runner = null, IProjectRunConfigurationService? runConfigurations = null)
    {
        _discovery = discovery ?? new ProjectDiscoveryService();
        _importInteraction = importInteraction;
        _workspaceInteraction = workspaceInteraction;
        _git = git ?? new GitCliService(new CommandRunner());
        _runner = runner ?? new ProjectRunnerService();
        _runConfigurations = runConfigurations ?? new ProjectRunConfigurationService(_discovery);
        Projects.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ProjectSummary));
            OnPropertyChanged(nameof(ProjectCountLabel));
            OnPropertyChanged(nameof(RunningSummary));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }
    private void AttachProject(ProjectViewModel project)
    {
        project.Selected += SelectProject;
        project.StateChanged += () => OnPropertyChanged(nameof(RunningSummary));
        project.Workspace = new(project, _git, _runner, _runConfigurations, _workspaceInteraction);
        project.OpenRequested += (item, tab) =>
        {
            SelectProject(item);
            item.Workspace!.SelectedTabIndex = tab;
            _workspaceInteraction?.OpenProject(item.Workspace);
        };
        project.RemoveRequested += async item => { await RemoveProjectAsync(item); };
        if (_workspaceInteraction is not null) _ = project.Workspace.Git.RefreshCommand.ExecuteAsync(null);
    }

    public async Task<bool> RemoveProjectAsync(ProjectViewModel project)
    {
        if (project.IsRemoving || !Projects.Contains(project) || _workspaceInteraction is null) return false;
        if (project.Workspace is { IsBusy: true } || project.Workspace?.Git.IsBusy == true)
        {
            Feedback = "Wait for the current project/Git operation before removing it.";
            return false;
        }
        project.IsRemoving = true;
        try
        {
            if (!await _workspaceInteraction.ConfirmRemovalAsync(project.Name, project.IsRunning)) return false;
            if (project.Workspace is not null) await project.Workspace.StopForRemovalAsync();
            _workspaceInteraction.CloseProject(project.Id);
            var wasSelected = project.IsSelected;
            project.IsSelected = false;
            Projects.Remove(project);
            if (wasSelected && Projects.FirstOrDefault() is { } next) SelectProject(next);
            Feedback = $"{project.Name} removed from DevDock. No local files were deleted.";
            return true;
        }
        catch (Exception ex) { Feedback = "Unable to remove project: " + ex.Message; return false; }
        finally { project.IsRemoving = false; }
    }

    private bool ContainsProject(string root) => Projects.Any(p => ProjectPath.Comparer.Equals(p.Path, root));

    [RelayCommand]
    private async Task AddProjectAsync()
    {
        if (_importInteraction is null) { Feedback = "Folder selection is unavailable until the desktop window is ready."; return; }
        try
        {
            var path = await _importInteraction.PickFolderAsync();
            if (path is null) return;
            var root = ProjectPath.Normalize(path);
            if (ContainsProject(root))
            {
                await _importInteraction.ShowMessageAsync("Project already added", "This project is already in DevDock.");
                return;
            }
            IsScanning = true;
            Feedback = "Scanning project…";
            ProjectDiscoveryResult result;
            try { result = await _discovery.DiscoverAsync(root); }
            finally { IsScanning = false; }
            if (!result.IsSupported)
            {
                Feedback = "No supported project detected.";
                var details = result.Warnings.Count > 0 ? "\n\nSome files could not be inspected. Check folder access and the scan limits." : "";
                await _importInteraction.ShowMessageAsync("No supported project found", "No supported development project was detected in this folder.\n\nYou can choose another directory." + details);
                return;
            }
            if (!await _importInteraction.ConfirmImportAsync(result)) { Feedback = "Project import cancelled."; return; }
            // Recheck at the point of insertion; no import mutates state until confirmation.
            if (ContainsProject(result.RootPath))
            {
                await _importInteraction.ShowMessageAsync("Project already added", "This project is already in DevDock.");
                return;
            }
            var project = ProjectViewModel.FromDiscovery(result);
            AttachProject(project);
            Projects.Insert(0, project);
            SelectProject(project);
            Feedback = $"{project.Name} added · local project · available for this session";
            ProjectImported?.Invoke();
        }
        catch (Exception ex)
        {
            IsScanning = false;
            Feedback = "Project could not be imported.";
            var message = ex switch
            {
                UnauthorizedAccessException => "DevDock cannot access this folder. Choose a folder you have permission to read.",
                IOException => "The selected folder could not be read. Check that it is still available and choose it again.",
                NotSupportedException => ex.Message,
                _ => "DevDock could not inspect this folder. You can choose another directory."
            };
            try { await _importInteraction.ShowMessageAsync("Unable to import project", message); }
            catch { Feedback = message; } // The owner may have closed while discovery was running.
        }
    }
    private void SelectProject(ProjectViewModel selected)
    {
        foreach (var project in Projects) project.IsSelected = project == selected;
    }
    [RelayCommand] private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            foreach (var project in Projects.ToArray())
                if (project.Workspace?.Git.RefreshCommand.CanExecute(null) == true) await project.Workspace.Git.RefreshCommand.ExecuteAsync(null);
            Feedback = $"Workspace refreshed at {DateTime.Now:HH:mm:ss}";
        }
        finally { IsRefreshing = false; }
    }
}

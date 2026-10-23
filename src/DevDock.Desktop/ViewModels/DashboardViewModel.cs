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
    public ObservableCollection<ProjectViewModel> Projects { get; } = new(MockDataService.CreateProjects());
    public DockerContainerViewModel[] Containers { get; } = MockDataService.CreateContainers();
    public PortViewModel[] Ports { get; } = MockDataService.CreatePorts();
    public ApiHealthViewModel[] Apis { get; } = MockDataService.CreateApis();
    public string RunningSummary => $"{Projects.Count(p => p.IsRunning)} Running";
    public string ContainerSummary => $"{Containers.Length} Containers";
    public string PortSummary => $"{Ports.Length} Open Ports";
    public string ProjectSummary => $"{Projects.Count} Projects";
    public string ProjectCountLabel => $"{Projects.Count} projects";
    [ObservableProperty] private string _feedback = "Demo environment · local discovery available · service monitoring uses mock data";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isScanning;
    public string AddProjectLabel => IsScanning ? "Scanning project…" : "＋  Add Project";
    public event Action? ProjectImported;
    public string RefreshLabel => IsRefreshing ? "↻  Refreshing…" : "↻  Refresh";
    partial void OnIsRefreshingChanged(bool value) => OnPropertyChanged(nameof(RefreshLabel));
    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(AddProjectLabel));
    public DashboardViewModel(IProjectDiscoveryService? discovery = null, IProjectImportInteraction? importInteraction = null)
    {
        _discovery = discovery ?? new ProjectDiscoveryService();
        _importInteraction = importInteraction;
        foreach (var project in Projects)
            AttachProject(project);
        Projects.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ProjectSummary));
            OnPropertyChanged(nameof(ProjectCountLabel));
            OnPropertyChanged(nameof(RunningSummary));
        };
        foreach (var container in Containers)
            container.StateChanged += () => Feedback = "Container mock state updated · Docker is not connected";
        SelectProject(Projects[0]);
    }
    private void AttachProject(ProjectViewModel project)
    {
        project.Selected += SelectProject;
        project.MessageRequested += message => Feedback = message;
        project.StateChanged += () => OnPropertyChanged(nameof(RunningSummary));
    }

    private bool ContainsProject(string root) => Projects.Any(p => !p.IsDemo && ProjectPath.Comparer.Equals(p.Path, root));

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
        await Task.Delay(650);
        Feedback = $"Mock environment refreshed at {DateTime.Now:HH:mm:ss}";
        IsRefreshing = false;
    }
}

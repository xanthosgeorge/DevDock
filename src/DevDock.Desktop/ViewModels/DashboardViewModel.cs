using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    public ProjectViewModel[] Projects { get; } = MockDataService.CreateProjects();
    public DockerContainerViewModel[] Containers { get; } = MockDataService.CreateContainers();
    public PortViewModel[] Ports { get; } = MockDataService.CreatePorts();
    public ApiHealthViewModel[] Apis { get; } = MockDataService.CreateApis();
    public string RunningSummary => $"{Projects.Count(p => p.IsRunning)} Running";
    public string ContainerSummary => $"{Containers.Length} Containers";
    public string PortSummary => $"{Ports.Length} Open Ports";
    [ObservableProperty] private string _feedback = "Mock environment · no local services are accessed";
    [ObservableProperty] private bool _isRefreshing;
    public string RefreshLabel => IsRefreshing ? "↻  Refreshing…" : "↻  Refresh";
    partial void OnIsRefreshingChanged(bool value) => OnPropertyChanged(nameof(RefreshLabel));
    public DashboardViewModel()
    {
        foreach (var project in Projects)
        {
            project.Selected += SelectProject;
            project.MessageRequested += message => Feedback = message;
            project.StateChanged += () => OnPropertyChanged(nameof(RunningSummary));
        }
        foreach (var container in Containers)
            container.StateChanged += () => Feedback = "Container mock state updated · Docker is not connected";
        SelectProject(Projects[0]);
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

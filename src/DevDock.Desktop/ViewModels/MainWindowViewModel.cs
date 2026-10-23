using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;

namespace DevDock.Desktop.ViewModels;

public partial class NavigationItemViewModel(string label, string icon) : ObservableObject
{
    public string Label { get; } = label;
    public string Icon { get; } = icon;
    [ObservableProperty] private bool _isSelected;
}

public partial class MainWindowViewModel : ObservableObject
{
    public DashboardViewModel Dashboard { get; }
    public SystemMetricsViewModel Metrics { get; }
    public NavigationItemViewModel[] Navigation { get; } =
    [new("Dashboard", "⌂"), new("Projects", "▣"), new("Docker", "▤"), new("Ports", "⑂"), new("API Health", "ϟ"), new("Logs", "≡")];
    [ObservableProperty] private string _pageTitle = "Dashboard";
    [ObservableProperty] private bool _isDialogOpen;
    [ObservableProperty] private string _dialogTitle = "Settings";
    [ObservableProperty] private string _dialogMessage = "Workspace preferences are coming in a future version.";
    public bool ShowProjects => PageTitle is "Dashboard" or "Projects";
    public bool ShowEnvironment => PageTitle is "Dashboard" or "Docker" or "API Health";
    public bool ShowDocker => PageTitle is "Dashboard" or "Docker";
    public bool ShowPorts => PageTitle == "Dashboard";
    public bool ShowPortsWorkspace => PageTitle == "Ports";
    public bool ShowApis => PageTitle is "Dashboard" or "API Health";
    public bool ShowLogs => PageTitle == "Logs";
    public GridLength ProjectWidth => ShowProjects ? new GridLength(1.5, GridUnitType.Star) : new GridLength(0);
    public GridLength EnvironmentWidth => ShowEnvironment ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    public GridLength ColumnGap => ShowProjects && ShowEnvironment ? new GridLength(20) : new GridLength(0);
    public MainWindowViewModel(DashboardViewModel? dashboard = null, SystemMetricsViewModel? metrics = null)
    {
        Dashboard = dashboard ?? new DashboardViewModel();
        Metrics = metrics ?? new SystemMetricsViewModel();
        Dashboard.ProjectImported += () => { if (!ShowProjects) Navigate("Projects"); };
        Navigation[0].IsSelected = true;
    }
    partial void OnPageTitleChanged(string value)
    {
        foreach (var name in new[] { nameof(ShowProjects), nameof(ShowEnvironment), nameof(ShowDocker), nameof(ShowPorts), nameof(ShowPortsWorkspace), nameof(ShowApis), nameof(ShowLogs), nameof(ProjectWidth), nameof(EnvironmentWidth), nameof(ColumnGap) }) OnPropertyChanged(name);
    }
    [RelayCommand] private void Navigate(string label)
    {
        PageTitle = label;
        foreach (var item in Navigation) item.IsSelected = item.Label == label;
        if (label == "Ports" && Dashboard.Ports.RefreshCommand.CanExecute(null)) _ = Dashboard.Ports.RefreshCommand.ExecuteAsync(null);
    }
    [RelayCommand] private void Settings()
    {
        DialogTitle = "Your workspace, your way";
        DialogMessage = "Projects are saved automatically in your local workspace and restored when DevDock opens. Additional workspace preferences will arrive in a future version.";
        IsDialogOpen = true;
    }
    [RelayCommand] private void CloseDialog() => IsDialogOpen = false;
}

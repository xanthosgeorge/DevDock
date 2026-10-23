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
    public DashboardViewModel Dashboard { get; } = new();
    public NavigationItemViewModel[] Navigation { get; } =
    [new("Dashboard", "⌂"), new("Projects", "▣"), new("Docker", "▤"), new("Ports", "⑂"), new("API Health", "ϟ"), new("Logs", "≡")];
    [ObservableProperty] private string _pageTitle = "Dashboard";
    [ObservableProperty] private bool _isDialogOpen;
    [ObservableProperty] private string _dialogTitle = "Add a project";
    [ObservableProperty] private string _dialogMessage = "Project discovery will be available in DevDock v0.2.";
    public bool ShowProjects => PageTitle is "Dashboard" or "Projects";
    public bool ShowEnvironment => PageTitle is "Dashboard" or "Docker" or "Ports" or "API Health";
    public bool ShowDocker => PageTitle is "Dashboard" or "Docker";
    public bool ShowPorts => PageTitle is "Dashboard" or "Ports";
    public bool ShowApis => PageTitle is "Dashboard" or "API Health";
    public bool ShowLogs => PageTitle == "Logs";
    public GridLength ProjectWidth => ShowProjects ? new GridLength(1.5, GridUnitType.Star) : new GridLength(0);
    public GridLength EnvironmentWidth => ShowEnvironment ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    public GridLength ColumnGap => ShowProjects && ShowEnvironment ? new GridLength(20) : new GridLength(0);
    public MainWindowViewModel() => Navigation[0].IsSelected = true;
    partial void OnPageTitleChanged(string value)
    {
        foreach (var name in new[] { nameof(ShowProjects), nameof(ShowEnvironment), nameof(ShowDocker), nameof(ShowPorts), nameof(ShowApis), nameof(ShowLogs), nameof(ProjectWidth), nameof(EnvironmentWidth), nameof(ColumnGap) }) OnPropertyChanged(name);
    }
    [RelayCommand] private void Navigate(string label)
    {
        PageTitle = label;
        foreach (var item in Navigation) item.IsSelected = item.Label == label;
    }
    [RelayCommand] private void AddProject()
    {
        DialogTitle = "Add a project";
        DialogMessage = "Project discovery will be available in DevDock v0.2.";
        IsDialogOpen = true;
    }
    [RelayCommand] private void Settings()
    {
        DialogTitle = "Your workspace, your way";
        DialogMessage = "Settings and workspace preferences will arrive in a future version. DevDock v0.1 uses a curated mock environment.";
        IsDialogOpen = true;
    }
    [RelayCommand] private void CloseDialog() => IsDialogOpen = false;
}

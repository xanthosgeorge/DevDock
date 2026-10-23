using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Branch { get; init; }
    public required string GitStatus { get; init; }
    public required string LastActivity { get; init; }
    public required string[] Technologies { get; init; }
    public required ServiceViewModel[] Services { get; init; }
    public string RepositoryLabel { get; init; } = "GitHub";
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isRunning;
    public string ToggleLabel => IsRunning ? "■  Stop" : "▶  Start";
    public event Action<ProjectViewModel>? Selected;
    public event Action<string>? MessageRequested;
    public event Action? StateChanged;
    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(ToggleLabel));

    [RelayCommand] private void Select() => Selected?.Invoke(this);
    [RelayCommand] private void Toggle()
    {
        IsRunning = !IsRunning;
        foreach (var service in Services) service.State = IsRunning ? ServiceState.Running : ServiceState.Stopped;
        StateChanged?.Invoke();
        MessageRequested?.Invoke($"{Name} {(IsRunning ? "started" : "stopped")} · mock state updated");
    }
    [RelayCommand] private void OpenIde() => MessageRequested?.Invoke("IDE integration coming in DevDock v0.2.");
    [RelayCommand] private void OpenTerminal() => MessageRequested?.Invoke("Terminal integration coming in DevDock v0.3.");
    [RelayCommand] private void OpenRepository() => MessageRequested?.Invoke("Git integration coming in DevDock v0.2.");
}

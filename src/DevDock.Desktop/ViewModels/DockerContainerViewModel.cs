using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class DockerContainerViewModel(string name, string port, string uptime, bool running) : ObservableObject
{
    public string Name { get; } = name;
    public string Port { get; } = port;
    public string Uptime { get; } = uptime;
    [ObservableProperty] private bool _isRunning = running;
    public ServiceState State => IsRunning ? ServiceState.Running : ServiceState.Stopped;
    public string ActionLabel => IsRunning ? "■" : "▶";
    public string ActionHint => IsRunning ? $"Stop {Name} (mock)" : $"Start {Name} (mock)";
    public string DisplayUptime => IsRunning ? Uptime : "Stopped";
    public event Action? StateChanged;
    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(ActionHint)); OnPropertyChanged(nameof(DisplayUptime));
    }
    [RelayCommand] private void Toggle() { IsRunning = !IsRunning; StateChanged?.Invoke(); }
}

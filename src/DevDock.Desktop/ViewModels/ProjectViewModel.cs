using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    [ObservableProperty] private string _branch = "";
    [ObservableProperty] private string _gitStatus = "";
    public required string LastActivity { get; init; }
    public required string[] Technologies { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
    [ObservableProperty] private ProjectWorkspaceViewModel? _workspace;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(RemoveCommand))] private bool _isRemoving;
    public string OriginLabel => "LOCAL";
    public bool HasBranch => !string.IsNullOrEmpty(Branch);
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isRunning;
    public event Action<ProjectViewModel>? Selected;
    public event Action? StateChanged;
    public event Action<ProjectViewModel, int>? OpenRequested;
    public event Func<ProjectViewModel, Task>? RemoveRequested;
    private bool CanRemove => !IsRemoving;
    public string LocalRunState => IsRunning ? "Running · started by DevDock" : "Not running in DevDock";
    public ServiceState LocalProcessState => IsRunning ? ServiceState.Running : Workspace?.Process is not null ? ServiceState.Stopped : ServiceState.Unknown;
    partial void OnBranchChanged(string value) => OnPropertyChanged(nameof(HasBranch));
    partial void OnIsRunningChanged(bool value) { OnPropertyChanged(nameof(LocalRunState)); OnPropertyChanged(nameof(LocalProcessState)); StateChanged?.Invoke(); }

    [RelayCommand] private void Select() => Selected?.Invoke(this);
    [RelayCommand] private void OpenRepository() => OpenRequested?.Invoke(this, 1);
    [RelayCommand] private void OpenProject() => OpenRequested?.Invoke(this, 0);
    [RelayCommand(CanExecute = nameof(CanRemove))] private async Task RemoveAsync()
    {
        if (RemoveRequested is not null) await RemoveRequested(this);
    }
    public void ApplyGitStatus(GitRepositoryStatus status)
    {
        Branch = status.Branch;
        GitStatus = status.IsRepository ? status.Summary : "Git not detected";
    }

    public static ProjectViewModel FromDiscovery(ProjectDiscoveryResult result) => new()
    {
        Name = result.Name,
        Path = result.RootPath,
        Technologies = result.Technologies.ToArray(),
        Branch = "",
        GitStatus = result.HasGitRepository ? "Git detected" : "Git not detected",
        LastActivity = "Imported this session"
    };
}

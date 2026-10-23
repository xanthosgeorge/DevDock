using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class WorkspaceDialogViewModel : ObservableObject
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string ConfirmLabel { get; init; }
    public bool IsChoice { get; init; }
    public IReadOnlyList<ProjectRunConfiguration> Configurations { get; init; } = [];
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))] private ProjectRunConfiguration? _selectedConfiguration;
    private bool CanConfirm => !IsChoice || SelectedConfiguration is not null;
    public event Action<bool>? CloseRequested;
    [RelayCommand(CanExecute = nameof(CanConfirm))] private void Confirm() => CloseRequested?.Invoke(true);
    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(false);
}

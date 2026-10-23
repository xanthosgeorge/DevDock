using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public sealed class PortRowViewModel
{
    public AssociatedPort Source { get; }
    public int Port => Source.Port.Port;
    public string Protocol => Source.Port.Protocol;
    public string Address => Source.Port.LocalAddress;
    public int? Pid => Source.Port.ProcessId;
    public string Process => Source.Port.ProcessName;
    public string Project => Source.Project?.Name ?? "—";
    public string Endpoint => Source.Endpoint?.AbsoluteUri ?? "—";
    public string Status => Source.Port.State;
    public bool IsOwned => Source.Project is not null;
    public bool CanStop => Source.CanStop;
    public bool HasEndpoint => Source.Endpoint is not null;
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public PortRowViewModel(AssociatedPort source, PortsWorkspaceViewModel workspace)
    {
        Source = source;
        OpenCommand = new AsyncRelayCommand(() => workspace.OpenAsync(this));
        CopyCommand = new AsyncRelayCommand(() => workspace.CopyAsync(this));
        StopCommand = new AsyncRelayCommand(() => workspace.StopAsync(this), () => CanStop && !workspace.IsBusy);
    }
    public bool Matches(string search) => new[] { Port.ToString(), Pid?.ToString() ?? "", Process, Project, Address, Endpoint }
        .Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase));
}

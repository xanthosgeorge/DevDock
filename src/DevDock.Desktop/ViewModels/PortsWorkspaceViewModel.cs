using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class PortsWorkspaceViewModel(IPortService service, Func<IReadOnlyList<TrackedPortProject>> projects,
    IPortProcessControl processControl, IPortInteraction? interaction = null, IPlatformService? platform = null) : ObservableObject
{
    private PortScanResult? _scan;
    private CancellationTokenSource? _scanCancellation;
    private PortRowViewModel[] _all = [];
    public ObservableCollection<PortRowViewModel> Rows { get; } = [];
    public ObservableCollection<PortRowViewModel> Preview { get; } = [];
    public string[] Filters { get; } = ["All", "DevDock Projects", "Other Processes"];
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _message = "Refresh to inspect local listening TCP ports.";
    [ObservableProperty] private string _error = "";
    public bool HasError => Error.Length > 0;
    public bool IsEmpty => Rows.Count == 0;
    public bool HasPreview => Preview.Count > 0;
    public bool HasScanned => _scan is not null;
    public string Summary => $"{_all.Length} Listening sockets · {_all.Count(p => p.IsOwned)} DevDock · {_all.Count(p => !p.IsOwned)} Other";
    public string ResultCount => $"{Rows.Count} shown · IPv4 / IPv6 listeners listed separately";
    public string RefreshLabel => IsBusy ? "Refreshing…" : "↻  Refresh";
    partial void OnSearchChanged(string value) => ApplyFilter();
    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(RefreshLabel)); RefreshCommand.NotifyCanExecuteChanged();
        foreach (var row in _all) row.StopCommand.NotifyCanExecuteChanged();
    }
    private bool CanRefresh => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))] private async Task RefreshAsync()
    {
        IsBusy = true; Error = "";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _scanCancellation = cancellation;
        try
        {
            var scan = await service.GetListeningPortsAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _scan = scan;
            Reassociate();
            Message = $"Updated {DateTime.Now:HH:mm:ss}. " + (_scan.Notice ?? "Only local listening TCP sockets are shown.");
        }
        catch (OperationCanceledException) { Message = "Port refresh cancelled or timed out. Refresh to retry."; }
        catch (Exception ex) { Trace.WriteLine(ex); Error = "Unable to inspect local ports. " + ex.Message; Message = "Refresh failed; previous results may be out of date."; }
        finally { _scanCancellation = null; IsBusy = false; }
    }

    [RelayCommand] public void CancelRefresh() => _scanCancellation?.Cancel();
    public void Reassociate()
    {
        if (_scan is null) return;
        var tracked = projects();
        _all = _scan.Ports.Select(p => new PortRowViewModel(PortAssociation.Associate(p, _scan.Processes, tracked), this)).OrderBy(p => p.Port).ThenBy(p => p.Address).ToArray();
        ApplyFilter(); Preview.Clear(); foreach (var row in _all.Take(5)) Preview.Add(row);
        OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(HasScanned));
    }
    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _all.Where(p => (SelectedFilter == "All" || p.IsOwned == (SelectedFilter == "DevDock Projects")) && p.Matches(Search.Trim()))) Rows.Add(row);
        OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(ResultCount));
    }
    public async Task OpenAsync(PortRowViewModel row)
    {
        if (!row.HasEndpoint) return;
        try { if (platform is null) throw new InvalidOperationException("The desktop launcher is unavailable."); await platform.OpenUrlAsync(row.Endpoint); }
        catch (Exception ex) { Error = "Unable to open endpoint. " + ex.Message; }
    }
    public async Task CopyAsync(PortRowViewModel row)
    {
        if (!row.HasEndpoint) return;
        try { if (platform is null) throw new InvalidOperationException("The clipboard is unavailable."); await platform.CopyToClipboardAsync(row.Endpoint); Message = "Copied " + row.Endpoint; }
        catch (Exception ex) { Error = "Unable to copy endpoint. " + ex.Message; }
    }
    public async Task StopAsync(PortRowViewModel row)
    {
        if (IsBusy || !row.CanStop || interaction is null) return;
        IsBusy = true; Error = "";
        try
        {
            if (!await interaction.ConfirmStopAsync(row.Source.Port, row.IsOwned)) return;
            await processControl.StopAsync(row.Source);
            Message = $"Stopped {row.Process} (PID {row.Pid}).";
            _scan = await service.GetListeningPortsAsync(); Reassociate();
        }
        catch (Exception ex) { Trace.WriteLine(ex); Error = "Unable to stop process. " + ex.Message; }
        finally { IsBusy = false; }
    }
}

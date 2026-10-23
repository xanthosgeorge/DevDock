using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class SystemMetricsViewModel : ObservableObject
{
    private readonly ISystemMetricsService _service;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cancellation;
    private Task? _refreshLoop;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string _memoryText = "—";
    [ObservableProperty] private string _diskText = "—";
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryPercent;
    [ObservableProperty] private double _diskPercent;
    [ObservableProperty] private bool _hasCpu;
    [ObservableProperty] private bool _hasMemory;
    [ObservableProperty] private bool _hasDisk;
    [ObservableProperty] private string _status = "Reading system metrics…";
    public string CpuToolTip => "System CPU usage over the latest sampling interval.";
    public string MemoryToolTip => "Physical RAM in use / total usable physical RAM (GiB). Excludes virtual memory and swap.";
    public string DiskToolTip => "Space used / total capacity of drive C: (GiB). This is capacity, not disk activity.";

    public SystemMetricsViewModel(ISystemMetricsService? service = null, TimeSpan? interval = null)
    {
        _service = service ?? SystemMetricsServiceFactory.Create();
        _interval = interval ?? TimeSpan.FromSeconds(2);
        if (_interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
    }

    public void Start()
    {
        if (_refreshLoop is { IsCompleted: false }) return;
        _cancellation?.Dispose();
        _cancellation = new();
        _refreshLoop = RefreshLoopAsync(_cancellation.Token);
    }

    public async Task StopAsync()
    {
        var cancellation = _cancellation; var loop = _refreshLoop;
        if (cancellation is null) return;
        cancellation.Cancel();
        if (loop is not null) await loop;
        if (ReferenceEquals(_cancellation, cancellation)) { _cancellation = null; _refreshLoop = null; cancellation.Dispose(); }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                try
                {
                    var snapshot = await _service.GetSnapshotAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    Apply(snapshot);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    Apply(new(null, null, null, DateTimeOffset.Now, "Metrics unavailable · retrying…"));
                }
                await Task.Delay(_interval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    internal void Apply(SystemMetricsSnapshot snapshot)
    {
        HasCpu = snapshot.CpuPercent is { } cpu && double.IsFinite(cpu) && cpu is >= 0 and <= 100;
        HasMemory = IsValid(snapshot.Memory); HasDisk = IsValid(snapshot.Disk);
        CpuPercent = HasCpu ? snapshot.CpuPercent!.Value : 0;
        MemoryPercent = HasMemory ? snapshot.Memory!.Percent : 0;
        DiskPercent = HasDisk ? snapshot.Disk!.Percent : 0;
        CpuText = HasCpu ? CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%" : "—";
        MemoryText = HasMemory ? FormatCapacity(snapshot.Memory!, "0.0") : "—";
        DiskText = HasDisk ? FormatCapacity(snapshot.Disk!, "0") : "—";
        Status = snapshot.Notice ?? (HasCpu ? "Updates every 2 seconds" : "Sampling CPU…");
    }

    private static bool IsValid(CapacityUsage? usage) => usage is { TotalBytes: > 0 } && usage.UsedBytes <= usage.TotalBytes;
    private static string FormatCapacity(CapacityUsage usage, string format)
    {
        const double gib = 1024d * 1024 * 1024;
        return (usage.UsedBytes / gib).ToString(format, CultureInfo.CurrentCulture) + " / "
            + (usage.TotalBytes / gib).ToString(format, CultureInfo.CurrentCulture) + " GiB";
    }
}

namespace DevDock.Desktop.Models;

public sealed record CapacityUsage(ulong UsedBytes, ulong TotalBytes)
{
    public double Percent => TotalBytes == 0 ? 0 : Math.Clamp(100d * UsedBytes / TotalBytes, 0, 100);
}

public sealed record SystemMetricsSnapshot(double? CpuPercent, CapacityUsage? Memory, CapacityUsage? Disk,
    DateTimeOffset CapturedAt, string? Notice = null);

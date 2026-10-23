using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public static class PortServiceFactory
{
    public static IPortService Create() => OperatingSystem.IsWindows() ? new WindowsPortService()
        : OperatingSystem.IsMacOS() ? new MacOsPortService(new CommandRunner()) : new UnsupportedPortService();
    private sealed class UnsupportedPortService : IPortService
    {
        public Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default)
            => Task.FromException<PortScanResult>(new PlatformNotSupportedException("Port discovery currently supports Windows and macOS."));
    }
}

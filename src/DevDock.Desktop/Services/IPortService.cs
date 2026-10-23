using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IPortService
{
    Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default);
}

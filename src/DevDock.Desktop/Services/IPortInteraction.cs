using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IPortInteraction
{
    Task<bool> ConfirmStopAsync(PortInfo port, bool owned);
}

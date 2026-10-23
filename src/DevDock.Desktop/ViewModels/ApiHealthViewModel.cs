using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public record ApiHealthViewModel(string Name, string Url, ServiceState State, int Code, string Latency);

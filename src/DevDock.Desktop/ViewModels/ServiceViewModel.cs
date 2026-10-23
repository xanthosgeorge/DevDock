using CommunityToolkit.Mvvm.ComponentModel;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class ServiceViewModel(string name, string endpoint, ServiceState state) : ObservableObject
{
    public string Name { get; } = name;
    public string Endpoint { get; } = endpoint;
    [ObservableProperty] private ServiceState _state = state;
}

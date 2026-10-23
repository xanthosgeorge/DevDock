using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class EndpointViewModel(ProjectEndpoint endpoint, IPlatformService? platform, Action<string> reportError, bool isPrimary = false) : ObservableObject
{
    public ProjectEndpoint Endpoint { get; } = endpoint;
    public string Url => Endpoint.Url.AbsoluteUri;
    public string Label => (isPrimary ? "Primary · " : "") + Endpoint.Name + (Endpoint.IsRuntimeDetected ? " · runtime detected" : " · expected, unconfirmed");
    [ObservableProperty] private string _copyLabel = "Copy";
    [RelayCommand] private async Task CopyAsync()
    {
        try
        {
            if (platform is null) throw new InvalidOperationException("The desktop clipboard is unavailable.");
            await platform.CopyToClipboardAsync(Url); CopyLabel = "Copied";
            await Task.Delay(1800); CopyLabel = "Copy";
        }
        catch (Exception ex) { reportError(ex.Message); }
    }
    [RelayCommand] private async Task OpenAsync()
    {
        try
        {
            if (platform is null) throw new InvalidOperationException("The desktop launcher is unavailable.");
            await platform.OpenUrlAsync(Url);
        }
        catch (Exception ex) { reportError(ex.Message); }
    }
}

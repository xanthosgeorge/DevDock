using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace DevDock.Desktop.Services;

public sealed class AvaloniaPlatformService(Window owner) : IPlatformService
{
    public async Task OpenUrlAsync(string url)
    {
        if (!RuntimeEndpointDetector.Validate(url, out var uri)) throw new InvalidOperationException("Only valid HTTP/HTTPS URLs can be opened.");
        var launcher = TopLevel.GetTopLevel(owner)?.Launcher ?? throw new InvalidOperationException("The desktop launcher is unavailable.");
        if (!await launcher.LaunchUriAsync(uri!)) throw new InvalidOperationException("The default browser could not be opened.");
    }
    public async Task CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard ?? throw new InvalidOperationException("The clipboard is unavailable.");
        await clipboard.SetTextAsync(text);
    }
}

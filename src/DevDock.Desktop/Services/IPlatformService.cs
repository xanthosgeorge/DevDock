namespace DevDock.Desktop.Services;

public interface IPlatformService
{
    Task OpenUrlAsync(string url);
    Task CopyToClipboardAsync(string text);
}

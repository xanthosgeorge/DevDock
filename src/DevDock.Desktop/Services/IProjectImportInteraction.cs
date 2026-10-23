using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

/// <summary>UI boundary for the native picker and modal import dialogs.</summary>
public interface IProjectImportInteraction
{
    Task<string?> PickFolderAsync();
    Task<bool> ConfirmImportAsync(ProjectDiscoveryResult result);
    Task ShowMessageAsync(string title, string message);
}

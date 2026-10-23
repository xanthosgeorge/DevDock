using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DevDock.Desktop.Models;
using DevDock.Desktop.ViewModels;
using DevDock.Desktop.Views;

namespace DevDock.Desktop.Services;

public sealed class AvaloniaProjectImportInteraction(Window owner) : IProjectImportInteraction
{
    public async Task<string?> PickFolderAsync()
    {
        if (!owner.StorageProvider.CanPickFolder)
            throw new NotSupportedException("Folder selection is not available on this platform.");
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a local project to DevDock",
            AllowMultiple = false
        });
        if (folders.Count == 0) return null;
        using var folder = folders[0];
        return folder.TryGetLocalPath() ?? throw new NotSupportedException("Choose a local filesystem folder to import this project.");
    }

    public Task<bool> ConfirmImportAsync(ProjectDiscoveryResult result) =>
        new ProjectImportWindow(ProjectImportViewModel.Preview(result)).ShowDialog<bool>(owner);

    public async Task ShowMessageAsync(string title, string message) =>
        await new ProjectImportWindow(ProjectImportViewModel.Message(title, message)).ShowDialog<bool>(owner);
}

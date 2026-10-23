using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IWorkspaceStore
{
    Task<IReadOnlyList<SavedProject>> LoadAsync();
    Task SaveAsync(IReadOnlyList<SavedProject> projects);
}

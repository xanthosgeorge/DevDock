using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.ViewModels;

public partial class ProjectImportViewModel
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public ProjectDiscoveryResult? Discovery { get; init; }
    public bool IsPreview => Discovery is not null;
    public string Name => Discovery?.Name ?? "";
    public string RootPath => Discovery?.RootPath ?? "";
    public IReadOnlyList<string> Technologies => Discovery?.Technologies ?? [];
    public IReadOnlyList<DetectedProjectFile> ProjectFiles => Discovery?.ProjectFiles ?? [];
    public IReadOnlyList<string> Warnings => Discovery?.Warnings ?? [];
    public bool HasWarnings => Warnings.Count > 0;
    public string GitSummary => Discovery?.HasGitRepository == true ? "✓ Git repository detected" : "Git not detected";
    public string DismissLabel => IsPreview ? "Cancel" : "Got it";
    public event Action<bool>? CloseRequested;

    public static ProjectImportViewModel Preview(ProjectDiscoveryResult result) => new()
    {
        Title = "Project detected",
        Description = "Review this local project before adding it to your workspace.",
        Discovery = result
    };

    public static ProjectImportViewModel Message(string title, string message) => new() { Title = title, Description = message };
    [RelayCommand] private void Confirm() { if (IsPreview) CloseRequested?.Invoke(true); }
    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(false);
}

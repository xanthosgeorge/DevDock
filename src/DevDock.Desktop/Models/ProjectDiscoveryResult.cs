namespace DevDock.Desktop.Models;

public sealed record DetectedProjectFile(string RelativePath, string Kind);

public sealed class ProjectDiscoveryResult
{
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public IReadOnlyList<string> Technologies { get; init; } = [];
    public IReadOnlyList<DetectedProjectFile> ProjectFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool HasDocker { get; init; }
    public bool HasGitRepository { get; init; }
    public string? SolutionFile { get; init; }
    public bool IsSupported { get; init; }
}

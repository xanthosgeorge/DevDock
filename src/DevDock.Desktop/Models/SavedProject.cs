namespace DevDock.Desktop.Models;

/// <summary>Workspace metadata only. Processes, output, credentials and Git state are never persisted.</summary>
public sealed record SavedProject(Guid Id, string Name, string Path, string[] Technologies);

namespace DevDock.Desktop.Models;

public sealed record GitFileChange(string Path, char IndexStatus, char WorkTreeStatus, string? OriginalPath = null, bool IsConflict = false)
{
    public bool IsUntracked => IndexStatus == '?';
    public bool IsStaged => !IsUntracked && IndexStatus != '.';
    public bool IsUnstaged => IsUntracked || WorkTreeStatus != '.';
    public string IndexLabel => IndexStatus.ToString();
    public string WorkTreeLabel => IsUntracked ? "??" : WorkTreeStatus.ToString();
}

public sealed class GitRepositoryStatus
{
    public bool IsRepository { get; init; }
    public string RootPath { get; init; } = "";
    public string Branch { get; init; } = "";
    public string? HeadCommit { get; init; }
    public string? Upstream { get; init; }
    public int Ahead { get; init; }
    public int Behind { get; init; }
    public IReadOnlyList<GitFileChange> Files { get; init; } = [];
    public IReadOnlyList<GitRemote> Remotes { get; init; } = [];
    public bool IsClean => Files.Count == 0;
    public bool HasConflicts => Files.Any(f => f.IsConflict);
    public string Summary => IsClean ? "Clean" : $"{Files.Count} changed {(Files.Count == 1 ? "file" : "files")}";
}

public sealed record GitCommit(string Hash, string ShortHash, string Message, string Author, DateTimeOffset Date, bool IsHead)
{
    public string DateLabel => Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string HeadLabel => IsHead ? "● HEAD" : "";
}

public sealed record GitRemote(string Name, string DisplayUrl);

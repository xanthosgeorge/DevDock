using System.ComponentModel;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class GitUnavailableException() : Exception("Git was not found on this system.");

public sealed class GitCliService(ICommandRunner runner, string executable = "git") : IGitService
{
    private async Task<CommandResult> Execute(string path, IEnumerable<string> arguments, string? input = null, bool allowFailure = false)
    {
        CommandResult result;
        try { result = await runner.ExecuteAsync(executable, new[] { "--no-pager", "--literal-pathspecs", "-c", "color.ui=false" }.Concat(arguments), path, input); }
        catch (OperationCanceledException) { throw new InvalidOperationException("Git operation timed out or was cancelled. Check connectivity and existing credential configuration."); }
        if (result.OutputTruncated) throw new InvalidOperationException("Git output exceeded the safe display limit. Narrow the repository scope or use your Git client.");
        if (!allowFailure && result.ExitCode != 0)
        {
            var error = GitOutputParser.RedactCredentials(string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr).Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Git exited with code {result.ExitCode}." : error[..Math.Min(error.Length, 4000)]);
        }
        return result;
    }

    public async Task<GitRepositoryStatus> GetStatusAsync(string repositoryPath)
    {
        try { await Execute(Environment.CurrentDirectory, ["--version"]); }
        catch (Win32Exception) { throw new GitUnavailableException(); }
        var rootResult = await Execute(repositoryPath, ["rev-parse", "--show-toplevel"], allowFailure: true);
        if (rootResult.ExitCode != 0)
        {
            if (rootResult.StdErr.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)) return new();
            throw new InvalidOperationException(GitOutputParser.RedactCredentials(rootResult.StdErr).Trim());
        }
        var root = rootResult.StdOut.EndsWith('\n') ? rootResult.StdOut[..^1] : rootResult.StdOut;
        if (OperatingSystem.IsWindows()) root = root.TrimEnd('\r');
        root = ProjectPath.Normalize(root);
        var status = await Execute(root, ["status", "--porcelain=v2", "--branch", "-z", "--untracked-files=all"]);
        var remotes = await Execute(root, ["config", "--null", "--get-regexp", @"^remote\..*\.url$"], allowFailure: true);
        if (remotes.ExitCode is not (0 or 1)) throw new InvalidOperationException("Unable to read Git remotes.");
        return GitOutputParser.ParseStatus(status.StdOut, root, GitOutputParser.ParseRemotes(remotes.StdOut));
    }

    public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repositoryPath, int count = 50)
    {
        var status = await RequireRepository(repositoryPath);
        if (status.HeadCommit is null) return [];
        var history = await Execute(status.RootPath, ["log", $"--max-count={Math.Clamp(count, 1, 500)}", "-z", "--format=%H%x00%h%x00%s%x00%an%x00%aI", "--"]);
        return GitOutputParser.ParseHistory(history.StdOut);
    }

    private async Task<GitRepositoryStatus> RequireRepository(string path)
    {
        var status = await GetStatusAsync(path);
        if (!status.IsRepository) throw new InvalidOperationException("This project is not a Git repository.");
        return status;
    }

    public async Task FetchAsync(string repositoryPath)
    {
        var status = await RequireRepository(repositoryPath);
        if (status.Remotes.Count == 0) throw new InvalidOperationException("No Git remote is configured for this repository.");
        await Execute(status.RootPath, ["fetch"]);
    }

    public async Task PullAsync(string repositoryPath)
    {
        var status = await RequireRepository(repositoryPath);
        if (status.HasConflicts) throw new InvalidOperationException("The repository has unresolved conflicts. Resolve them in your Git client before pulling.");
        await Execute(status.RootPath, ["-c", "rebase.autoStash=false", "-c", "merge.autoStash=false", "pull", "--ff-only", "--no-rebase", "--no-autostash", "--no-squash"]);
    }

    public async Task StageAsync(string repositoryPath, IEnumerable<string> files)
    {
        var status = await RequireRepository(repositoryPath);
        var paths = ExpandPaths(status, files);
        if (paths.Length > 0) await Execute(status.RootPath, new[] { "add", "--all", "--" }.Concat(paths));
    }

    public async Task StageAllAsync(string repositoryPath)
    {
        var status = await RequireRepository(repositoryPath);
        await Execute(status.RootPath, ["add", "--all", "--", "."]);
    }

    public async Task UnstageAsync(string repositoryPath, IEnumerable<string> files)
    {
        var status = await RequireRepository(repositoryPath);
        var paths = ExpandPaths(status, files);
        if (paths.Length == 0) return;
        // --cached only updates the index, including on unborn branches. No worktree files are removed.
        var args = status.HeadCommit is null ? new[] { "rm", "--cached", "--force", "--ignore-unmatch", "--" } : new[] { "restore", "--staged", "--" };
        await Execute(status.RootPath, args.Concat(paths));
    }

    public async Task CommitAsync(string repositoryPath, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new InvalidOperationException("Enter a commit message.");
        var status = await RequireRepository(repositoryPath);
        if (status.HasConflicts) throw new InvalidOperationException("Resolve Git conflicts before committing.");
        if (!status.Files.Any(f => f.IsStaged)) throw new InvalidOperationException("Stage at least one change before committing.");
        await Execute(status.RootPath, ["commit", "--file=-"], message);
    }

    private static string[] ExpandPaths(GitRepositoryStatus status, IEnumerable<string> files)
    {
        var paths = files.Distinct(StringComparer.Ordinal).ToList();
        foreach (var change in status.Files.Where(f => paths.Contains(f.Path) && f.OriginalPath is not null).ToArray()) paths.Add(change.OriginalPath!);
        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.IndexOf('\0') >= 0 || path.Split('/', '\\').Contains(".."))
                throw new InvalidOperationException("Only repository-relative file paths can be staged or unstaged.");
            var full = Path.GetFullPath(Path.Combine(status.RootPath, path));
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(status.RootPath) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("File is outside the repository.");
        }
        return paths.Distinct(StringComparer.Ordinal).ToArray();
    }
}

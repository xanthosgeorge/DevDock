using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class GitCliServiceTests
{
    [Fact]
    public async Task StagesUnstagesAndCommitsLiteralFilenamesWithoutChangingWorkingFiles()
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        repo.Write("file[1] & ü.txt", "first"); repo.Write("file1 & ü.txt", "other"); repo.Write("--help.txt", "not an option");
        await repo.Git.StageAsync(repo.Root, ["file[1] & ü.txt", "--help.txt"]);
        var status = await repo.Git.GetStatusAsync(repo.Root);
        Assert.Equal(2, status.Files.Count(f => f.IsStaged));
        Assert.True(status.Files.Single(f => f.Path == "file1 & ü.txt").IsUntracked);
        repo.Write("file[1] & ü.txt", "modified after staging");
        await repo.Git.UnstageAsync(repo.Root, ["file[1] & ü.txt"]);
        Assert.Equal("modified after staging", File.ReadAllText(Path.Combine(repo.Root, "file[1] & ü.txt")));
        await repo.Git.StageAllAsync(repo.Root);
        await repo.Git.CommitAsync(repo.Root, "Initial commit\n\nMultiline message with 'quotes' & arguments.");
        Assert.True((await repo.Git.GetStatusAsync(repo.Root)).IsClean);
        var history = await repo.Git.GetHistoryAsync(repo.Root);
        var commit = Assert.Single(history);
        Assert.Equal("Initial commit", commit.Message); Assert.True(commit.IsHead);
        Assert.Equal("DevDock Tests", commit.Author);
    }

    [Fact]
    public async Task HandlesUnbornHistoryAndRejectsEmptyOrUnstagedCommits()
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        Assert.Empty(await repo.Git.GetHistoryAsync(repo.Root));
        var remoteError = await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.FetchAsync(repo.Root));
        Assert.Contains("No Git remote", remoteError.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.CommitAsync(repo.Root, ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.CommitAsync(repo.Root, "Nothing staged"));
        repo.Write("tracked.txt", "new");
        await repo.Git.StageAllAsync(repo.Root);
        await repo.Git.UnstageAsync(repo.Root, ["tracked.txt"]);
        Assert.True(File.Exists(Path.Combine(repo.Root, "tracked.txt")));
        Assert.DoesNotContain((await repo.Git.GetStatusAsync(repo.Root)).Files, f => f.IsStaged);
    }

    [Fact]
    public async Task UnstagesRenamesWithoutRestoringOrDeletingSourceFiles()
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        repo.Write("before.txt", "unchanged contents"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Initial");
        File.Move(Path.Combine(repo.Root, "before.txt"), Path.Combine(repo.Root, "after.txt"));
        await repo.Git.StageAllAsync(repo.Root);
        Assert.Equal("before.txt", Assert.Single((await repo.Git.GetStatusAsync(repo.Root)).Files).OriginalPath);
        await repo.Git.UnstageAsync(repo.Root, ["after.txt"]);
        Assert.True(File.Exists(Path.Combine(repo.Root, "after.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Root, "before.txt")));
        Assert.DoesNotContain((await repo.Git.GetStatusAsync(repo.Root)).Files, f => f.IsStaged);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("nested/../../outside.txt")]
    public async Task RejectsPathsOutsideRepository(string path)
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.StageAsync(repo.Root, [path]));
    }

    [Fact]
    public async Task NonRepositoryAndMissingGitRemainRecoverable()
    {
        using var folder = new TemporaryProject();
        Assert.False((await new GitCliService(new CommandRunner()).GetStatusAsync(folder.Root)).IsRepository);
        await Assert.ThrowsAsync<GitUnavailableException>(() => new GitCliService(new CommandRunner(), "devdock-git-not-installed-8f1a").GetStatusAsync(folder.Root));
    }

    [Fact]
    public async Task PullRefusesDivergenceWithoutCreatingMergeOrDiscardingCommits()
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        repo.Write("tracked.txt", "initial"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Initial");
        var bare = Path.Combine(repo.Files.Root, "remote.git"); var checkout = Path.Combine(repo.Files.Root, "checkout");
        await repo.NativeAsync(repo.Files.Root, "clone", "--bare", repo.Root, bare);
        await repo.NativeAsync(repo.Files.Root, "clone", bare, checkout);
        await repo.NativeAsync(checkout, "config", "user.name", "DevDock Tests");
        await repo.NativeAsync(checkout, "config", "user.email", "devdock-tests@example.invalid");
        await repo.NativeAsync(checkout, "config", "commit.gpgsign", "false");
        await repo.NativeAsync(checkout, "config", "core.hooksPath", Path.Combine(repo.Files.Root, "no-hooks"));
        File.WriteAllText(Path.Combine(checkout, "local.txt"), "my commit");
        await repo.Git.StageAllAsync(checkout); await repo.Git.CommitAsync(checkout, "Local commit");
        var before = (await repo.Git.GetStatusAsync(checkout)).HeadCommit;
        repo.Write("upstream.txt", "their commit"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Remote commit");
        await repo.NativeAsync(repo.Root, "push", bare, "main");
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.PullAsync(checkout));
        Assert.Equal(before, (await repo.Git.GetStatusAsync(checkout)).HeadCommit);
        Assert.Equal("my commit", File.ReadAllText(Path.Combine(checkout, "local.txt")));
    }

    [Fact]
    public async Task FetchAndFastForwardPullUseOnlyLocalRemoteAndPreserveDirtyChanges()
    {
        using var repo = new TemporaryGitRepository(); await repo.InitializeAsync();
        repo.Write("tracked.txt", "initial"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Initial");
        var bare = Path.Combine(repo.Files.Root, "remote.git");
        var checkout = Path.Combine(repo.Files.Root, "checkout");
        await repo.NativeAsync(repo.Files.Root, "clone", "--bare", repo.Root, bare);
        await repo.NativeAsync(repo.Files.Root, "clone", bare, checkout);
        repo.Write("tracked.txt", "upstream update"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Upstream");
        await repo.NativeAsync(repo.Root, "push", bare, "main"); // Local disposable bare repository only.
        await repo.Git.FetchAsync(checkout);
        Assert.Equal(1, (await repo.Git.GetStatusAsync(checkout)).Behind);
        await repo.NativeAsync(checkout, "config", "pull.squash", "true");
        await repo.Git.PullAsync(checkout);
        Assert.Equal("upstream update", File.ReadAllText(Path.Combine(checkout, "tracked.txt")));
        Assert.True((await repo.Git.GetStatusAsync(checkout)).IsClean);
        Assert.Equal(0, (await repo.Git.GetStatusAsync(checkout)).Behind);
        File.WriteAllText(Path.Combine(checkout, "tracked.txt"), "local work must survive");
        await repo.NativeAsync(checkout, "config", "pull.rebase", "true");
        await repo.NativeAsync(checkout, "config", "rebase.autoStash", "true");
        await repo.NativeAsync(checkout, "config", "merge.autoStash", "true");
        repo.Write("tracked.txt", "another upstream update"); await repo.Git.StageAllAsync(repo.Root); await repo.Git.CommitAsync(repo.Root, "Another upstream");
        await repo.NativeAsync(repo.Root, "push", bare, "main");
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Git.PullAsync(checkout));
        Assert.Equal("local work must survive", File.ReadAllText(Path.Combine(checkout, "tracked.txt")));
        Assert.Empty((await repo.NativeAsync(checkout, "stash", "list", "--format=%H")).StdOut);
    }
}

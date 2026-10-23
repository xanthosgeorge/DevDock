using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class GitWorkspaceViewModelTests
{
    [Fact]
    public async Task CommitRequiresMessageAndStagedChangesAndGitOperationsExcludeEachOther()
    {
        var service = new TestGit();
        var project = ProjectViewModel.FromDiscovery(new() { Name = "Test", RootPath = Path.GetTempPath(), IsSupported = true });
        var workspace = new GitWorkspaceViewModel(service, project);
        await workspace.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("main", project.Branch); Assert.Equal("1 changed file", project.GitStatus);
        Assert.False(workspace.CommitCommand.CanExecute(null));
        workspace.CommitMessage = "   "; Assert.False(workspace.CommitCommand.CanExecute(null));
        workspace.CommitMessage = "Commit"; Assert.True(workspace.CommitCommand.CanExecute(null));
        var fetch = workspace.FetchCommand.ExecuteAsync(null);
        Assert.True(workspace.IsBusy); Assert.False(workspace.PullCommand.CanExecute(null)); Assert.False(workspace.CommitCommand.CanExecute(null));
        service.FetchCompletion.SetResult(); await fetch;
        Assert.False(workspace.IsBusy); Assert.Equal("Fetch completed.", workspace.Message);
        project.IsRemoving = true;
        Assert.False(workspace.FetchCommand.CanExecute(null)); Assert.False(workspace.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingGitDisablesActionsAndShowsExactMessage()
    {
        var project = ProjectViewModel.FromDiscovery(new() { Name = "Test", RootPath = Path.GetTempPath(), IsSupported = true });
        var service = new TestGit { Missing = true };
        var workspace = new GitWorkspaceViewModel(service, project);
        await workspace.RefreshCommand.ExecuteAsync(null);
        Assert.False(workspace.GitAvailable);
        Assert.Equal("Git was not found on this system.", workspace.Error);
        Assert.False(workspace.FetchCommand.CanExecute(null)); Assert.False(workspace.StageAllCommand.CanExecute(null));
        Assert.True(workspace.RefreshCommand.CanExecute(null));
    }

    private sealed class TestGit : IGitService
    {
        public bool Missing { get; init; }
        public TaskCompletionSource FetchCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<GitRepositoryStatus> GetStatusAsync(string repositoryPath) => Missing
            ? Task.FromException<GitRepositoryStatus>(new GitUnavailableException())
            : Task.FromResult(new GitRepositoryStatus { IsRepository = true, Branch = "main", RootPath = repositoryPath, HeadCommit = "abc", Files = [new("file.cs", 'M', '.')] });
        public Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repositoryPath, int count = 50) => Task.FromResult<IReadOnlyList<GitCommit>>([]);
        public Task FetchAsync(string repositoryPath) => FetchCompletion.Task;
        public Task PullAsync(string repositoryPath) => Task.CompletedTask;
        public Task StageAsync(string repositoryPath, IEnumerable<string> files) => Task.CompletedTask;
        public Task StageAllAsync(string repositoryPath) => Task.CompletedTask;
        public Task UnstageAsync(string repositoryPath, IEnumerable<string> files) => Task.CompletedTask;
        public Task CommitAsync(string repositoryPath, string message) => Task.CompletedTask;
    }
}

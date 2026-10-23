using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class ProjectRemovalTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovalOnlyUpdatesDashboardAfterConfirmation(bool confirm)
    {
        using var project = new TemporaryProject();
        project.Write("App.csproj", "<Project />"); project.Write("source.cs", "source must survive"); project.Write(".git/HEAD", "ref: refs/heads/main");
        var interaction = new Interaction(project.Root, confirm);
        var dashboard = new DashboardViewModel(new ProjectDiscoveryService(), interaction, interaction, new FakeGit());
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        var local = dashboard.Projects[0];
        var removed = await dashboard.RemoveProjectAsync(local);
        Assert.Equal(confirm, removed);
        Assert.Equal(confirm ? 0 : 1, dashboard.Projects.Count);
        Assert.Equal(confirm, dashboard.IsEmpty);
        Assert.True(File.Exists(Path.Combine(project.Root, "App.csproj")));
        Assert.Equal("source must survive", File.ReadAllText(Path.Combine(project.Root, "source.cs")));
        Assert.True(File.Exists(Path.Combine(project.Root, ".git", "HEAD")));
        if (confirm) Assert.Empty(dashboard.Projects);
        else Assert.Single(dashboard.Projects, p => p.IsSelected);
        Assert.False(local.IsRemoving);
    }

    private sealed class Interaction(string path, bool confirm) : IProjectImportInteraction, IProjectWorkspaceInteraction
    {
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(path);
        public Task<bool> ConfirmImportAsync(ProjectDiscoveryResult result) => Task.FromResult(true);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmRemovalAsync(string name, bool running) => Task.FromResult(confirm);
        public Task<ProjectRunConfiguration?> ChooseRunConfigurationAsync(string name, IReadOnlyList<ProjectRunConfiguration> configurations) => Task.FromResult<ProjectRunConfiguration?>(null);
        public void OpenProject(ProjectWorkspaceViewModel workspace) { }
        public void CloseProject(Guid projectId) { }
    }

    internal sealed class FakeGit : IGitService
    {
        public Task<GitRepositoryStatus> GetStatusAsync(string repositoryPath) => Task.FromResult(new GitRepositoryStatus());
        public Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repositoryPath, int count = 50) => Task.FromResult<IReadOnlyList<GitCommit>>([]);
        public Task FetchAsync(string repositoryPath) => Task.CompletedTask;
        public Task PullAsync(string repositoryPath) => Task.CompletedTask;
        public Task StageAsync(string repositoryPath, IEnumerable<string> files) => Task.CompletedTask;
        public Task StageAllAsync(string repositoryPath) => Task.CompletedTask;
        public Task UnstageAsync(string repositoryPath, IEnumerable<string> files) => Task.CompletedTask;
        public Task CommitAsync(string repositoryPath, string message) => Task.CompletedTask;
    }
}

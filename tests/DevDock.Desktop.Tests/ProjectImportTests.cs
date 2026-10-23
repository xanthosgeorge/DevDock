using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class ProjectImportTests
{
    private static ProjectDiscoveryResult Result(string path) => new()
    {
        Name = "Detected project", RootPath = ProjectPath.Normalize(path), IsSupported = true,
        Technologies = [".NET 10", "Docker"], HasDocker = true, HasGitRepository = true
    };

    [Fact]
    public async Task ConfirmedImportUpdatesCountsSelectsProjectAndNavigatesWithoutInventingState()
    {
        var interaction = new FakeInteraction();
        var dashboard = new DashboardViewModel(new FakeDiscovery(Result(interaction.Path!)), interaction);
        var main = new MainWindowViewModel(dashboard);
        main.NavigateCommand.Execute("Docker");
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.Equal(5, dashboard.Projects.Count);
        Assert.Equal("5 Projects", dashboard.ProjectSummary);
        Assert.Equal("5 projects", dashboard.ProjectCountLabel);
        Assert.Equal("3 Running", dashboard.RunningSummary);
        Assert.Equal("Projects", main.PageTitle);
        var project = dashboard.Projects[0];
        Assert.False(project.IsDemo);
        Assert.True(project.IsSelected);
        Assert.Single(dashboard.Projects, p => p.IsSelected);
        Assert.Equal("Git detected", project.GitStatus);
        Assert.Empty(project.Branch);
        Assert.Empty(project.Services);
        Assert.False(project.ToggleCommand.CanExecute(null));
        Assert.False(project.IsRunning);
        Assert.Equal(4, dashboard.Projects.Count(p => p.IsDemo));
    }

    [Fact]
    public async Task PickerCancellationDoesNotScanOrChangeTheDashboard()
    {
        var interaction = new FakeInteraction { Path = null };
        var discovery = new FakeDiscovery(Result(Path.GetTempPath()));
        var dashboard = new DashboardViewModel(discovery, interaction);
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.Equal(0, discovery.Calls);
        Assert.Equal(4, dashboard.Projects.Count);
        Assert.Null(interaction.Preview);
    }

    [Fact]
    public async Task PreviewCancellationDoesNotImport()
    {
        var interaction = new FakeInteraction { Confirm = false };
        var dashboard = new DashboardViewModel(new FakeDiscovery(Result(interaction.Path!)), interaction);
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.NotNull(interaction.Preview);
        Assert.Equal(4, dashboard.Projects.Count);
        Assert.Single(dashboard.Projects, p => p.IsSelected);
    }

    [Fact]
    public async Task DuplicateNormalizedPathIsRejectedBeforeScanning()
    {
        var interaction = new FakeInteraction();
        var discovery = new FakeDiscovery(Result(interaction.Path!));
        var dashboard = new DashboardViewModel(discovery, interaction);
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        interaction.Path = Path.Combine(interaction.Path!, ".") + Path.DirectorySeparatorChar;
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.Equal(5, dashboard.Projects.Count);
        Assert.Equal(1, discovery.Calls);
        Assert.Contains("already in DevDock", interaction.LastMessage);
    }

    [Fact]
    public void PathComparisonUsesHostRulesAndPreservesFilesystemRoots()
    {
        Assert.Equal(OperatingSystem.IsWindows(), ProjectPath.Comparer.Equals("Example", "example"));
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.Equal(root, ProjectPath.Normalize(root));
    }

    [Fact]
    public async Task UnsupportedFolderShowsExplanationWithoutPreviewOrImport()
    {
        var interaction = new FakeInteraction();
        var result = new ProjectDiscoveryResult { Name = "Empty", RootPath = interaction.Path!, IsSupported = false };
        var dashboard = new DashboardViewModel(new FakeDiscovery(result), interaction);
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.Null(interaction.Preview);
        Assert.Contains("No supported development project", interaction.LastMessage);
        Assert.Equal(4, dashboard.Projects.Count);
    }

    [Fact]
    public async Task ScanningDisablesReentryAndResetsLoadingAfterCompletion()
    {
        var interaction = new FakeInteraction { Confirm = false };
        var completion = new TaskCompletionSource<ProjectDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dashboard = new DashboardViewModel(new PendingDiscovery(completion.Task), interaction);
        var operation = dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.True(dashboard.IsScanning);
        Assert.Contains("Scanning", dashboard.AddProjectLabel);
        Assert.False(dashboard.AddProjectCommand.CanExecute(null));
        completion.SetResult(Result(interaction.Path!));
        await operation;
        Assert.False(dashboard.IsScanning);
        Assert.True(dashboard.AddProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task AccessFailureShowsFriendlyMessageAndAllowsRetry()
    {
        var interaction = new FakeInteraction();
        var dashboard = new DashboardViewModel(new PendingDiscovery(Task.FromException<ProjectDiscoveryResult>(new UnauthorizedAccessException())), interaction);
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.False(dashboard.IsScanning);
        Assert.True(dashboard.AddProjectCommand.CanExecute(null));
        Assert.Contains("permission", interaction.LastMessage);
        Assert.Equal(4, dashboard.Projects.Count);
    }

    [Fact]
    public void LocalProjectWithoutGitDoesNotShowBranchOrPretendToBeRunning()
    {
        var result = new ProjectDiscoveryResult { Name = "Node", RootPath = Path.GetTempPath(), Technologies = ["Node.js"], IsSupported = true };
        var project = ProjectViewModel.FromDiscovery(result);
        Assert.Equal("Git not detected", project.GitStatus);
        Assert.False(project.HasBranch);
        Assert.False(project.IsRunning);
    }

    private sealed class FakeDiscovery(ProjectDiscoveryResult result) : IProjectDiscoveryService
    {
        public int Calls { get; private set; }
        public Task<ProjectDiscoveryResult> DiscoverAsync(string projectPath) { Calls++; return Task.FromResult(result); }
    }
    private sealed class PendingDiscovery(Task<ProjectDiscoveryResult> pending) : IProjectDiscoveryService
    {
        public Task<ProjectDiscoveryResult> DiscoverAsync(string projectPath) => pending;
    }
    private sealed class FakeInteraction : IProjectImportInteraction
    {
        public string? Path { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DevDock Import", Guid.NewGuid().ToString("N"));
        public bool Confirm { get; init; } = true;
        public ProjectDiscoveryResult? Preview { get; private set; }
        public string LastMessage { get; private set; } = "";
        public Task<string?> PickFolderAsync() => Task.FromResult(Path);
        public Task<bool> ConfirmImportAsync(ProjectDiscoveryResult result) { Preview = result; return Task.FromResult(Confirm); }
        public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
    }
}

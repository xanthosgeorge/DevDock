using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public async Task ImportedProjectSurvivesRestartAndConfirmedRemovalStaysRemoved()
    {
        using var fixture = new TemporaryProject();
        fixture.Write("App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        fixture.Write("App/source.cs", "keep this file");
        var path = Path.Combine(fixture.Root, "workspace.json");
        var interaction = new Interaction(Path.Combine(fixture.Root, "App"));
        var first = Dashboard(path, interaction);
        Assert.False(first.AddProjectCommand.CanExecute(null));
        await first.InitializeAsync();
        Assert.True(first.AddProjectCommand.CanExecute(null));
        await first.AddProjectCommand.ExecuteAsync(null);
        var imported = Assert.Single(first.Projects);
        var id = imported.Id;
        Assert.True(File.Exists(path)); // Saved immediately, without requiring a graceful shutdown.

        var reopened = Dashboard(path, interaction);
        await reopened.InitializeAsync();
        await reopened.InitializeAsync(); // Initialization is idempotent.
        var restored = Assert.Single(reopened.Projects);
        Assert.Equal(id, restored.Id); Assert.Equal(imported.Name, restored.Name);
        Assert.Equal(imported.Path, restored.Path); Assert.Equal(imported.Technologies, restored.Technologies);
        Assert.False(restored.IsRunning); Assert.NotNull(restored.Workspace);
        Assert.Null(restored.Workspace.Process); Assert.Empty(restored.Workspace.Output);
        Assert.True(restored.IsSelected);
        await reopened.AddProjectCommand.ExecuteAsync(null); Assert.Single(reopened.Projects);
        Assert.Contains("already", interaction.Message);

        interaction.ConfirmRemove = false;
        Assert.False(await reopened.RemoveProjectAsync(restored));
        Assert.Single(await new JsonWorkspaceStore(path).LoadAsync());
        interaction.ConfirmRemove = true;
        Assert.True(await reopened.RemoveProjectAsync(restored));
        var afterRemoval = Dashboard(path, interaction);
        await afterRemoval.InitializeAsync(); Assert.Empty(afterRemoval.Projects);
        Assert.Equal("keep this file", File.ReadAllText(Path.Combine(fixture.Root, "App", "source.cs")));
    }

    [Fact]
    public async Task JsonRoundTripPreservesUnicodeOrderAndMissingFoldersWithoutRuntimeState()
    {
        using var fixture = new TemporaryProject();
        var path = Path.Combine(fixture.Root, "settings", "workspace.json");
        var entries = new[]
        {
            new SavedProject(Guid.NewGuid(), "Εφαρμογή Α", Path.Combine(fixture.Root, "missing A"), [".NET 10"]),
            new SavedProject(Guid.NewGuid(), "App B", fixture.Root, ["Node.js", "Angular"])
        };
        Assert.Empty(await new JsonWorkspaceStore(path).LoadAsync());
        await new JsonWorkspaceStore(path).SaveAsync(entries);
        var restored = await new JsonWorkspaceStore(path).LoadAsync();
        Assert.Equal(entries.Select(p => p.Id), restored.Select(p => p.Id));
        Assert.Equal(entries.Select(p => p.Path), restored.Select(p => p.Path));
        var dashboard = Dashboard(path, new Interaction(fixture.Root));
        await dashboard.InitializeAsync();
        Assert.Equal("Εφαρμογή Α", dashboard.Projects[0].Name);
        Assert.True(dashboard.Projects[0].HasLocationNotice); Assert.False(dashboard.Projects[1].HasLocationNotice);
        Assert.All(dashboard.Projects, p => Assert.False(p.IsRunning));
        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("IsRunning", json); Assert.DoesNotContain("Process", json);
        Assert.DoesNotContain("GitStatus", json); Assert.DoesNotContain("Output", json);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Theory]
    [InlineData("broken json")]
    [InlineData("{\"Version\":99,\"Projects\":[]}")]
    [InlineData("{\"Version\":1,\"Projects\":[{}]}")]
    public async Task UnreadableWorkspaceIsPreservedAndCanBeRetried(string contents)
    {
        using var fixture = new TemporaryProject();
        var path = Path.Combine(fixture.Root, "workspace.json");
        await File.WriteAllTextAsync(path, contents);
        var dashboard = Dashboard(path, new Interaction(fixture.Root));
        await dashboard.InitializeAsync();
        Assert.True(dashboard.HasWorkspaceError); Assert.True(dashboard.NeedsWorkspaceReload);
        Assert.False(dashboard.AddProjectCommand.CanExecute(null));
        Assert.Equal(contents, await File.ReadAllTextAsync(path));
        await File.WriteAllTextAsync(path, "{\"Version\":1,\"Projects\":[]}");
        await dashboard.InitializeAsync();
        Assert.False(dashboard.HasWorkspaceError); Assert.False(dashboard.NeedsWorkspaceReload);
        Assert.True(dashboard.AddProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedSaveDoesNotClaimSuccessfulImportOrRemoval()
    {
        using var fixture = new TemporaryProject();
        fixture.Write("App.csproj", "<Project />");
        var store = new FailingStore(); var interaction = new Interaction(fixture.Root);
        var dashboard = new DashboardViewModel(importInteraction: interaction, workspaceInteraction: interaction,
            git: new ProjectRemovalTests.FakeGit(), workspaceStore: store);
        await dashboard.InitializeAsync();
        store.Fail = true;
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        Assert.Empty(dashboard.Projects); Assert.True(dashboard.HasWorkspaceError);
        Assert.Contains("save", interaction.Message);
        store.Fail = false;
        await dashboard.AddProjectCommand.ExecuteAsync(null);
        var project = Assert.Single(dashboard.Projects);
        Assert.False(dashboard.HasWorkspaceError);
        store.Fail = true;
        Assert.False(await dashboard.RemoveProjectAsync(project));
        Assert.Single(dashboard.Projects); Assert.Single(store.Projects);
        store.Fail = false;
        Assert.True(await dashboard.RemoveProjectAsync(project)); Assert.Empty(store.Projects);
    }

    [Fact]
    public async Task SavingAgainReplacesPreviousSnapshotAndDeduplicatesNormalizedPaths()
    {
        using var fixture = new TemporaryProject();
        var path = Path.Combine(fixture.Root, "workspace.json");
        var store = new JsonWorkspaceStore(path);
        var entry = new SavedProject(Guid.NewGuid(), "App", fixture.Root, []);
        await store.SaveAsync([entry, entry with { Id = Guid.NewGuid(), Path = Path.Combine(fixture.Root, ".") }]);
        Assert.Single(await store.LoadAsync());
        await store.SaveAsync([]); Assert.Empty(await store.LoadAsync());
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    private static DashboardViewModel Dashboard(string path, Interaction interaction) => new(importInteraction: interaction,
        workspaceInteraction: interaction, git: new ProjectRemovalTests.FakeGit(), workspaceStore: new JsonWorkspaceStore(path));

    private sealed class FailingStore : IWorkspaceStore
    {
        public bool Fail;
        public IReadOnlyList<SavedProject> Projects = [];
        public Task<IReadOnlyList<SavedProject>> LoadAsync() => Task.FromResult(Projects);
        public Task SaveAsync(IReadOnlyList<SavedProject> projects)
        {
            if (Fail) throw new IOException("Disk unavailable.");
            Projects = projects.ToArray(); return Task.CompletedTask;
        }
    }
    private sealed class Interaction(string path) : IProjectImportInteraction, IProjectWorkspaceInteraction
    {
        public bool ConfirmRemove = true;
        public string Message = "";
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(path);
        public Task<bool> ConfirmImportAsync(ProjectDiscoveryResult result) => Task.FromResult(true);
        public Task ShowMessageAsync(string title, string message) { Message = message; return Task.CompletedTask; }
        public Task<bool> ConfirmRemovalAsync(string name, bool running) => Task.FromResult(ConfirmRemove);
        public Task<ProjectRunConfiguration?> ChooseRunConfigurationAsync(string name, IReadOnlyList<ProjectRunConfiguration> configurations) => Task.FromResult<ProjectRunConfiguration?>(null);
        public void OpenProject(ProjectWorkspaceViewModel workspace) { }
        public void CloseProject(Guid projectId) { }
    }
}

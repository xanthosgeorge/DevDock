using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class ProjectDiscoveryServiceTests
{
    private readonly ProjectDiscoveryService _service = new();

    [Fact]
    public async Task DetectsDotNetSolutionWebSdkFrameworkAndDatabase()
    {
        using var project = new TemporaryProject();
        project.Write("IronLog.sln");
        project.Write("IronLog.API/IronLog.API.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            <ItemGroup><PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" /></ItemGroup></Project>
            """);
        project.Write(".git/HEAD", "ref: refs/heads/main");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal("IronLog", result.Name);
        Assert.Equal("IronLog.sln", result.SolutionFile);
        Assert.Equal(ProjectPath.Normalize(project.Root), result.RootPath);
        Assert.True(result.IsSupported);
        Assert.True(result.HasGitRepository);
        Assert.Contains(".NET 10", result.Technologies);
        Assert.Contains("C#", result.Technologies);
        Assert.Contains("ASP.NET Core", result.Technologies);
        Assert.Contains("PostgreSQL", result.Technologies);
        Assert.DoesNotContain(".NET", result.Technologies);
        Assert.DoesNotContain(result.ProjectFiles, f => f.RelativePath.Contains("HEAD"));
    }

    [Fact]
    public async Task HandlesNestedSrcProjectsAndMultipleTargetFrameworks()
    {
        using var project = new TemporaryProject();
        project.Write("src/Library/Library.fsproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net8.0;net9.0;net10.0-macos</TargetFrameworks></PropertyGroup></Project>
            """);
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal("Library", result.Name);
        Assert.Contains("F#", result.Technologies);
        foreach (var version in new[] { ".NET 8", ".NET 9", ".NET 10" }) Assert.Contains(version, result.Technologies);
    }

    [Theory]
    [InlineData("net48", ".NET Framework 4.8")]
    [InlineData("net472", ".NET Framework 4.7.2")]
    [InlineData("netstandard2.0", ".NET Standard 2.0")]
    [InlineData("$(SharedTargetFramework)", ".NET")]
    public async Task DoesNotInventFrameworkVersions(string framework, string expected)
    {
        using var project = new TemporaryProject();
        project.Write("App.csproj", $"<Project><PropertyGroup><TargetFramework>{framework}</TargetFramework></PropertyGroup></Project>");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Contains(expected, result.Technologies);
    }

    [Fact]
    public async Task ReadsLegacyNamespacedProjectWithoutEvaluatingImports()
    {
        using var project = new TemporaryProject();
        project.Write("Legacy.csproj", """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="../../outside.props" />
              <ItemGroup><PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" /><PackageReference Include="Microsoft.Data.Sqlite" /><PackageReference Include="MySqlConnector" /></ItemGroup>
            </Project>
            """);
        var result = await _service.DiscoverAsync(project.Root);
        foreach (var database in new[] { "SQL Server", "SQLite", "MySQL" }) Assert.Contains(database, result.Technologies);
    }

    [Theory]
    [InlineData("@angular/core", "Angular")]
    [InlineData("react", "React")]
    [InlineData("vue", "Vue")]
    [InlineData("next", "Next.js")]
    public async Task DetectsFrontendDependenciesAndPackageName(string dependency, string expected)
    {
        using var project = new TemporaryProject();
        project.Write("frontend/package.json", """
            {"name":"frontend-app","dependencies":{"DEPENDENCY":"latest"},"devDependencies":{"typescript":"latest"}}
            """.Replace("DEPENDENCY", dependency));
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal("frontend-app", result.Name);
        Assert.Contains("Node.js", result.Technologies);
        Assert.Contains(expected, result.Technologies);
        Assert.Contains("TypeScript", result.Technologies);
    }

    [Fact]
    public async Task DoesNotInferDependenciesFromPackageDescriptions()
    {
        using var project = new TemporaryProject();
        project.Write("package.json", """{"name":"plain-node","description":"react vue next postgres mysql sqlite"}""");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal(["Node.js"], result.Technologies);
    }

    [Fact]
    public async Task DetectsAngularWorkspaceAndGitWorktreeFile()
    {
        using var project = new TemporaryProject();
        project.Write("frontend/package.json", "{}");
        project.Write("frontend/angular.json", "{}");
        project.Write(".git", "gitdir: /do/not/follow/this/path");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Contains("Angular", result.Technologies);
        Assert.True(result.HasGitRepository);
    }

    [Fact]
    public async Task DetectsMavenAndJavaFxWithoutRunningAnything()
    {
        using var project = new TemporaryProject();
        project.Write("pom.xml", """
            <project xmlns="http://maven.apache.org/POM/4.0.0"><dependencies><dependency>
            <groupId>org.openjfx</groupId><artifactId>javafx-controls</artifactId></dependency></dependencies></project>
            """);
        var result = await _service.DiscoverAsync(project.Root);
        foreach (var technology in new[] { "Java", "Maven", "JavaFX" }) Assert.Contains(technology, result.Technologies);
    }

    [Theory]
    [InlineData("build.gradle")]
    [InlineData("build.gradle.kts")]
    public async Task DetectsGradleAndJavaFx(string filename)
    {
        using var project = new TemporaryProject();
        project.Write(filename, "plugins { id(\"org.openjfx.javafxplugin\") }; javafx { modules(\"javafx.controls\") }");
        var result = await _service.DiscoverAsync(project.Root);
        foreach (var technology in new[] { "Java", "Gradle", "JavaFX" }) Assert.Contains(technology, result.Technologies);
    }

    [Theory]
    [InlineData("requirements.txt", "Flask>=3.0\npsycopg[binary]>=3", "Flask")]
    [InlineData("pyproject.toml", "[project]\ndependencies = [\"fastapi>=0.100\"]", "FastAPI")]
    [InlineData("Pipfile", "[packages]\ndjango = \"*\"", "Django")]
    [InlineData("setup.py", "raise RuntimeError('must never execute'); install_requires=['flask']", "Flask")]
    public async Task DetectsPythonAndFrameworkHints(string filename, string content, string expected)
    {
        using var project = new TemporaryProject();
        project.Write(filename, content);
        var result = await _service.DiscoverAsync(project.Root);
        Assert.True(result.IsSupported);
        Assert.Contains("Python", result.Technologies);
        Assert.Contains(expected, result.Technologies);
    }

    [Theory]
    [InlineData("Dockerfile")]
    [InlineData("docker-compose.yml")]
    [InlineData("docker-compose.yaml")]
    [InlineData("compose.yml")]
    [InlineData("compose.yaml")]
    public async Task RecognizesDockerConfiguration(string filename)
    {
        using var project = new TemporaryProject();
        project.Write(filename, "services:\n  db:\n    image: postgres:17");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.True(result.HasDocker);
        Assert.True(result.IsSupported);
        Assert.Contains("Docker", result.Technologies);
        if (filename != "Dockerfile") Assert.Contains("PostgreSQL", result.Technologies);
    }

    [Theory]
    [InlineData("go.mod", "Go")]
    [InlineData("Cargo.toml", "Rust")]
    public async Task DetectsOtherLanguages(string filename, string expected)
    {
        using var project = new TemporaryProject();
        project.Write(filename);
        Assert.Contains(expected, (await _service.DiscoverAsync(project.Root)).Technologies);
    }

    [Fact]
    public async Task ReadsDatabaseProvidersWithoutReturningConnectionSecrets()
    {
        using var project = new TemporaryProject();
        project.Write("App.csproj", "<Project />");
        project.Write("appsettings.json", """{"Database":{"Provider":"Npgsql"},"ConnectionStrings":{"Default":"Password=test-secret-do-not-export"}}""");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Contains("PostgreSQL", result.Technologies);
        Assert.DoesNotContain("test-secret", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task RejectsEmptyGitOnlyAndConfigurationOnlyFolders()
    {
        using var project = new TemporaryProject();
        Assert.False((await _service.DiscoverAsync(project.Root)).IsSupported);
        project.Write(".git/HEAD", "ref: refs/heads/main");
        project.Write("appsettings.json", "{}");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.False(result.IsSupported);
        Assert.True(result.HasGitRepository);
    }

    [Fact]
    public async Task IgnoresGeneratedDependencyAndToolDirectories()
    {
        using var project = new TemporaryProject();
        foreach (var directory in new[] { "bin", "obj", "node_modules", ".git", "dist", "build", "packages", ".vs", ".idea", ".venv", "target", "vendor" })
            project.Write(directory + "/package.json", "{}");
        Assert.False((await _service.DiscoverAsync(project.Root)).IsSupported);
    }

    [Fact]
    public async Task StopsAtDepthLimitAndExplainsPartialResults()
    {
        using var project = new TemporaryProject();
        project.Write("one/two/three/four/package.json", "{}");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.False(result.IsSupported);
        Assert.Contains(result.Warnings, warning => warning.Contains("depth"));
    }

    [Fact]
    public async Task ReportsMalformedManifestsAndStillRecognizesProjectMarkers()
    {
        using var project = new TemporaryProject();
        project.Write("package.json", "{invalid");
        project.Write("App.csproj", "<Project>");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.True(result.IsSupported);
        Assert.Contains("Node.js", result.Technologies);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public async Task RejectsXmlExternalEntities()
    {
        using var project = new TemporaryProject();
        project.Write("App.csproj", """<!DOCTYPE Project [<!ENTITY file SYSTEM "file:///does-not-exist">]><Project>&file;</Project>""");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Single(result.Warnings);
        Assert.DoesNotContain("ASP.NET Core", result.Technologies);
    }

    [Fact]
    public async Task SkipsOversizedManifestContents()
    {
        using var project = new TemporaryProject();
        project.Write("package.json", new string(' ', ProjectDiscoveryService.MaxManifestBytes + 1));
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Contains("Node.js", result.Technologies);
        Assert.Contains(result.Warnings, warning => warning.Contains("size limit"));
    }

    [Fact]
    public async Task UsesRootSolutionThenMainProjectThenPackageThenDirectoryName()
    {
        using var project = new TemporaryProject();
        project.Write("package.json", """{"name":"package-name"}""");
        Assert.Equal("package-name", (await _service.DiscoverAsync(project.Root)).Name);
        project.Write("Main.csproj", "<Project />");
        Assert.Equal("Main", (await _service.DiscoverAsync(project.Root)).Name);
        project.Write("Workspace.slnx", "<Solution />");
        Assert.Equal("Workspace", (await _service.DiscoverAsync(project.Root)).Name);
        project.Write("Other.sln");
        Assert.Equal("Main", (await _service.DiscoverAsync(project.Root)).Name);
    }

    [Fact]
    public async Task BoundsDirectoryAndEntryEnumeration()
    {
        using var project = new TemporaryProject();
        for (var index = 0; index < ProjectDiscoveryService.MaxDirectories + 2; index++)
            project.Write($"module-{index:D3}/package.json", "{}");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal(ProjectDiscoveryService.MaxDirectories - 1, result.ProjectFiles.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("Folder scan limit"));

        using var manyEntries = new TemporaryProject();
        for (var index = 0; index < ProjectDiscoveryService.MaxEntriesPerDirectory + 1; index++)
            manyEntries.Write($"entry-{index:D3}.txt");
        result = await _service.DiscoverAsync(manyEntries.Root);
        Assert.Contains(result.Warnings, warning => warning.Contains("folder is too large"));
    }

    [Fact]
    public async Task BoundsRecognizedFileCount()
    {
        using var project = new TemporaryProject();
        for (var index = 0; index < ProjectDiscoveryService.MaxProjectFiles + 1; index++)
            project.Write($"App-{index:D3}.csproj", "<Project />");
        var result = await _service.DiscoverAsync(project.Root);
        Assert.Equal(ProjectDiscoveryService.MaxProjectFiles, result.ProjectFiles.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("Project file limit"));
    }

    [Fact]
    public async Task BoundsTotalManifestContents()
    {
        using var project = new TemporaryProject();
        var content = "{\"description\":\"" + new string('x', ProjectDiscoveryService.MaxManifestBytes - 100) + "\"}";
        for (var index = 0; index < 9; index++) project.Write($"module-{index}/package.json", content);
        var result = await _service.DiscoverAsync(project.Root);
        Assert.True(result.IsSupported);
        Assert.Contains(result.Warnings, warning => warning.Contains("size limit"));
    }

    [Fact]
    public async Task MissingFolderIsReportedAsIoFailure()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _service.DiscoverAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}

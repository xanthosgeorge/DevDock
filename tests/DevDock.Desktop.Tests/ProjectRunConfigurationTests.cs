using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class ProjectRunConfigurationTests
{
    private readonly ProjectRunConfigurationService _service = new(new ProjectDiscoveryService());
    [Fact]
    public async Task OffersEveryRunnableTargetAndSkipsLibrariesAndTests()
    {
        using var project = new TemporaryProject();
        project.Write("Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        project.Write("Worker/Worker.csproj", "<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>");
        project.Write("Library/Library.csproj", "<Project />");
        project.Write("Tests/Tests.csproj", "<Project><PropertyGroup><OutputType>Exe</OutputType><IsTestProject>true</IsTestProject></PropertyGroup></Project>");
        var configurations = await _service.DetectAsync(Guid.NewGuid(), project.Root);
        Assert.Equal(2, configurations.Count);
        Assert.All(configurations, c => { Assert.Equal("dotnet", c.Executable); Assert.Contains("--project", c.Arguments); Assert.Equal(Path.GetDirectoryName(c.Arguments[2]), c.WorkingDirectory); });
    }

    [Theory]
    [InlineData("{\"scripts\":{\"dev\":\"vite --host\"}}", "dev")]
    [InlineData("{\"scripts\":{\"serve\":\"serve\",\"start\":\"ng serve\",\"dev\":\"vite\"}}", "start")]
    [InlineData("{\"scripts\":{\"serve\":\"my-tool\"}}", "serve")]
    public async Task UsesExistingScriptNamesInsteadOfExecutingDiscoveredText(string json, string expected)
    {
        using var project = new TemporaryProject(); project.Write("frontend/package.json", json);
        var configuration = Assert.Single(await _service.DetectAsync(Guid.NewGuid(), project.Root));
        Assert.Equal("npm", configuration.Executable); Assert.Equal(new[] { "run", expected }, configuration.Arguments);
    }

    [Fact]
    public async Task DoesNotInventScriptsOrRunUnknownJavaConfiguration()
    {
        using var project = new TemporaryProject();
        project.Write("package.json", "{\"scripts\":{\"test\":\"tests\"}}");
        project.Write("pom.xml", "<project />");
        project.Write("build.gradle", "// application plugin is deliberately absent");
        Assert.Empty(await _service.DetectAsync(Guid.NewGuid(), project.Root));
    }

    [Fact]
    public async Task MultiTargetFrameworksRequireAFrameworkSpecificChoice()
    {
        using var project = new TemporaryProject();
        project.Write("App.csproj", "<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>net9.0;net10.0</TargetFrameworks></PropertyGroup></Project>");
        var configurations = await _service.DetectAsync(Guid.NewGuid(), project.Root);
        Assert.Equal(2, configurations.Count);
        Assert.All(configurations, c => Assert.Contains("--framework", c.Arguments));
    }

    [Fact]
    public void LaunchArgumentsRemainSeparateAndWindowsNeverInvokesRepositoryWrappers()
    {
        var info = ProcessLaunch.Create("dotnet", ["run", "--project", "folder & name/App.csproj"], Path.GetTempPath());
        Assert.False(info.UseShellExecute);
        Assert.Equal("folder & name/App.csproj", info.ArgumentList[2]);
        if (OperatingSystem.IsWindows()) Assert.Throws<NotSupportedException>(() => ProcessLaunch.Create("mvn", ["javafx:run"], Path.GetTempPath()));
    }
}

using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class ProjectRunnerServiceTests
{
    [Fact]
    public async Task CapturesOutputStopsOnlyOwnedProcessesAndTracksNaturalExit()
    {
        using var project = new TemporaryProject();
        project.Write("Probe.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        project.Write("Program.cs", """
            Console.WriteLine("ready");
            Console.Error.WriteLine("diagnostic");
            Console.WriteLine(new string('x', 12000));
            if (args.Contains("exit")) return;
            await Task.Delay(Timeout.Infinite);
            """);
        var commands = new CommandRunner();
        var build = await commands.ExecuteAsync("dotnet", ["build", "Probe.csproj", "--disable-build-servers", "--verbosity", "quiet", "--nologo"], project.Root);
        Assert.True(build.ExitCode == 0, build.StdErr + build.StdOut);
        var dll = Path.Combine(project.Root, "bin", "Debug", "net10.0", "Probe.dll");
        var runner = new ProjectRunnerService();
        var otherRunner = new ProjectRunnerService();
        var configuration = new ProjectRunConfiguration(Guid.NewGuid(), "Probe", "dotnet", [dll], project.Root);
        var process = await runner.StartAsync(configuration);
        ProjectProcess? unrelated = null;
        try
        {
            await WaitUntil(() => process.GetOutput().Any(l => l.Text.Contains("line truncated")));
            Assert.True(process.IsRunning); Assert.True(process.Pid > 0);
            Assert.Equal(configuration.ProjectId, process.ProjectId);
            Assert.Contains(process.GetOutput(), l => l.Text == "ready" && !l.IsError);
            Assert.Contains(process.GetOutput(), l => l.Text == "diagnostic" && l.IsError);
            Assert.All(process.GetOutput(), l => Assert.True(l.Text.Length < 8300));
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(configuration));
            unrelated = await otherRunner.StartAsync(configuration with { ProjectId = Guid.NewGuid() });
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StopAsync(unrelated));
            Assert.True(unrelated.IsRunning);
            process.ClearOutput(); Assert.Empty(process.GetOutput());
            await runner.StopAsync(process); Assert.False(process.IsRunning);
            Assert.True(unrelated.IsRunning);
            var natural = await runner.StartAsync(configuration with { Arguments = [dll, "exit"] });
            await WaitUntil(() => !natural.IsRunning);
            Assert.Equal(0, natural.ExitCode);
        }
        finally { await runner.StopAllAsync(); await otherRunner.StopAllAsync(); }
        Assert.False(unrelated?.IsRunning ?? false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(configuration));
    }

    [Fact]
    public async Task StartFailureDoesNotCreateATrackedProcess()
    {
        var runner = new ProjectRunnerService();
        var configuration = new ProjectRunConfiguration(Guid.NewGuid(), "Missing", "devdock-tool-not-installed-49a1", [], Path.GetTempPath());
        await Assert.ThrowsAnyAsync<Exception>(() => runner.StartAsync(configuration));
        await runner.StopAllAsync();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}

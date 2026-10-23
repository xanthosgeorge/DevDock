using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class RuntimeEndpointTests
{
    [Theory]
    [InlineData("Now listening on: http://localhost:5001", "http://localhost:5001/")]
    [InlineData("info: Now listening on: https://localhost:7001", "https://localhost:7001/")]
    [InlineData("  ➜  Local: http://localhost:4200/", "http://localhost:4200/")]
    [InlineData("\u001b[32m  Local: http://localhost:5173/\u001b[0m", "http://localhost:5173/")]
    [InlineData("Tomcat started on port 8080 (http) with context path '/api'", "http://localhost:8080/api")]
    [InlineData("Tomcat started on port(s): 8443 (https)", "https://localhost:8443/")]
    public void KnownStartupMessagesProduceEndpoints(string output, string url)
        => Assert.Equal(url, RuntimeEndpointDetector.Parse(output)?.Url.AbsoluteUri);

    [Theory]
    [InlineData("Read documentation at http://localhost:5001")]
    [InlineData("Error contacting http://localhost:5001")]
    [InlineData("Local: file:///etc/passwd")]
    [InlineData("Now listening on: http://user:secret@localhost:5001")]
    [InlineData("Now listening on: http://localhost:90000")]
    [InlineData("Tomcat started on port 0 (http)")]
    public void ArbitraryOrInvalidUrlsAreIgnored(string output) => Assert.Null(RuntimeEndpointDetector.Parse(output));

    [Fact]
    public void RuntimeReplacesExpectedEndpointsAndSurvivesClearAndStop()
    {
        var process = new ProjectProcess(100, new(Guid.NewGuid(), "api", "dotnet", [], "/tmp"));
        process.SetExpectedEndpoints([new("Expected", new("http://localhost:5000"), false)]);
        Assert.False(Assert.Single(process.GetEndpoints()).IsRuntimeDetected);
        process.AddOutput("Now listening on: http://localhost:5001");
        process.AddOutput("Now listening on: https://localhost:7001");
        process.AddOutput("Now listening on: http://localhost:5001");
        process.SetExpectedEndpoints([new("Expected", new("http://localhost:5000"), false)]);
        Assert.Equal(2, process.GetEndpoints().Count);
        Assert.All(process.GetEndpoints(), e => Assert.True(e.IsRuntimeDetected));
        process.ClearOutput(); Assert.Equal(2, process.GetEndpoints().Count);
        process.Complete(0); Assert.Equal(2, process.GetEndpoints().Count); Assert.False(process.IsRunning);
        var next = new ProjectProcess(101, process.Configuration);
        Assert.Empty(next.GetEndpoints());
    }

    [Fact]
    public async Task ReadsOnlyDefaultDotnetProjectProfile()
    {
        using var project = new TemporaryProject();
        project.Write("Properties/launchSettings.json", """
            {"profiles":{"IIS Express":{"commandName":"IISExpress","applicationUrl":"http://localhost:1111"},
            "http":{"commandName":"Project","applicationUrl":"https://localhost:7001;http://localhost:5001;file:///bad"},
            "other":{"commandName":"Project","applicationUrl":"http://localhost:9999"}}}
            """);
        var config = new ProjectRunConfiguration(Guid.NewGuid(), "api", "dotnet", ["run", "--project", "api.csproj"], project.Root);
        var endpoints = await RuntimeEndpointDetector.ReadExpectedAsync(config);
        Assert.Equal(new[] { 7001, 5001 }, endpoints.Select(e => e.Url.Port));
        Assert.All(endpoints, e => Assert.False(e.IsRuntimeDetected));
        Assert.Empty(await RuntimeEndpointDetector.ReadExpectedAsync(config with { Executable = "npm" }));
        project.Write("Properties/launchSettings.json", "not-json");
        Assert.Empty(await RuntimeEndpointDetector.ReadExpectedAsync(config));
    }
}

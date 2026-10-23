using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class PortAssociationTests
{
    private static readonly DateTimeOffset Started = DateTimeOffset.Parse("2026-10-23T10:00:00Z");
    private static ProjectProcess Session() => new(10101, new(Guid.NewGuid(), "api", "dotnet", [], "/tmp"), Started);

    [Fact]
    public void AssociatesTrackedRootAndChildrenUsingIdentityAndNeverByExecutableName()
    {
        var session = Session();
        var root = new ProcessIdentity(10101, "dotnet", Started);
        var child = new ProcessIdentity(10102, "api", Started.AddSeconds(1), 10101);
        var unrelated = new ProcessIdentity(10103, "dotnet", Started);
        var processes = new[] { root, child, unrelated };
        var tracked = new[] { new TrackedPortProject("My API", session) };
        Assert.NotNull(PortAssociation.Associate(new(5001, "127.0.0.1", root), processes, tracked).Project);
        Assert.NotNull(PortAssociation.Associate(new(5001, "127.0.0.1", child), processes, tracked).Project);
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", unrelated), processes, tracked).Project);
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", root with { StartedAt = Started.AddMinutes(2) }), processes, tracked).Project);
        var recycledParent = root with { StartedAt = Started.AddMinutes(3) };
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", child), [recycledParent, child], tracked).Project);
        session.Complete(0);
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", root), processes, tracked).Project);
    }

    [Fact]
    public void EndpointRequiresConfirmedRuntimePortAddressAndOwnedPid()
    {
        var session = Session(); var identity = new ProcessIdentity(10101, "dotnet", Started);
        var tracked = new[] { new TrackedPortProject("API", session) };
        session.SetExpectedEndpoints([new("Expected", new("http://localhost:5001"), false)]);
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", identity), [identity], tracked).Endpoint);
        session.AddOutput("Now listening on: http://localhost:5001");
        Assert.NotNull(PortAssociation.Associate(new(5001, "0.0.0.0", identity), [identity], tracked).Endpoint);
        Assert.NotNull(PortAssociation.Associate(new(5001, "::1", identity), [identity], tracked).Endpoint);
        Assert.Null(PortAssociation.Associate(new(5432, "127.0.0.1", identity), [identity], tracked).Endpoint);
        Assert.Null(PortAssociation.Associate(new(5001, "192.0.2.4", identity), [identity], tracked).Endpoint);
        Assert.Null(PortAssociation.Associate(new(5001, "127.0.0.1", null), [identity], tracked).Endpoint);
    }

    [Fact]
    public void StopEligibilityProtectsSystemSelfAncestorsAndUnknownIdentity()
    {
        var parent = new ProcessIdentity(500, "shell", Started);
        var self = new ProcessIdentity(Environment.ProcessId, "DevDock", Started.AddSeconds(2), 500);
        Assert.False(PortAssociation.CanStop(null, []));
        Assert.False(PortAssociation.CanStop(new(4, "System", Started), []));
        Assert.False(PortAssociation.CanStop(new(120, "lsass", Started), []));
        Assert.False(PortAssociation.CanStop(new(501, "node", null), []));
        Assert.False(PortAssociation.CanStop(self, [parent, self]));
        Assert.False(PortAssociation.CanStop(parent, [parent, self]));
        Assert.True(PortAssociation.CanStop(new(502, "node", Started), [parent, self]));
    }

    [Fact]
    public void MacParserHandlesWildcardIpv6AndUnknownProcess()
    {
        var rows = MacOsPortService.Parse("p101\ncnode\nn*:4200\nn[::1]:5173\np102\ncpostgres\nn127.0.0.1:5432\n", []);
        Assert.Equal(new[] { 4200, 5173, 5432 }, rows.Select(p => p.Port));
        Assert.Equal("*", rows[0].LocalAddress); Assert.Equal("::1", rows[1].LocalAddress);
        Assert.Equal("postgres", rows[2].ProcessName); Assert.Equal(102, rows[2].ProcessId);
        Assert.Null(rows[2].Process?.StartedAt);
    }
}

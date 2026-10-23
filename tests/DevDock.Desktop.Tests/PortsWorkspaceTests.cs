using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class PortsWorkspaceTests
{
    [Fact]
    public async Task DefaultSortSearchFiltersAndRefreshUseRealRowsAndKeepFilterState()
    {
        var time = DateTimeOffset.UtcNow;
        var session = new ProjectProcess(19001, new(Guid.NewGuid(), "api", "dotnet", [], "/tmp"), time);
        session.AddOutput("Now listening on: http://localhost:5001");
        var owner = new ProcessIdentity(19001, "dotnet", time);
        var other = new ProcessIdentity(19002, "postgres", time);
        var service = new FakePorts(new([new(5432, "0.0.0.0", other), new(5001, "127.0.0.1", owner)], [owner, other]));
        var platform = new Platform();
        var vm = new PortsWorkspaceViewModel(service, () => [new("My API", session)], new Stopper(), platform: platform);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new[] { 5001, 5432 }, vm.Rows.Select(p => p.Port));
        Assert.Equal(2, vm.Preview.Count);
        foreach (var term in new[] { "5001", "DOTNET", "19001", "My API", "127.0.0.1", "http://localhost" })
        { vm.Search = term; Assert.Equal(5001, Assert.Single(vm.Rows).Port); }
        var http = vm.Rows[0];
        await http.CopyCommand.ExecuteAsync(null); await http.OpenCommand.ExecuteAsync(null);
        Assert.Equal(http.Endpoint, platform.Copied); Assert.Equal(http.Endpoint, platform.Opened);
        vm.Search = ""; vm.SelectedFilter = "Other Processes";
        Assert.Equal(5432, Assert.Single(vm.Rows).Port); Assert.False(vm.Rows[0].HasEndpoint);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Other Processes", vm.SelectedFilter); Assert.Equal(5432, Assert.Single(vm.Rows).Port);
        vm.SelectedFilter = "DevDock Projects"; Assert.Equal(5001, Assert.Single(vm.Rows).Port);
        session.Complete(0); vm.Reassociate(); Assert.Empty(vm.Rows);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task StopAlwaysRequiresExplicitConfirmation(bool confirmed)
    {
        var identity = new ProcessIdentity(19002, "node", DateTimeOffset.UtcNow);
        var ports = new FakePorts(new([new(4200, "127.0.0.1", identity)], [identity]));
        var control = new Stopper(); var interaction = new Interaction(confirmed);
        var vm = new PortsWorkspaceViewModel(ports, () => [], control, interaction);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Rows[0].StopCommand.ExecuteAsync(null);
        Assert.Equal(1, interaction.Calls); Assert.Equal(confirmed ? 1 : 0, control.Calls);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CancellationPreventsOverlappingScansAndLeavesUiUsable()
    {
        var service = new BlockingPorts();
        var vm = new PortsWorkspaceViewModel(service, () => [], new Stopper());
        var refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy); Assert.False(vm.RefreshCommand.CanExecute(null));
        vm.CancelRefresh(); await refresh;
        Assert.False(vm.IsBusy); Assert.False(vm.HasError); Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task StaleProcessIdentityIsRejectedBeforeAnyTermination()
    {
        var identity = new ProcessIdentity(19002, "node", DateTimeOffset.UtcNow);
        var ports = new FakePorts(new([new(4200, "127.0.0.1", identity with { StartedAt = DateTimeOffset.UtcNow.AddMinutes(1) })], []));
        var control = new PortProcessControl(ports, new ProjectRunnerService());
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.StopAsync(new(new(4200, "127.0.0.1", identity), null, null, true)));
    }

    private sealed class FakePorts(PortScanResult result) : IPortService
    {
        public Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
    private sealed class BlockingPorts : IPortService
    {
        public int Calls;
        public async Task<PortScanResult> GetListeningPortsAsync(CancellationToken cancellationToken = default)
        { Calls++; await Task.Delay(Timeout.Infinite, cancellationToken); return new([], []); }
    }
    private sealed class Stopper : IPortProcessControl
    {
        public int Calls;
        public Task StopAsync(AssociatedPort port, CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; }
    }
    private sealed class Interaction(bool confirm) : IPortInteraction
    {
        public int Calls;
        public Task<bool> ConfirmStopAsync(PortInfo port, bool owned) { Calls++; return Task.FromResult(confirm); }
    }
    private sealed class Platform : IPlatformService
    {
        public string? Copied, Opened;
        public Task CopyToClipboardAsync(string text) { Copied = text; return Task.CompletedTask; }
        public Task OpenUrlAsync(string url) { Opened = url; return Task.CompletedTask; }
    }
}

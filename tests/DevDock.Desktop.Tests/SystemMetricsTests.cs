using DevDock.Desktop.Models;
using DevDock.Desktop.Services;
using DevDock.Desktop.ViewModels;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class SystemMetricsTests
{
    [Fact]
    public void CpuUsesIntervalDeltasAndCountsKernelIdleOnlyOnce()
    {
        var sampler = new CpuUsageSampler();
        Assert.Null(sampler.Sample(new(600, 1000, 400)));
        // Kernel +100 (including idle +40), user +100 => 80% busy.
        Assert.Equal(80d, sampler.Sample(new(640, 1100, 500)));
        Assert.Null(sampler.Sample(new(640, 1100, 500))); // No elapsed CPU time.
        Assert.Equal(0d, sampler.Sample(new(740, 1200, 500)));
        Assert.Equal(100d, sampler.Sample(new(740, 1300, 600)));
    }

    [Fact]
    public void CpuCounterResetsAndInvalidDeltasDoNotInventPercentages()
    {
        var sampler = new CpuUsageSampler();
        Assert.Null(sampler.Sample(new(100, 200, 100)));
        Assert.Null(sampler.Sample(new(10, 20, 10))); // Counter reset.
        Assert.Null(sampler.Sample(new(100, 30, 20))); // Idle exceeds elapsed total.
        sampler.Reset();
        Assert.Null(sampler.Sample(new(100, 200, 100)));
    }

    [Fact]
    public void ViewModelDisplaysPhysicalCapacityAndClearsUnavailableValues()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        var vm = new SystemMetricsViewModel();
        vm.Apply(new(25, new(8 * gib, 32 * gib), new(400 * gib, 1000 * gib), DateTimeOffset.Now));
        Assert.Equal("25%", vm.CpuText); Assert.Contains("32", vm.MemoryText); Assert.Contains("GiB", vm.MemoryText);
        Assert.Equal("400 / 1000 GiB", vm.DiskText);
        Assert.Equal(25, vm.MemoryPercent); Assert.Equal(40, vm.DiskPercent);
        Assert.Contains("physical RAM", vm.MemoryToolTip); Assert.Contains("C:", vm.DiskToolTip);
        vm.Apply(new(null, null, new(400 * gib, 1000 * gib), DateTimeOffset.Now, "RAM unavailable"));
        Assert.Equal("—", vm.CpuText); Assert.Equal("—", vm.MemoryText);
        Assert.False(vm.HasCpu); Assert.False(vm.HasMemory); Assert.True(vm.HasDisk);
        Assert.Equal("RAM unavailable", vm.Status);
        vm.Apply(new(double.NaN, new(5, 0), new(100, 10), DateTimeOffset.Now));
        Assert.False(vm.HasCpu); Assert.False(vm.HasMemory); Assert.False(vm.HasDisk);
        Assert.Equal("—", vm.DiskText);
    }

    [Fact]
    public async Task RepeatedStartDoesNotOverlapScansAndStopCancelsPendingRead()
    {
        var service = new BlockingService();
        var vm = new SystemMetricsViewModel(service, TimeSpan.FromMilliseconds(10));
        vm.Start(); vm.Start(); vm.Start();
        await service.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, service.Calls);
        await vm.StopAsync(); await vm.StopAsync();
        Assert.True(service.Cancelled); Assert.Equal(1, service.Calls);
        vm.Start();
        Assert.Equal(2, service.Calls);
        await vm.StopAsync();
    }

    [Fact]
    public async Task PollingRecoversAfterFailureAndStopsCleanly()
    {
        var service = new RecoveringService();
        var vm = new SystemMetricsViewModel(service, TimeSpan.FromMilliseconds(10));
        vm.Start();
        try
        {
            Assert.Contains("unavailable", vm.Status);
            await service.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!vm.HasCpu) await Task.Delay(10, timeout.Token);
            Assert.Equal("42%", vm.CpuText);
        }
        finally { await vm.StopAsync(); }
        var calls = service.Calls;
        await Task.Delay(40); Assert.Equal(calls, service.Calls);
    }

    private sealed class BlockingService : ISystemMetricsService
    {
        public int Calls; public bool Cancelled;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<SystemMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            Calls++; Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            return new(null, null, null, DateTimeOffset.Now);
        }
    }
    private sealed class RecoveringService : ISystemMetricsService
    {
        public int Calls;
        public TaskCompletionSource Recovered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SystemMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (++Calls == 1) throw new IOException("Temporary read failure");
            Recovered.TrySetResult();
            return Task.FromResult(new SystemMetricsSnapshot(42, new(8, 32), new(10, 100), DateTimeOffset.Now));
        }
    }
}

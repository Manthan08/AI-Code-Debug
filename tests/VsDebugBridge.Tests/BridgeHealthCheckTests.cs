using System.Diagnostics;
using VsDebugBridge.Contracts;
using VsDebugBridge.Ipc;

namespace VsDebugBridge.Tests;

public sealed class BridgeHealthCheckTests
{
    [Fact]
    public async Task HealthCheckReportsMissingDiscoveryDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "VsDebugBridge.Tests", Guid.NewGuid().ToString("N"));
        var client = new NamedPipeBridgeClient(new DiscoveryStore(root, TimeSpan.FromMinutes(10)), connectTimeoutMs: 100);

        var health = await client.GetHealthCheckAsync(new BridgeHealthCheckRequest(), CancellationToken.None);

        Assert.Equal(BridgeHealthStatus.Unavailable, health.Status);
        Assert.Empty(health.Instances);
        Assert.Contains("No Visual Studio bridge instance registrations", health.Summary);
    }

    [Fact]
    public async Task HealthCheckMarksStaleRegistrationUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "VsDebugBridge.Tests", Guid.NewGuid().ToString("N"));
        var store = new DiscoveryStore(root, TimeSpan.FromSeconds(1));
        Directory.CreateDirectory(store.InstancesDirectory);

        var process = Process.GetCurrentProcess();
        File.WriteAllText(
            Path.Combine(store.InstancesDirectory, "stale.json"),
            BridgeJson.Serialize(new VisualStudioInstanceInfo
            {
                InstanceId = "stale",
                ProcessId = process.Id,
                PipeName = BridgeConstants.CreatePipeName(process.Id),
                LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
            }));

        var client = new NamedPipeBridgeClient(store, connectTimeoutMs: 100);

        var health = await client.GetHealthCheckAsync(new BridgeHealthCheckRequest(), CancellationToken.None);

        Assert.Equal(BridgeHealthStatus.Unavailable, health.Status);
        Assert.Single(health.Instances);
        Assert.True(health.Instances[0].IsStale);
        Assert.Contains("stale", health.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheckReportsReachablePipeHealthy()
    {
        var root = Path.Combine(Path.GetTempPath(), "VsDebugBridge.Tests", Guid.NewGuid().ToString("N"));
        var store = new DiscoveryStore(root, TimeSpan.FromMinutes(10));
        var pipeName = "VsDebugBridge.Tests." + Guid.NewGuid().ToString("N");
        var process = Process.GetCurrentProcess();

        using var server = new NamedPipeBridgeServer(pipeName, request => BridgeProtocol.Success(request.RequestId, "ok"));
        server.Start();

        store.WriteInstance(new VisualStudioInstanceInfo
        {
            InstanceId = "current",
            ProcessId = process.Id,
            PipeName = pipeName,
            SolutionName = "Sample"
        });

        var client = new NamedPipeBridgeClient(store, connectTimeoutMs: 1000);

        var health = await client.GetHealthCheckAsync(new BridgeHealthCheckRequest(), CancellationToken.None);

        Assert.Equal(BridgeHealthStatus.Healthy, health.Status);
        Assert.Single(health.Instances);
        Assert.True(health.Instances[0].IsSelected);
        Assert.True(health.Instances[0].ProcessAlive);
        Assert.True(health.Instances[0].PipeReachable);
        Assert.Equal("ok", health.Instances[0].PingResponse);
    }
}

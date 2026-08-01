using System.Diagnostics;
using VsDebugBridge.Contracts;
using VsDebugBridge.Ipc;

namespace VsDebugBridge.Tests;

public sealed class DiscoveryStoreTests
{
    [Fact]
    public void ListInstancesReturnsLiveInstancesNewestFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "VsDebugBridge.Tests", Guid.NewGuid().ToString("N"));
        var store = new DiscoveryStore(root, TimeSpan.FromMinutes(10));
        var process = Process.GetCurrentProcess();

        store.WriteInstance(new VisualStudioInstanceInfo
        {
            InstanceId = "current",
            ProcessId = process.Id,
            PipeName = BridgeConstants.CreatePipeName(process.Id),
            SolutionName = "Sample"
        });

        var instances = store.ListInstances();

        Assert.Single(instances);
        Assert.Equal("current", instances[0].InstanceId);
        Assert.Equal("Sample", instances[0].SolutionName);
    }
}

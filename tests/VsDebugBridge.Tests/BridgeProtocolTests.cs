using VsDebugBridge.Contracts;
using VsDebugBridge.Ipc;

namespace VsDebugBridge.Tests;

public sealed class BridgeProtocolTests
{
    [Fact]
    public void RequestPayloadRoundTrips()
    {
        var request = BridgeProtocol.CreateRequest(BridgeCommands.GetSnapshot, new DebugSnapshotRequest
        {
            InstanceId = "1234",
            MaxLocals = 10,
            SourceContextLinesBefore = 7,
            SourceContextLinesAfter = 8,
            MaxOutputLines = 40
        });

        var payload = BridgeProtocol.ReadRequestPayload<DebugSnapshotRequest>(request);

        Assert.Equal("1234", payload.InstanceId);
        Assert.Equal(10, payload.MaxLocals);
        Assert.Equal(7, payload.SourceContextLinesBefore);
        Assert.Equal(8, payload.SourceContextLinesAfter);
        Assert.Equal(40, payload.MaxOutputLines);
    }

    [Fact]
    public void FailedResponseThrowsUsefulMessage()
    {
        var response = BridgeProtocol.Failure("request-1", "no_instance", "No Visual Studio instance found.");

        var ex = Assert.Throws<InvalidOperationException>(() => BridgeProtocol.ReadResponsePayload<DebugSnapshot>(response));

        Assert.Contains("No Visual Studio instance found", ex.Message);
    }

    [Fact]
    public void DebugActionRequestPayloadRoundTrips()
    {
        var request = BridgeProtocol.CreateRequest(BridgeCommands.ExecuteDebugAction, new DebugActionRequest
        {
            InstanceId = "1234",
            Action = DebugActionKinds.StepOver,
            Approved = true,
            SnapshotRequest = new DebugSnapshotRequest
            {
                MaxStackFrames = 5
            }
        });

        var payload = BridgeProtocol.ReadRequestPayload<DebugActionRequest>(request);

        Assert.Equal("1234", payload.InstanceId);
        Assert.Equal(DebugActionKinds.StepOver, payload.Action);
        Assert.True(payload.Approved);
        Assert.Equal(5, payload.SnapshotRequest.MaxStackFrames);
    }
}

using System;

namespace VsDebugBridge.Contracts
{
    public sealed class BridgeRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        public string Command { get; set; } = string.Empty;

        public string? PayloadJson { get; set; }
    }
}

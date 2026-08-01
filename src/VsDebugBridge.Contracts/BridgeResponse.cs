namespace VsDebugBridge.Contracts
{
    public sealed class BridgeResponse
    {
        public string RequestId { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string? PayloadJson { get; set; }

        public BridgeError? Error { get; set; }
    }

    public sealed class BridgeError
    {
        public string Code { get; set; } = "bridge_error";

        public string Message { get; set; } = string.Empty;
    }
}

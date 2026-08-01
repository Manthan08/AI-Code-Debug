namespace VsDebugBridge.Contracts
{
    public static class DebugActionKinds
    {
        public const string StepOver = "stepOver";
        public const string StepInto = "stepInto";
        public const string StepOut = "stepOut";
        public const string Continue = "continue";
        public const string Pause = "pause";
        public const string SetBreakpoint = "setBreakpoint";
        public const string RemoveBreakpoint = "removeBreakpoint";
    }

    public sealed class DebugActionRequest
    {
        public string? InstanceId { get; set; }

        public string Action { get; set; } = string.Empty;

        public bool Approved { get; set; }

        public string? FilePath { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }

        public DebugSnapshotRequest SnapshotRequest { get; set; } = new DebugSnapshotRequest();
    }

    public sealed class DebugActionResult
    {
        public string Action { get; set; } = string.Empty;

        public bool Executed { get; set; }

        public string Message { get; set; } = string.Empty;

        public DebugSnapshot? Snapshot { get; set; }
    }
}

using System;

namespace VsDebugBridge.Contracts
{
    public sealed class VisualStudioInstanceInfo
    {
        public string InstanceId { get; set; } = string.Empty;

        public int ProcessId { get; set; }

        public string PipeName { get; set; } = string.Empty;

        public string? Version { get; set; }

        public string? SolutionName { get; set; }

        public string? SolutionFullName { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}

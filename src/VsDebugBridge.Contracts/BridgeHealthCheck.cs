using System;
using System.Collections.Generic;

namespace VsDebugBridge.Contracts
{
    public static class BridgeHealthStatus
    {
        public const string Healthy = "healthy";
        public const string Degraded = "degraded";
        public const string Unavailable = "unavailable";
        public const string NotChecked = "not_checked";
    }

    public sealed class BridgeHealthCheckRequest
    {
        public string? InstanceId { get; set; }
    }

    public sealed class VisualStudioBridgeHealthCheck
    {
        public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public string Status { get; set; } = BridgeHealthStatus.Unavailable;

        public string Summary { get; set; } = string.Empty;

        public string? McpServerVersion { get; set; }

        public string? IpcVersion { get; set; }

        public string DiscoveryDirectory { get; set; } = string.Empty;

        public string? SelectedInstanceId { get; set; }

        public List<VisualStudioBridgeInstanceHealth> Instances { get; set; } = new List<VisualStudioBridgeInstanceHealth>();

        public List<string> RecommendedActions { get; set; } = new List<string>();
    }

    public sealed class VisualStudioBridgeInstanceHealth
    {
        public VisualStudioInstanceInfo? Instance { get; set; }

        public string? InstanceFilePath { get; set; }

        public bool IsSelected { get; set; }

        public bool ProcessAlive { get; set; }

        public bool IsStale { get; set; }

        public double? LastSeenAgeSeconds { get; set; }

        public bool? PipeReachable { get; set; }

        public string? PingResponse { get; set; }

        public string Status { get; set; } = BridgeHealthStatus.NotChecked;

        public string? Error { get; set; }
    }
}

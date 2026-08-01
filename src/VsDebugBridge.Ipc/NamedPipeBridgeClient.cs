using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.Ipc
{
    public sealed class NamedPipeBridgeClient
    {
        private readonly DiscoveryStore _discoveryStore;
        private readonly int _connectTimeoutMs;

        public NamedPipeBridgeClient(DiscoveryStore discoveryStore)
            : this(discoveryStore, 3000)
        {
        }

        public NamedPipeBridgeClient(DiscoveryStore discoveryStore, int connectTimeoutMs)
        {
            _discoveryStore = discoveryStore;
            _connectTimeoutMs = connectTimeoutMs;
        }

        public Task<IReadOnlyList<VisualStudioInstanceInfo>> ListInstancesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_discoveryStore.ListInstances());
        }

        public Task<DebugSnapshot> GetSnapshotAsync(DebugSnapshotRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var instance = _discoveryStore.ResolveInstance(request.InstanceId);
                var bridgeRequest = BridgeProtocol.CreateRequest(BridgeCommands.GetSnapshot, request);
                var response = Send(instance.PipeName, bridgeRequest);
                return BridgeProtocol.ReadResponsePayload<DebugSnapshot>(response);
            }, cancellationToken);
        }

        public Task<string> PingAsync(string? instanceId, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var instance = _discoveryStore.ResolveInstance(instanceId);
                var request = BridgeProtocol.CreateRequest(BridgeCommands.Ping);
                var response = Send(instance.PipeName, request);
                return BridgeProtocol.ReadResponsePayload<string>(response);
            }, cancellationToken);
        }

        public Task<VisualStudioBridgeHealthCheck> GetHealthCheckAsync(BridgeHealthCheckRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreateHealthCheck(request.InstanceId);
            }, cancellationToken);
        }

        public Task<DebugActionResult> ExecuteDebugActionAsync(DebugActionRequest request, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var instance = _discoveryStore.ResolveInstance(request.InstanceId);
                request.SnapshotRequest.InstanceId = null;
                var bridgeRequest = BridgeProtocol.CreateRequest(BridgeCommands.ExecuteDebugAction, request);
                var response = Send(instance.PipeName, bridgeRequest);
                return BridgeProtocol.ReadResponsePayload<DebugActionResult>(response);
            }, cancellationToken);
        }

        private VisualStudioBridgeHealthCheck CreateHealthCheck(string? instanceId)
        {
            var health = new VisualStudioBridgeHealthCheck
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                DiscoveryDirectory = _discoveryStore.InstancesDirectory,
                IpcVersion = typeof(NamedPipeBridgeClient).Assembly.GetName().Version?.ToString()
            };

            health.Instances.AddRange(_discoveryStore.InspectInstances());

            if (health.Instances.Count == 0)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Summary = "No Visual Studio bridge instance registrations were found.";
                health.RecommendedActions.Add("Start Visual Studio with the VsDebugBridge VSIX installed and open a solution.");
                health.RecommendedActions.Add("If Visual Studio is already open, check that the extension loaded successfully in ActivityLog.xml.");
                return health;
            }

            var selected = SelectInstance(health.Instances, instanceId);
            if (selected == null)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Summary = "Requested Visual Studio bridge instance was not found.";
                health.RecommendedActions.Add("Run VisualStudioListInstances and pass one of the returned instance ids, process ids, or solution names.");
                return health;
            }

            selected.IsSelected = true;
            health.SelectedInstanceId = selected.Instance?.InstanceId;

            if (selected.Instance == null)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Summary = "Selected Visual Studio bridge registration is invalid.";
                health.RecommendedActions.Add("Delete invalid registration files from the discovery directory and restart Visual Studio.");
                return health;
            }

            if (selected.IsStale || !selected.ProcessAlive)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Summary = selected.Error ?? "Selected Visual Studio bridge instance is not available.";
                health.RecommendedActions.Add("Close stale Visual Studio processes and restart Visual Studio with the bridge extension installed.");
                return health;
            }

            TryPing(selected);

            if (selected.PipeReachable == true)
            {
                selected.Status = BridgeHealthStatus.Healthy;
                health.Status = BridgeHealthStatus.Healthy;
                health.Summary = "Visual Studio bridge is registered, process is alive, and the named pipe responded.";
                return health;
            }

            selected.Status = BridgeHealthStatus.Degraded;
            health.Status = BridgeHealthStatus.Degraded;
            health.Summary = "Visual Studio bridge registration exists, but the named pipe did not respond.";
            health.RecommendedActions.Add("Restart Visual Studio and check ActivityLog.xml for VsDebugBridgePackage load errors.");
            health.RecommendedActions.Add("If the VSIX was just updated, uninstall the old extension, restart Visual Studio, then install the latest VSIX.");
            return health;
        }

        private static VisualStudioBridgeInstanceHealth? SelectInstance(IReadOnlyList<VisualStudioBridgeInstanceHealth> instances, string? instanceId)
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                return instances.FirstOrDefault(instance =>
                    instance.Instance != null && DiscoveryStore.MatchesInstance(instance.Instance, instanceId!));
            }

            return instances.FirstOrDefault(instance => instance.Instance != null && !instance.IsStale && instance.ProcessAlive) ??
                instances.FirstOrDefault(instance => instance.Instance != null);
        }

        private void TryPing(VisualStudioBridgeInstanceHealth health)
        {
            try
            {
                var request = BridgeProtocol.CreateRequest(BridgeCommands.Ping);
                var response = Send(health.Instance!.PipeName, request);
                health.PingResponse = BridgeProtocol.ReadResponsePayload<string>(response);
                health.PipeReachable = true;
            }
            catch (Exception ex)
            {
                health.PipeReachable = false;
                health.Error = ex.Message;
            }
        }

        private BridgeResponse Send(string pipeName, BridgeRequest request)
        {
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                pipe.Connect(_connectTimeoutMs);

                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true))
                using (var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(BridgeJson.Serialize(request));

                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        throw new InvalidOperationException("Visual Studio bridge returned an empty pipe response.");
                    }

                    return BridgeJson.Deserialize<BridgeResponse>(line!) ??
                        throw new InvalidOperationException("Visual Studio bridge returned invalid JSON.");
                }
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using VsDebugBridge.Contracts;
using VsDebugBridge.Ipc;

namespace VsDebugBridge.VisualStudioExtension
{
    internal sealed class VisualStudioBridgeService : IDisposable
    {
        private readonly DTE2 _dte;
        private readonly DebugSnapshotProvider _snapshotProvider;
        private readonly DebugActionExecutor _debugActionExecutor;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly DiscoveryStore _discoveryStore = new DiscoveryStore();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly VisualStudioInstanceInfo _instance;
        private readonly NamedPipeBridgeServer _server;
        private Task? _heartbeatTask;

        public VisualStudioBridgeService(DTE2 dte, DebugSnapshotProvider snapshotProvider, JoinableTaskFactory joinableTaskFactory)
        {
            _dte = dte;
            _snapshotProvider = snapshotProvider;
            _debugActionExecutor = new DebugActionExecutor(dte, snapshotProvider);
            _joinableTaskFactory = joinableTaskFactory;

            var process = Process.GetCurrentProcess();
            _instance = new VisualStudioInstanceInfo
            {
                InstanceId = process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProcessId = process.Id,
                PipeName = BridgeConstants.CreatePipeName(process.Id),
                StartedAtUtc = DateTimeOffset.UtcNow
            };

            _server = new NamedPipeBridgeServer(_instance.PipeName, HandleRequest);
        }

        public void Start()
        {
            RefreshInstance();
            _discoveryStore.WriteInstance(_instance);
            _server.Start();
            _heartbeatTask = Task.Run(() => Heartbeat(_cancellationTokenSource.Token));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _server.Dispose();
            _discoveryStore.DeleteInstance(_instance.InstanceId);
            _cancellationTokenSource.Dispose();
        }

        private BridgeResponse HandleRequest(BridgeRequest request)
        {
            if (string.Equals(request.Command, BridgeCommands.Ping, StringComparison.OrdinalIgnoreCase))
            {
                return BridgeProtocol.Success(request.RequestId, "ok");
            }

            if (string.Equals(request.Command, BridgeCommands.GetSnapshot, StringComparison.OrdinalIgnoreCase))
            {
                var snapshotRequest = BridgeProtocol.ReadRequestPayload<DebugSnapshotRequest>(request);
                var snapshot = _joinableTaskFactory.Run(async () =>
                {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    RefreshInstance();
                    return _snapshotProvider.Capture(_instance, snapshotRequest);
                });

                _discoveryStore.WriteInstance(_instance);
                return BridgeProtocol.Success(request.RequestId, snapshot);
            }

            if (string.Equals(request.Command, BridgeCommands.ExecuteDebugAction, StringComparison.OrdinalIgnoreCase))
            {
                var actionRequest = BridgeProtocol.ReadRequestPayload<DebugActionRequest>(request);
                var result = _joinableTaskFactory.Run(async () =>
                {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    RefreshInstance();
                    var actionResult = _debugActionExecutor.Execute(_instance, actionRequest);
                    _discoveryStore.WriteInstance(_instance);
                    return actionResult;
                });

                return BridgeProtocol.Success(request.RequestId, result);
            }

            return BridgeProtocol.Failure(request.RequestId, "unknown_command", "Unknown bridge command: " + request.Command);
        }

        private async Task Heartbeat(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                    _joinableTaskFactory.Run(async () =>
                    {
                        await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                        RefreshInstance();
                    });

                    _discoveryStore.WriteInstance(_instance);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                }
            }
        }

        private void RefreshInstance()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _instance.Version = _dte.Version;
            var solutionFullName = _dte.Solution?.FullName;
            _instance.SolutionFullName = solutionFullName;
            _instance.SolutionName = string.IsNullOrWhiteSpace(solutionFullName)
                ? null
                : System.IO.Path.GetFileNameWithoutExtension(solutionFullName);
            _instance.LastSeenUtc = DateTimeOffset.UtcNow;
        }
    }
}

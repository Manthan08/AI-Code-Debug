using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using VsDebugBridge.Contracts;
using VsDebugBridge.Ipc;

namespace VsDebugBridge.McpServer.Tools
{
    internal sealed class VisualStudioDebugTools
    {
        private readonly NamedPipeBridgeClient _client;

        public VisualStudioDebugTools(NamedPipeBridgeClient client)
        {
            _client = client;
        }

        [McpServerTool]
        [Description("Lists Visual Studio instances currently advertising the read-only debug bridge.")]
        public Task<IReadOnlyList<VisualStudioInstanceInfo>> VisualStudioListInstances(CancellationToken cancellationToken = default)
        {
            return _client.ListInstancesAsync(cancellationToken);
        }

        [McpServerTool]
        [Description("Returns a read-only snapshot of the current Visual Studio debugger state, including source context, thread/frame, call stack, locals, exception details, build errors, and recent Output window lines when available.")]
        public Task<DebugSnapshot> VisualStudioDebugSnapshot(
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            [Description("Maximum call stack frames to return.")] int maxStackFrames = 30,
            [Description("Maximum local variables to return from the current frame.")] int maxLocals = 200,
            [Description("Maximum build errors to return from Error List.")] int maxBuildErrors = 200,
            [Description("Maximum characters per variable or exception value.")] int maxValueLength = 500,
            [Description("Source lines to include before the current line.")] int sourceContextLinesBefore = 20,
            [Description("Source lines to include after the current line.")] int sourceContextLinesAfter = 20,
            [Description("Maximum Output window panes to return.")] int maxOutputPanes = 5,
            [Description("Maximum lines to return from each Output window pane.")] int maxOutputLines = 200,
            CancellationToken cancellationToken = default)
        {
            var request = new DebugSnapshotRequest
            {
                InstanceId = instanceId,
                MaxStackFrames = maxStackFrames,
                MaxLocals = maxLocals,
                MaxBuildErrors = maxBuildErrors,
                MaxValueLength = maxValueLength,
                SourceContextLinesBefore = sourceContextLinesBefore,
                SourceContextLinesAfter = sourceContextLinesAfter,
                MaxOutputPanes = maxOutputPanes,
                MaxOutputLines = maxOutputLines
            };

            return _client.GetSnapshotAsync(request, cancellationToken);
        }

        [McpServerTool]
        [Description("Checks that the MCP server can reach a registered Visual Studio bridge instance.")]
        public Task<string> VisualStudioBridgePing(
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return _client.PingAsync(instanceId, cancellationToken);
        }

        [McpServerTool]
        [Description("Returns a structured health check for MCP discovery, Visual Studio bridge registration, process liveness, and named-pipe reachability.")]
        public async Task<VisualStudioBridgeHealthCheck> VisualStudioBridgeHealthCheck(
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            var request = new BridgeHealthCheckRequest
            {
                InstanceId = instanceId
            };

            var health = await _client.GetHealthCheckAsync(request, cancellationToken).ConfigureAwait(false);
            health.McpServerVersion = typeof(VisualStudioDebugTools).Assembly.GetName().Version?.ToString();
            return health;
        }

        [McpServerTool]
        [Description("Steps over the current statement in Visual Studio, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioStepOver(
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.StepOver, approved, instanceId, cancellationToken: cancellationToken);
        }

        [McpServerTool]
        [Description("Steps into the current statement in Visual Studio, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioStepInto(
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.StepInto, approved, instanceId, cancellationToken: cancellationToken);
        }

        [McpServerTool]
        [Description("Steps out of the current method in Visual Studio, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioStepOut(
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.StepOut, approved, instanceId, cancellationToken: cancellationToken);
        }

        [McpServerTool]
        [Description("Continues the current Visual Studio debugging session, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioContinueDebugging(
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.Continue, approved, instanceId, cancellationToken: cancellationToken);
        }

        [McpServerTool]
        [Description("Requests Visual Studio to pause/break all debuggee threads, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioPauseDebugging(
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.Pause, approved, instanceId, cancellationToken: cancellationToken);
        }

        [McpServerTool]
        [Description("Sets a Visual Studio breakpoint at filePath:line, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioSetBreakpoint(
            [Description("Absolute source file path.")] string filePath,
            [Description("1-based source line number.")] int line,
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("1-based source column. Defaults to 1.")] int column = 1,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.SetBreakpoint, approved, instanceId, filePath, line, column, cancellationToken);
        }

        [McpServerTool]
        [Description("Removes a Visual Studio breakpoint matching filePath:line, then returns a fresh snapshot. Requires approved=true after explicit user approval.")]
        public Task<DebugActionResult> VisualStudioRemoveBreakpoint(
            [Description("Absolute source file path.")] string filePath,
            [Description("1-based source line number.")] int line,
            [Description("Must be true only after the user explicitly approves this debug action.")] bool approved = false,
            [Description("1-based source column. Defaults to 1.")] int column = 1,
            [Description("Optional Visual Studio instance id, process id, or solution name. Leave empty to use the most recent instance.")] string? instanceId = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteDebugAction(DebugActionKinds.RemoveBreakpoint, approved, instanceId, filePath, line, column, cancellationToken);
        }

        private Task<DebugActionResult> ExecuteDebugAction(
            string action,
            bool approved,
            string? instanceId,
            string? filePath = null,
            int? line = null,
            int? column = null,
            CancellationToken cancellationToken = default)
        {
            var request = new DebugActionRequest
            {
                InstanceId = instanceId,
                Action = action,
                Approved = approved,
                FilePath = filePath,
                Line = line,
                Column = column,
                SnapshotRequest = new DebugSnapshotRequest()
            };

            return _client.ExecuteDebugActionAsync(request, cancellationToken);
        }
    }
}

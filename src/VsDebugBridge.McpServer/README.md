# AI Debug Lens MCP Server

Local stdio MCP server for the AI Debug Lens VSIX.

## Tools

- `VisualStudioListInstances`: list registered Visual Studio bridge instances.
- `VisualStudioBridgeHealthCheck`: diagnose discovery, stale registrations, process liveness, and pipe reachability.
- `VisualStudioBridgePing`: verify one registered bridge pipe responds.
- `VisualStudioDebugSnapshot`: return the read-only debugger snapshot, including source context, selected thread/frame, exception details, locals, build errors, and recent Output window lines.
- `VisualStudioStepOver`, `VisualStudioStepInto`, `VisualStudioStepOut`: approved stepping actions.
- `VisualStudioContinueDebugging`, `VisualStudioPauseDebugging`: approved execution-control actions.
- `VisualStudioSetBreakpoint`, `VisualStudioRemoveBreakpoint`: approved breakpoint actions.

## Local Development Config

```toml
[mcp_servers.vs_debug_bridge]
command = "dotnet"
args = ["run", "--project", "C:\\path\\to\\ai-debug-lens\\src\\VsDebugBridge.McpServer\\VsDebugBridge.McpServer.csproj"]
startup_timeout_sec = 20
tool_timeout_sec = 60
enabled_tools = ["VisualStudioListInstances", "VisualStudioDebugSnapshot", "VisualStudioBridgePing", "VisualStudioBridgeHealthCheck", "VisualStudioStepOver", "VisualStudioStepInto", "VisualStudioStepOut", "VisualStudioContinueDebugging", "VisualStudioPauseDebugging", "VisualStudioSetBreakpoint", "VisualStudioRemoveBreakpoint"]
```

Run `VisualStudioBridgeHealthCheck` first after installing or updating the VSIX.

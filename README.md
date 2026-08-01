# AI Debug Lens

AI Debug Lens lets an AI coding agent read the current Visual Studio debugger context through a local MCP server.

Use it when you want an agent to see the active solution, file, line, call stack, locals, exceptions, Error List items, and recent Output window text while you debug a .NET project in Visual Studio.

## How It Works

- Visual Studio extension: runs inside Visual Studio and exposes debugger snapshots over a local named pipe.
- MCP server: runs as a local `stdio` process started by your AI agent.
- AI agent: connects to the MCP server and calls the Visual Studio tools.

This must run on the same Windows machine and user session as Visual Studio. It is not meant for cloud-only agents that cannot access your local Visual Studio process.

## Requirements

- Windows
- Visual Studio 2022 or newer
- .NET SDK 8.0 or newer
- PowerShell
- An AI agent/client with local MCP `stdio` support, such as Codex CLI, Claude Code, GitHub Copilot CLI, VS Code Copilot Chat, Cursor, or another MCP-capable client

## Quick Setup From Source

Clone or copy this repository, then run:

```powershell
dotnet restore .\VsDebugBridge.slnx
dotnet build .\VsDebugBridge.slnx
dotnet test .\VsDebugBridge.slnx --no-build
.\scripts\setup.ps1
```

To build and launch the VSIX installer:

```powershell
.\scripts\setup.ps1 -InstallVsix
```

After installing the VSIX:

1. Restart Visual Studio.
2. Open a C#/.NET solution.
3. Start debugging or break at a breakpoint.
4. Configure your AI agent to run the MCP server.
5. Ask the agent to run `VisualStudioBridgeHealthCheck`.

## MCP Server Command

All clients need the same local command:

```text
dotnet run --project C:\path\to\ai-debug-lens\src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj
```

Replace `C:\path\to\ai-debug-lens` with your local repository path.

## Codex CLI / ChatGPT Desktop

Add the MCP server from the command line:

```powershell
codex mcp add vs_debug_bridge -- dotnet run --project C:\path\to\ai-debug-lens\src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj
```

Or add it manually.

Add this to your Codex config, usually `~/.codex/config.toml`:

```toml
[mcp_servers.vs_debug_bridge]
command = "dotnet"
args = ["run", "--project", "C:\\path\\to\\ai-debug-lens\\src\\VsDebugBridge.McpServer\\VsDebugBridge.McpServer.csproj"]
startup_timeout_sec = 20
tool_timeout_sec = 60
enabled_tools = [
  "VisualStudioListInstances",
  "VisualStudioDebugSnapshot",
  "VisualStudioBridgePing",
  "VisualStudioBridgeHealthCheck",
  "VisualStudioStepOver",
  "VisualStudioStepInto",
  "VisualStudioStepOut",
  "VisualStudioContinueDebugging",
  "VisualStudioPauseDebugging",
  "VisualStudioSetBreakpoint",
  "VisualStudioRemoveBreakpoint"
]
```

Restart Codex, the IDE extension, or the desktop app after changing the config.

## Claude Code

Claude Code supports local `stdio` MCP servers. Configure this server in your project `.mcp.json` or with the equivalent `claude mcp add` command for your installed Claude Code version.

Example `.mcp.json`:

```json
{
  "mcpServers": {
    "vs_debug_bridge": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\ai-debug-lens\\src\\VsDebugBridge.McpServer\\VsDebugBridge.McpServer.csproj"
      ]
    }
  }
}
```

## GitHub Copilot CLI

Add the MCP server from the command line:

```powershell
copilot mcp add vs_debug_bridge -- dotnet run --project C:\path\to\ai-debug-lens\src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj
```

Or, in Copilot CLI interactive mode, run:

```text
/mcp add
```

Then restart the Copilot CLI session and check that the MCP tools are available.

## VS Code Copilot Chat

For VS Code with Copilot Chat agent mode, add a local MCP server configuration similar to:

```json
{
  "servers": {
    "vs_debug_bridge": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\ai-debug-lens\\src\\VsDebugBridge.McpServer\\VsDebugBridge.McpServer.csproj"
      ]
    }
  }
}
```

Enable the server in the tools picker before asking Copilot to inspect Visual Studio debugger state.

## Other MCP Clients

Use a local `stdio` MCP server with:

- Command: `dotnet`
- Args: `run`, `--project`, `C:\path\to\ai-debug-lens\src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj`
- Working directory: optional, usually the repository root
- Environment variables: none required

Do not use this bridge from a hosted/cloud MCP configuration unless that environment can access the same local Visual Studio process and named pipe.

## Useful Agent Prompts

```text
Run VisualStudioBridgeHealthCheck and tell me whether the VSIX is reachable.
```

```text
Get the VisualStudioDebugSnapshot and summarize the current break location, exception, locals, call stack, Error List, and Output window.
```

```text
Step over once with approved=true, then refresh the debugger snapshot.
```

## MCP Tools

- `VisualStudioListInstances`
- `VisualStudioDebugSnapshot`
- `VisualStudioBridgePing`
- `VisualStudioBridgeHealthCheck`
- `VisualStudioStepOver`
- `VisualStudioStepInto`
- `VisualStudioStepOut`
- `VisualStudioContinueDebugging`
- `VisualStudioPauseDebugging`
- `VisualStudioSetBreakpoint`
- `VisualStudioRemoveBreakpoint`

## Debug Action Safety

Snapshot and health-check tools are read-only.

Debugger actions require `approved=true` before the Visual Studio extension executes them:

- step over, step into, step out
- continue and pause
- set or remove breakpoint at `filePath:line`

Each action returns a fresh debugger snapshot after execution.

## Troubleshooting

Run `VisualStudioBridgeHealthCheck` first. It reports:

- discovery folder path
- Visual Studio process state
- stale registration files
- named-pipe reachability
- MCP and IPC versions

Common fixes:

- Restart Visual Studio after installing or updating the VSIX.
- Make sure the solution is open in Visual Studio.
- Make sure the MCP server command uses the correct local path.
- If several Visual Studio instances are open, pass `instanceId`, process id, or solution name to `VisualStudioDebugSnapshot`.
- If tools do not appear in the AI client, restart the client and re-check its MCP configuration.

## Build Release Artifacts

```powershell
.\scripts\release.ps1
```

This builds, tests, packs the MCP server, and creates the VSIX package under `artifacts\release-*`.

## Projects

- `src/VsDebugBridge.Contracts`: shared snapshot and IPC DTOs
- `src/VsDebugBridge.Ipc`: named-pipe client/server and discovery files
- `src/VsDebugBridge.McpServer`: local stdio MCP server
- `src/VsDebugBridge.VisualStudioExtension`: Visual Studio VSIX
- `tests/VsDebugBridge.Tests`: focused contract/discovery tests
- `samples/DebugSample`: small console app for manual debugger validation

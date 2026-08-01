# Release Checklist

## Local Release Build

```powershell
.\scripts\release.ps1
```

The script creates `artifacts\release-<timestamp>` with:

- `AIDebugLens.vsix`
- `VsDebugBridge.McpServer.<version>.nupkg`

Use `-SkipTests` only when tests already passed in the same workspace.

## Manual v1.0 Gates

- Sign the VSIX with the release certificate.
- Install the signed VSIX on a clean Visual Studio 2022+ machine.
- Configure Codex MCP from the generated setup output.
- Run `VisualStudioBridgeHealthCheck`.
- Open `samples\DebugSample`, break inside `CreateInvoice`, and run `VisualStudioDebugSnapshot`.
- Verify approved actions with `approved=true`: step over, continue/pause, set/remove breakpoint.
- Publish the signed VSIX and MCP package.

## Safety Requirement

Read-only tools must work without approval. Debug actions must remain separate tools and must not execute unless `approved=true` is present.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$SkipTests,

    [switch]$InstallVsix
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $Root "VsDebugBridge.slnx"
$McpProject = Join-Path $Root "src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj"
$VsixProject = Join-Path $Root "src\VsDebugBridge.VisualStudioExtension\VsDebugBridge.VisualStudioExtension.csproj"
$VsixProjectDirectory = Split-Path -Parent $VsixProject
$VsixPath = Join-Path $Root "src\VsDebugBridge.VisualStudioExtension\bin\$Configuration\net472\AIDebugLens.vsix"

function Find-VisualStudioTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FindPattern,

        [Parameter(Mandatory = $true)]
        [string[]]$FallbackPaths
    )

    $VsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $VsWhere) {
        $Found = & $VsWhere -latest -products * -find $FindPattern | Select-Object -First 1
        if ($Found -and (Test-Path -LiteralPath $Found)) {
            return $Found
        }
    }

    foreach ($Path in $FallbackPaths) {
        if (Test-Path -LiteralPath $Path) {
            return $Path
        }
    }

    throw "Could not find Visual Studio tool matching '$FindPattern'. Install Visual Studio 2022+ with MSBuild."
}

$MsBuild = Find-VisualStudioTool `
    -FindPattern "MSBuild\**\Bin\MSBuild.exe" `
    -FallbackPaths @(
        "$env:ProgramFiles\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\17\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\17\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\17\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

Write-Host "Restoring solution..."
dotnet restore $Solution

Write-Host "Building solution..."
dotnet build $Solution -c $Configuration --no-restore

if (-not $SkipTests) {
    Write-Host "Running tests..."
    dotnet test $Solution -c $Configuration --no-build --verbosity minimal
}

Write-Host "Packaging VSIX..."
$GeneratedVsixManifest = Join-Path $VsixProjectDirectory "obj\$Configuration\extension.vsixmanifest"
$GeneratedVsixFiles = Join-Path $VsixProjectDirectory "obj\$Configuration\files.json"
Remove-Item -LiteralPath $GeneratedVsixManifest, $GeneratedVsixFiles, $VsixPath -Force -ErrorAction SilentlyContinue
& $MsBuild $VsixProject /t:Build,GeneratePkgDef,CreateVsixContainer /p:Configuration=$Configuration

if (-not (Test-Path -LiteralPath $VsixPath)) {
    throw "VSIX was not created at $VsixPath"
}

if ($InstallVsix) {
    $VsixInstaller = Find-VisualStudioTool `
        -FindPattern "Common7\IDE\VSIXInstaller.exe" `
        -FallbackPaths @(
            "$env:ProgramFiles\Microsoft Visual Studio\18\Enterprise\Common7\IDE\VSIXInstaller.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\18\Professional\Common7\IDE\VSIXInstaller.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\17\Enterprise\Common7\IDE\VSIXInstaller.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\17\Professional\Common7\IDE\VSIXInstaller.exe",
            "$env:ProgramFiles\Microsoft Visual Studio\17\Community\Common7\IDE\VSIXInstaller.exe"
        )

    Write-Host "Installing VSIX..."
    & $VsixInstaller $VsixPath
}

$EscapedMcpProject = $McpProject.Replace("\", "\\")

Write-Host ""
Write-Host "VSIX:"
Write-Host $VsixPath
Write-Host ""
Write-Host "Codex MCP config:"
@"
[mcp_servers.vs_debug_bridge]
command = "dotnet"
args = ["run", "--project", "$EscapedMcpProject"]
startup_timeout_sec = 20
tool_timeout_sec = 60
enabled_tools = ["VisualStudioListInstances", "VisualStudioDebugSnapshot", "VisualStudioBridgePing", "VisualStudioBridgeHealthCheck", "VisualStudioStepOver", "VisualStudioStepInto", "VisualStudioStepOut", "VisualStudioContinueDebugging", "VisualStudioPauseDebugging", "VisualStudioSetBreakpoint", "VisualStudioRemoveBreakpoint"]
"@ | Write-Host

Write-Host ""
Write-Host "After installing the VSIX, restart Visual Studio, open a C# solution, then ask Codex to run VisualStudioBridgeHealthCheck."

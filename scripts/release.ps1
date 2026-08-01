[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $Root "VsDebugBridge.slnx"
$McpProject = Join-Path $Root "src\VsDebugBridge.McpServer\VsDebugBridge.McpServer.csproj"
$VsixPath = Join-Path $Root "src\VsDebugBridge.VisualStudioExtension\bin\$Configuration\net472\AIDebugLens.vsix"
$Artifacts = Join-Path $Root ("artifacts\release-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

New-Item -ItemType Directory -Path $Artifacts | Out-Null

Write-Host "Release artifacts:"
Write-Host $Artifacts
Write-Host ""

dotnet restore $Solution
dotnet build $Solution -c $Configuration --no-restore

if (-not $SkipTests) {
    dotnet test $Solution -c $Configuration --no-build --verbosity minimal
}

dotnet pack $McpProject -c $Configuration --no-build -o $Artifacts

& (Join-Path $PSScriptRoot "setup.ps1") -Configuration $Configuration -SkipTests

if (-not (Test-Path -LiteralPath $VsixPath)) {
    throw "VSIX was not created at $VsixPath"
}

Copy-Item -LiteralPath $VsixPath -Destination $Artifacts

Write-Host ""
Write-Host "Created release artifacts:"
Get-ChildItem -LiteralPath $Artifacts | Select-Object FullName, Length

Write-Host ""
Write-Host "Before v1.0 publishing: sign the VSIX with the release certificate, then publish the VSIX and MCP package from the artifact folder."

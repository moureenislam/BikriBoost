param(
    [ValidateSet('Restore', 'Build', 'Test')]
    [string]$Action = 'Build'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectDotnet = & (Join-Path $PSScriptRoot 'Resolve-DotNet.ps1')
Push-Location -LiteralPath $projectRoot
try {
    & $projectDotnet $Action.ToLowerInvariant() BikriBoost.slnx --nologo
    $projectExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $projectExitCode

param([ValidateRange(1024, 65535)][int]$Port = 5071)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$demoDotnet = & (Join-Path $PSScriptRoot 'scripts/Resolve-DotNet.ps1')
& $demoDotnet run --project BikriBoost.Web --no-launch-profile -- --environment Development --Database:Provider=Sqlite --urls "http://localhost:$Port"
exit $LASTEXITCODE

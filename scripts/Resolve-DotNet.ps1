$ErrorActionPreference = 'Stop'
$installedDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$candidates = @(
    $(if ($installedDotnet) { $installedDotnet.Source })
    $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'BikriBoost/dotnet/dotnet.exe' })
)
foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate)) {
        $sdks = & $candidate --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^10\.')) {
            $env:DOTNET_ROOT = Split-Path -Parent $candidate
            $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
            return $candidate
        }
    }
}
throw 'The .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and try again.'

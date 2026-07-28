param(
    [Parameter(Position = 0)]
    [string]$Mode = 'start'
)

$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
Set-Location $appRoot

if (-not $env:APP_VARIANT) { $env:APP_VARIANT = 'development' }
if (-not $env:EXPO_PUBLIC_APP_ENV) { $env:EXPO_PUBLIC_APP_ENV = 'development' }
if (-not $env:EXPO_PUBLIC_API_BASE_URL) { $env:EXPO_PUBLIC_API_BASE_URL = 'http://127.0.0.1:5190' }

$npx = (Get-Command npx.cmd).Source

switch ($Mode) {
    { $_ -in 'start', 'run', '--dev-client', 'dev-client' } {
        & $npx expo start --dev-client
        break
    }
    { $_ -in '--ios', 'ios' } {
        & $npx expo start --dev-client --ios
        break
    }
    { $_ -in '--android', 'android' } {
        & $npx expo start --dev-client --android
        break
    }
    { $_ -in '--web', 'web' } {
        & $npx expo start --web
        break
    }
    { $_ -in '--tunnel', 'tunnel' } {
        & $npx expo start --dev-client --tunnel
        break
    }
    { $_ -in '--export-web', 'export-web' } {
        & $npx expo export --platform web
        break
    }
    { $_ -in '--doctor', 'doctor' } {
        & $npx expo-doctor
        break
    }
    { $_ -in '--help', 'help' } {
        Write-Host 'usage: ./script/build_and_run.ps1 [start|--ios|--android|--web|--dev-client|--tunnel|--export-web|--doctor|--help]'
        return
    }
    default {
        throw "Unsupported mode: $Mode"
    }
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 5190,

    [string]$LanAddress
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LanAddress)) {
    $defaultRoute = Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1

    if ($null -eq $defaultRoute) {
        throw 'No active IPv4 default route was found. Pass -LanAddress explicitly.'
    }

    $LanAddress = Get-NetIPAddress -AddressFamily IPv4 -InterfaceIndex $defaultRoute.InterfaceIndex |
        Where-Object { $_.IPAddress -notlike '169.254.*' } |
        Select-Object -ExpandProperty IPAddress -First 1
}

$parsedAddress = $null
if (-not [System.Net.IPAddress]::TryParse($LanAddress, [ref]$parsedAddress)) {
    throw "The LAN address '$LanAddress' is not a valid IP address."
}

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}

$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
$projectPath = Join-Path $PSScriptRoot '..\src\Rydo.Api'

Write-Host "RYDO API environment: $($env:ASPNETCORE_ENVIRONMENT)"
Write-Host "Phone API URL: http://${LanAddress}:$Port"
Write-Host 'The phone and this computer must be on the same network.'

dotnet run --project $projectPath --no-launch-profile

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$backendDirectory = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendDirectory '.env.docker.local'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw 'Configure Docker local credentials first with configure-docker-local.ps1.'
}

function ConvertFrom-RydoSecureString {
    param([Parameter(Mandatory)][Security.SecureString] $Value)
    return [System.Net.NetworkCredential]::new('', $Value).Password
}

function Assert-RydoSingleLineValue {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Value
    )
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name cannot be empty." }
    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "$Name cannot contain a line break."
    }
}

function Set-RydoEnvironmentValue {
    param(
        [Parameter(Mandatory)][string[]] $Lines,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Value
    )
    $prefix = "$Name="
    $updated = $false
    $result = foreach ($line in $Lines) {
        if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            "$prefix$Value"
            $updated = $true
        }
        else { $line }
    }
    if (-not $updated) { $result += "$prefix$Value" }
    return $result
}

$adminPassword = $null
try {
    $adminEmail = (Read-Host 'Enter the local administrator email').Trim().ToLowerInvariant()
    Write-Host 'Use a dedicated phone number that is not registered as a RYDO Passenger or Driver.'
    $adminPhone = (Read-Host 'Enter the unique administrator phone in international format (for example +27821234567)').Trim()
    $secureAdminPassword = Read-Host 'Enter a local administrator password (minimum 16 characters)' -AsSecureString
    $adminPassword = ConvertFrom-RydoSecureString $secureAdminPassword

    Assert-RydoSingleLineValue -Name 'Administrator email' -Value $adminEmail
    Assert-RydoSingleLineValue -Name 'Administrator phone' -Value $adminPhone
    Assert-RydoSingleLineValue -Name 'Administrator password' -Value $adminPassword
    if ($adminEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { throw 'Enter a valid administrator email.' }
    if ($adminPhone -notmatch '^\+[1-9]\d{7,14}$') { throw 'Enter an international phone number beginning with +.' }
    if ($adminPassword.Length -lt 16 -or $adminPassword.Length -gt 200) {
        throw 'The administrator password must contain between 16 and 200 characters.'
    }

    $environmentLines = [System.IO.File]::ReadAllLines($environmentFile)
    $environmentLines = Set-RydoEnvironmentValue $environmentLines 'AdminAccess__Enabled' 'true'
    $environmentLines = Set-RydoEnvironmentValue $environmentLines 'AdminAccess__BootstrapEmail' $adminEmail
    $environmentLines = Set-RydoEnvironmentValue $environmentLines 'AdminAccess__BootstrapPhoneNumber' $adminPhone
    $environmentLines = Set-RydoEnvironmentValue $environmentLines 'AdminAccess__BootstrapPassword' $adminPassword

    [System.IO.File]::WriteAllLines(
        $environmentFile,
        $environmentLines,
        [System.Text.UTF8Encoding]::new($false)
    )
    Write-Host 'Local administrator access configured. Recreate the API container to apply it:'
    Write-Host 'docker compose --env-file .env.docker.local up -d --build api'
}
finally {
    Remove-Variable secureAdminPassword, adminPassword, environmentLines -ErrorAction SilentlyContinue
}

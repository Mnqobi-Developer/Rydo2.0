[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$backendDirectory = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $backendDirectory 'src\Rydo.Api'
$environmentFile = Join-Path $backendDirectory '.env.docker.local'
$localCertificateFile = Join-Path $backendDirectory '.env.docker.local.ca.crt'

function ConvertFrom-RydoSecureString {
    param(
        [Parameter(Mandatory)]
        [Security.SecureString] $Value
    )

    $credential = [System.Net.NetworkCredential]::new('', $Value)
    return $credential.Password
}

function Assert-RydoSingleLineValue {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [string] $Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name cannot be empty."
    }

    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "$Name cannot contain a line break."
    }
}

function Export-RydoLocalTlsRoot {
    param(
        [Parameter(Mandatory)]
        [string] $Destination
    )

    $interceptionCertificate = @(
        Get-ChildItem Cert:\CurrentUser\Root, Cert:\LocalMachine\Root -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Subject -match 'Kaspersky|Zscaler|Fortinet|Cisco Umbrella|ESET|Avast'
            } |
            Sort-Object NotAfter -Descending
    ) | Select-Object -First 1

    if ($null -eq $interceptionCertificate) {
        [System.IO.File]::WriteAllText(
            $Destination,
            '',
            [System.Text.UTF8Encoding]::new($false)
        )
        return $false
    }

    $base64Certificate = [Convert]::ToBase64String(
        $interceptionCertificate.RawData,
        [Base64FormattingOptions]::InsertLineBreaks
    )
    $pemCertificate = "-----BEGIN CERTIFICATE-----`n" +
        $base64Certificate.Replace("`r`n", "`n") +
        "`n-----END CERTIFICATE-----`n"
    [System.IO.File]::WriteAllText(
        $Destination,
        $pemCertificate,
        [System.Text.UTF8Encoding]::new($false)
    )
    return $true
}

$databasePassword = $null
$googleMapsKey = $null

try {
    $secureDatabasePassword = Read-Host 'Enter the Supabase database password' -AsSecureString
    $databasePassword = ConvertFrom-RydoSecureString $secureDatabasePassword
    Assert-RydoSingleLineValue -Name 'Supabase database password' -Value $databasePassword

    $secretLines = & dotnet user-secrets list --project $apiProject 2>$null
    if ($LASTEXITCODE -eq 0) {
        foreach ($secretLine in $secretLines) {
            if ($secretLine -match '^GoogleMaps:ServerApiKey\s*=\s*(.+)$') {
                $googleMapsKey = $Matches[1]
                break
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($googleMapsKey)) {
        Write-Host 'GoogleMaps:ServerApiKey was not found in .NET user-secrets.'
        $secureGoogleMapsKey = Read-Host 'Enter the Google Maps server API key' -AsSecureString
        $googleMapsKey = ConvertFrom-RydoSecureString $secureGoogleMapsKey
    }

    Assert-RydoSingleLineValue -Name 'Google Maps server API key' -Value $googleMapsKey

    $escapedDatabasePassword = $databasePassword.Replace('"', '""')
    $connectionString = 'Host=aws-0-eu-west-1.pooler.supabase.com;' +
        'Port=5432;' +
        'Database=postgres;' +
        'Username=postgres.xvbiyctmgtgjtlbgjuas;' +
        'Password="' + $escapedDatabasePassword + '";' +
        'SSL Mode=Require;' +
        'GSS Encryption Mode=Disable;' +
        'Pooling=true;' +
        'Maximum Pool Size=10;' +
        'Application Name=rydo-api'

    $environmentLines = @(
        "ConnectionStrings__RydoDatabase=$connectionString"
        "GoogleMaps__ServerApiKey=$googleMapsKey"
    )

    [System.IO.File]::WriteAllLines(
        $environmentFile,
        $environmentLines,
        [System.Text.UTF8Encoding]::new($false)
    )

    $localTlsRootExported = Export-RydoLocalTlsRoot -Destination $localCertificateFile

    Write-Host "Docker credentials configured in $environmentFile"
    Write-Host 'The file is ignored by Git and excluded from Docker image builds.'
    if ($localTlsRootExported) {
        Write-Host 'A local HTTPS inspection root was configured for this computer only.'
    }
}
finally {
    Remove-Variable secureDatabasePassword, secureGoogleMapsKey, databasePassword, googleMapsKey, escapedDatabasePassword, connectionString, environmentLines, localTlsRootExported -ErrorAction SilentlyContinue
}

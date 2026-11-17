[CmdletBinding()]
param(
    # Overrides the connection string built from .env (or the Compose defaults when .env is absent).
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$services = @('Identity', 'Parking', 'Booking', 'Payment', 'Notification')

if (-not $ConnectionString) {
    $envFile = Join-Path $repositoryRoot '.env'
    if (Test-Path -LiteralPath $envFile) {
        # 1. First priority: Check for explicit ConnectionStrings:DefaultConnection or ConnectionStrings__DefaultConnection
        foreach ($line in Get-Content -LiteralPath $envFile) {
            if ($line -match '^\s*ConnectionStrings[:_]+DefaultConnection\s*=\s*(.*?)\s*$') {
                $raw = $Matches[1].Trim()
                if ($raw.StartsWith('"') -and $raw.EndsWith('"')) { $raw = $raw.Substring(1, $raw.Length - 2) }
                elseif ($raw.StartsWith("'") -and $raw.EndsWith("'")) { $raw = $raw.Substring(1, $raw.Length - 2) }
                if ($raw) {
                    $ConnectionString = $raw
                    break
                }
            }
        }
    }

    # 2. Second priority: If no explicit ConnectionString, check POSTGRES_* settings
    if (-not $ConnectionString) {
        $settings = @{
            POSTGRES_DB        = 'parking_system'
            POSTGRES_USER      = 'parking_dev'
            POSTGRES_PASSWORD  = 'parking_dev_local_only'
            POSTGRES_HOST_PORT = '5433'
            POSTGRES_HOST      = '127.0.0.1'
            POSTGRES_SSL_MODE  = 'Disable'
        }
        if (Test-Path -LiteralPath $envFile) {
            foreach ($line in Get-Content -LiteralPath $envFile) {
                if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and $settings.ContainsKey($Matches[1]) -and $Matches[2]) {
                    $val = $Matches[2].Trim()
                    if ($val.StartsWith('"') -and $val.EndsWith('"')) { $val = $val.Substring(1, $val.Length - 2) }
                    elseif ($val.StartsWith("'") -and $val.EndsWith("'")) { $val = $val.Substring(1, $val.Length - 2) }
                    $settings[$Matches[1]] = $val
                }
            }
        }

        $hostName = $settings['POSTGRES_HOST']
        if ($hostName -eq 'postgres') { $hostName = '127.0.0.1' }
        $isLocal = ($hostName -eq '127.0.0.1' -or $hostName -eq 'localhost')
        $sslMode = if ($settings['POSTGRES_SSL_MODE']) { $settings['POSTGRES_SSL_MODE'] } elseif ($isLocal) { 'Disable' } else { 'Require' }
        $sslPart = if ($sslMode -eq 'Disable') { 'GSS Encryption Mode=Disable' } else { "SSL Mode=$sslMode;Trust Server Certificate=true" }

        $ConnectionString = "Host=$hostName;Port=$($settings.POSTGRES_HOST_PORT);Database=$($settings.POSTGRES_DB);" +
            "Username=$($settings.POSTGRES_USER);Password=$($settings.POSTGRES_PASSWORD);$sslPart"
    }
}

$maskedTarget = $ConnectionString -replace 'Password=[^;]+', 'Password=******'
Write-Host "Target Database: $maskedTarget" -ForegroundColor Cyan

Push-Location $repositoryRoot
$previousConnectionString = $env:ConnectionStrings__DefaultConnection
try {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    foreach ($service in $services) {
        Write-Host "== $service"
        dotnet ef database update `
            --project "src/Services/$service/$service.Persistence/$service.Persistence.csproj" `
            --startup-project "src/Services/$service/$service.WebAPI/$service.WebAPI.csproj" `
            --context "${service}DbContext" `
            --connection "$ConnectionString"
        if ($LASTEXITCODE -ne 0) { throw "Migration failed for $service." }
    }
}
finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnectionString
    Pop-Location
}

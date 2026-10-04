[CmdletBinding()]
param(
    # Overrides the connection string built from .env (or the Compose defaults when .env is absent).
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$services = @('Identity', 'Parking', 'Booking', 'Payment', 'Notification')

if (-not $ConnectionString) {
    # Same defaults as docker-compose.yml; values in .env take precedence.
    $settings = @{
        POSTGRES_DB        = 'parking_system'
        POSTGRES_USER      = 'parking_dev'
        POSTGRES_PASSWORD  = 'parking_dev_local_only'
        POSTGRES_HOST_PORT = '5433'
    }
    $envFile = Join-Path $repositoryRoot '.env'
    if (Test-Path -LiteralPath $envFile) {
        foreach ($line in Get-Content -LiteralPath $envFile) {
            if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and $settings.ContainsKey($Matches[1]) -and $Matches[2]) {
                $settings[$Matches[1]] = $Matches[2]
            }
        }
    }
    # 127.0.0.1 rather than localhost: Compose publishes ports on IPv4 loopback only.
    $ConnectionString = "Host=127.0.0.1;Port=$($settings.POSTGRES_HOST_PORT);Database=$($settings.POSTGRES_DB);" +
        "Username=$($settings.POSTGRES_USER);Password=$($settings.POSTGRES_PASSWORD);GSS Encryption Mode=Disable"
}

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
            --context "${service}DbContext"
        if ($LASTEXITCODE -ne 0) { throw "Migration failed for $service." }
    }
}
finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnectionString
    Pop-Location
}

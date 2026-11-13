<#
.SYNOPSIS
Applies the pending EF Core migrations of all five services to the local PostgreSQL database.

.DESCRIPTION
Run from anywhere; the script finds the repository root. The connection string is built from .env
(POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_HOST_PORT) with the same defaults as
docker-compose.yml, or taken from -ConnectionString. The password is never printed.

Each service is migrated on its own. The script stops at the first failure and says where to look.
After a successful run it also checks that no model change is missing a migration.

.EXAMPLE
.\scripts\dev\Migrate-All.ps1

.EXAMPLE
.\scripts\dev\Migrate-All.ps1 -ConnectionString "Host=127.0.0.1;Port=5433;Database=parking_demo;Username=parking_dev;Password=..."
#>
[CmdletBinding()]
param(
    # Overrides the connection string built from .env (or the Compose defaults when .env is absent).
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$services = @('Identity', 'Parking', 'Booking', 'Payment', 'Notification')

if (-not $ConnectionString) {
    # Same defaults as docker-compose.yml (a public local-development value); values in .env take precedence.
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

# Never echo the password: show the target without it.
$target = ($ConnectionString -split ';' | Where-Object { $_ -match '^(Host|Port|Database)=' }) -join ' '
Write-Host "Target: $target"

# The WebAPI startup projects validate the RabbitMQ settings when the host is built, so give them
# throwaway values. Nothing connects to RabbitMQ during a migration.
$variables = @{
    ConnectionStrings__DefaultConnection = $ConnectionString
    RabbitMq__Host                       = '127.0.0.1'
    RabbitMq__Port                       = '5672'
    RabbitMq__Username                   = 'migration'
    RabbitMq__Password                   = 'migration'
    RabbitMq__VirtualHost                = '/'
}
$previous = @{}
foreach ($name in $variables.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }

Push-Location $repositoryRoot
try {
    foreach ($name in $variables.Keys) { Set-Item -Path "Env:$name" -Value $variables[$name] }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    foreach ($service in $services) {
        $project = "src/Services/$service/$service.Persistence/$service.Persistence.csproj"
        $startup = "src/Services/$service/$service.WebAPI/$service.WebAPI.csproj"
        $context = "${service}DbContext"

        Write-Host "== $service"
        # A fresh clone or worktree has no restored packages yet, and dotnet ef does not restore.
        dotnet restore $startup --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for $service." }
        dotnet ef database update --project $project --startup-project $startup --context $context
        if ($LASTEXITCODE -ne 0) {
            throw ("Migration failed for $service. If the message says a column or table already exists, the database " +
                "has the schema but not the history row (for example a migration renamed on another branch): compare " +
                "$($service.ToLowerInvariant()).""__EFMigrationsHistory"" with src/Services/$service/$service.Persistence/Migrations " +
                "before touching anything.")
        }
    }

    foreach ($service in $services) {
        dotnet ef migrations has-pending-model-changes `
            --project "src/Services/$service/$service.Persistence/$service.Persistence.csproj" `
            --startup-project "src/Services/$service/$service.WebAPI/$service.WebAPI.csproj" `
            --context "${service}DbContext" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "$service has model changes without a migration. Add one before merging."
        }
    }
}
finally {
    foreach ($name in $variables.Keys) {
        if ($null -eq $previous[$name]) { Remove-Item -Path "Env:$name" -ErrorAction SilentlyContinue }
        else { Set-Item -Path "Env:$name" -Value $previous[$name] }
    }
    Pop-Location
}

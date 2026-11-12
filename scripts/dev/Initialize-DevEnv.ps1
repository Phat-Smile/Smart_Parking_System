<#
.SYNOPSIS
Creates the repository .env from .env.example and fills the blank local secrets with random values.

.DESCRIPTION
Run from anywhere; the script finds the repository root. It never overwrites a value that is already set,
and it only touches the keys listed in -Keys. Generated values stay in .env (ignored by Git); they are never
printed. Run it again at any time: it only fills what is still blank.

.EXAMPLE
.\scripts\dev\Initialize-DevEnv.ps1

.EXAMPLE
.\scripts\dev\Initialize-DevEnv.ps1 -Keys SECURITY_OTP_PEPPER,SENSOR_DEVICE_KEY
#>
[CmdletBinding()]
param(
    [string[]] $Keys = @('SECURITY_OTP_PEPPER')
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$example = Join-Path $root '.env.example'
$envFile = Join-Path $root '.env'

if (-not (Test-Path $example)) { throw ".env.example not found in $root" }

if (-not (Test-Path $envFile)) {
    Copy-Item $example $envFile
    Write-Host 'Created .env from .env.example.'
}

function New-RandomSecret {
    # Create().GetBytes($bytes) works on Windows PowerShell 5.1 as well as PowerShell 7.
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes)
}

$lines = [System.IO.File]::ReadAllLines($envFile)
$filled = @()
for ($i = 0; $i -lt $lines.Length; $i++) {
    foreach ($key in $Keys) {
        if ($lines[$i] -match ('^\s*' + [regex]::Escape($key) + '\s*=\s*$')) {
            $lines[$i] = "$key=$(New-RandomSecret)"
            $filled += $key
        }
    }
}

if ($filled.Count -gt 0) {
    [System.IO.File]::WriteAllLines($envFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

foreach ($key in $Keys) {
    $line = $lines | Where-Object { $_ -match ('^\s*' + [regex]::Escape($key) + '\s*=') } | Select-Object -First 1
    if (-not $line) { Write-Warning "$key is not in .env; add it manually."; continue }
    $set = ($line -split '=', 2)[1].Trim().Length -gt 0
    $state = if ($filled -contains $key) { 'generated' } elseif ($set) { 'already set' } else { 'STILL EMPTY' }
    Write-Host ("{0,-22} {1}" -f $key, $state)
}

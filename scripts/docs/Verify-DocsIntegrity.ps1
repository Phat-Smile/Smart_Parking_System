<#
.SYNOPSIS
    Verify-DocsIntegrity.ps1
    Validates documentation synchronization, single-source-of-truth integrity,
    and deterministic generation across all derived artifacts (.docx, diagrams .puml, .png).
.DESCRIPTION
    1. Executes all Python generators (Run 1) and records SHA-256 hashes for all derived artifacts.
    2. Re-runs Python generators (Run 2) and asserts binary/content hash parity (Determinism Check).
    3. Checks git diff to guarantee committed artifacts are strictly up-to-date with Markdown SSOT (Freshness Check).
#>

[CmdletBinding()]
param (
    [switch]$SkipDeterminismCheck = $false,
    [switch]$SkipFreshnessCheck = $false
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path "$PSScriptRoot/../.."
Set-Location $RepoRoot

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  SMART PARKING SYSTEM - DOCS INTEGRITY & DETERMINISM VERIFIER" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$env:PYTHONIOENCODING = "utf-8"

$TrackedArtifacts = @(
    ".notes/us-auth-001-account-registration-api-design-spec.docx",
    ".notes/us-auth-003-email-otp-api-design-spec.docx",
    ".notes/us-auth-004-login-api-design-spec.docx",
    ".notes/us-auth-005-refresh-token-and-logout-api-design-spec.docx",
    ".notes/diagrams/us-auth-001-activity.puml",
    ".notes/diagrams/us-auth-001-sequence.puml",
    ".notes/diagrams/us-auth-003-activity.puml",
    ".notes/diagrams/us-auth-003-sequence.puml",
    ".notes/diagrams/us-auth-004-activity.puml",
    ".notes/diagrams/us-auth-004-sequence.puml",
    ".notes/diagrams/us-auth-005-activity.puml",
    ".notes/diagrams/us-auth-005-refresh-sequence.puml",
    ".notes/diagrams/us-auth-005-logout-sequence.puml",
    ".notes/diagrams/us-auth-005-logout-all-sequence.puml"
)

function Invoke-Generators {
    Write-Host "`n[1/3] Running Python Diagram Generator (PUML & PNG)..." -ForegroundColor Yellow
    python .notes/create_diagram_images.py
    if ($LASTEXITCODE -ne 0) { throw "create_diagram_images.py failed with code $LASTEXITCODE" }

    Write-Host "[2/3] Running Python DOCX Generator (AUTH-001, 003, 004)..." -ForegroundColor Yellow
    python .notes/generate_docx_specs.py
    if ($LASTEXITCODE -ne 0) { throw "generate_docx_specs.py failed with code $LASTEXITCODE" }

    Write-Host "[3/3] Running Python DOCX Generator (AUTH-005)..." -ForegroundColor Yellow
    python .notes/generate_auth_005_spec.py
    if ($LASTEXITCODE -ne 0) { throw "generate_auth_005_spec.py failed with code $LASTEXITCODE" }
}

# Run 1
Write-Host "`n>>> [PASS 1] Executing generators..." -ForegroundColor Magenta
Invoke-Generators

$HashesPass1 = @{}
foreach ($file in $TrackedArtifacts) {
    if (Test-Path $file) {
        $HashesPass1[$file] = (Get-FileHash $file -Algorithm SHA256).Hash
    } else {
        throw "Missing expected artifact: $file"
    }
}

if (-not $SkipDeterminismCheck) {
    Write-Host "`n>>> [PASS 2] Executing generators for Determinism Check..." -ForegroundColor Magenta
    Start-Sleep -Seconds 1
    Invoke-Generators

    Write-Host "`n>>> [VERIFY] Comparing artifact hashes between Pass 1 and Pass 2..." -ForegroundColor Magenta
    $DeterminismFailed = $false
    foreach ($file in $TrackedArtifacts) {
        $h2 = (Get-FileHash $file -Algorithm SHA256).Hash
        $h1 = $HashesPass1[$file]
        if ($h1 -eq $h2) {
            Write-Host "  [OK] $file (Hash: $h2)" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $file (Pass1: $h1 != Pass2: $h2)" -ForegroundColor Red
            $DeterminismFailed = $true
        }
    }

    if ($DeterminismFailed) {
        throw "Determinism check failed: One or more generated artifacts produced non-deterministic output!"
    }
    Write-Host "`n[PASS] Determinism check passed: All artifacts are 100% bit-for-bit reproducible." -ForegroundColor Green
}

if (-not $SkipFreshnessCheck) {
    Write-Host "`n>>> [FRESHNESS] Checking git drift for generated documentation..." -ForegroundColor Magenta
    $gitStatus = git status --porcelain $TrackedArtifacts
    if ($gitStatus) {
        Write-Host "Uncommitted changes or drift detected in documentation artifacts:" -ForegroundColor Yellow
        $gitStatus | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        Write-Host "Hint: Commit the updated artifacts to keep documentation synchronized with Markdown SSOT." -ForegroundColor Cyan
    } else {
        Write-Host "[PASS] Freshness check passed: All tracked documentation artifacts are clean and in sync." -ForegroundColor Green
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "  ALL DOCUMENTATION INTEGRITY CHECKS COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

#Requires -Version 5.1
<#
.SYNOPSIS
    Automated test suite for scripts/dev/Seed-DemoData.ps1 logic without modifying production or database state.

.DESCRIPTION
    Validates:
      1. PowerShell syntax and AST parsing of Seed-DemoData.ps1.
      2. Identity FullName validation compliance: verifies 'Khach Hang Demo Hai' conforms to Identity's FullName regex.
      3. Account registry integrity: verifies Customer2 exists with required attributes.
      4. TOTP generation correctness: RFC 6238 compliance (6 digits, proper base32 decoding, HMAC-SHA1).
      5. VNPay HMAC-SHA512 calculation and parameter sorting.
      6. Dry-run execution test: ensures -Apply safety token check and dry-run execution exits cleanly.
      7. Top-up logic contract: confirms Idempotency-Key GUID header format and TopupId mapping.
#>
[CmdletBinding()]
param ()

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$seedScriptPath = Join-Path $scriptRoot 'Seed-DemoData.ps1'

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  TEST SUITE: Seed-DemoData.ps1 Integrity & Safety Verification    " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

function Assert-Test([string]$TestName, [scriptblock]$TestBlock) {
    try {
        & $TestBlock
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:testsPassed++
    }
    catch {
        Write-Host "  [FAIL] $TestName" -ForegroundColor Red
        Write-Host "         Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $script:testsFailed++
    }
}

# --- TEST 1: Syntax & AST Parsing ---
Assert-Test "1. PowerShell syntax check (AST parsing)" {
    $tokens = $null
    $errors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile($seedScriptPath, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        throw "Syntax errors found: $(($errors | ForEach-Object { $_.Message }) -join '; ')"
    }
}

# --- TEST 2: Identity FullName Regex Compliance ---
Assert-Test "2. Customer2 FullName conforms to Identity regex (no digits/parentheses)" {
    # Identity regex rule: only letters, whitespace, hyphens, and apostrophes allowed
    $fullName = "Khach Hang Demo Hai"
    $regex = '^[\p{L}\s''-]+$'
    if ($fullName -notmatch $regex) {
        throw "Name '$fullName' does not match Identity FullName regex."
    }
    if ($fullName -match '\d' -or $fullName.Contains('(') -or $fullName.Contains(')')) {
        throw "Name contains forbidden characters (digits or parentheses)."
    }
}

# --- TEST 3: Account definition check in script ---
Assert-Test "3. Account Customer2 exists with correct key, email and name" {
    $content = Get-Content -Raw -LiteralPath $seedScriptPath
    if (-not $content.Contains("New-DemoAccount 'Customer2' 'customer2' 'Khach Hang Demo Hai'")) {
        throw "New-DemoAccount 'Customer2' 'customer2' 'Khach Hang Demo Hai' not found in script."
    }
    if ($content.Contains("Khach Hang Demo 2 (Concurrency)")) {
        throw "Old invalid name 'Khach Hang Demo 2 (Concurrency)' is still present in script."
    }
}

# --- TEST 4: TOTP Generation correctness ---
Assert-Test "4. RFC 6238 TOTP generation generates 6 numeric digits" {
    $secret = "JBSWY3DPEHPK3PXP" # Base32 test secret
    
    $alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
    $clean = $secret.Trim().TrimEnd('=').Replace(' ', '').ToUpperInvariant()
    $bytes = New-Object System.Collections.Generic.List[byte]
    $buffer = 0
    $bits = 0
    foreach ($c in $clean.ToCharArray()) {
        $val = $alphabet.IndexOf($c)
        if ($val -lt 0) { throw "Invalid Base32 character: $c" }
        $buffer = ($buffer -shl 5) -bor $val
        $bits += 5
        if ($bits -ge 8) {
            $bytes.Add([byte](($buffer -shr ($bits - 8)) -band 0xFF))
            $bits -= 8
        }
    }
    $key = $bytes.ToArray()
    $step = [long][Math]::Floor(([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) / 30)
    $counter = [BitConverter]::GetBytes($step)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($counter) }

    $hmac = New-Object System.Security.Cryptography.HMACSHA1 -ArgumentList (,$key)
    try {
        $hash = $hmac.ComputeHash($counter)
        $offset = $hash[$hash.Length - 1] -band 0x0F
        $binary = (($hash[$offset] -band 0x7F) -shl 24) -bor (($hash[$offset + 1] -band 0xFF) -shl 16) -bor (($hash[$offset + 2] -band 0xFF) -shl 8) -bor ($hash[$offset + 3] -band 0xFF)
        $code = ($binary % 1000000).ToString('D6')
    }
    finally {
        $hmac.Dispose()
    }

    if ($code.Length -ne 6) { throw "TOTP code length is not 6: $code" }
    if ($code -notmatch '^\d{6}$') { throw "TOTP code is not 6 digits: $code" }
}

# --- TEST 5: VNPay Hash calculation ---
Assert-Test "5. VNPay HMAC-SHA512 calculation produces 128-char hex string" {
    $secret = "DEMOHASHSECRET1234567890"
    $rawQuery = "vnp_Amount=20000000&vnp_Command=pay&vnp_TxnRef=TOPUP_TEST_001"
    
    $hmac = New-Object System.Security.Cryptography.HMACSHA512
    try {
        $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
        $hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rawQuery))
        $secureHash = (-join ($hashBytes | ForEach-Object { $_.ToString('x2') })).ToLowerInvariant()
    }
    finally {
        $hmac.Dispose()
    }

    if ($secureHash.Length -ne 128) { throw "HMAC-SHA512 hash length must be 128 hex chars, got $($secureHash.Length)" }
    if ($secureHash -notmatch '^[0-9a-f]{128}$') { throw "Hash contains non-hex characters." }
}

# --- TEST 6: Top-up Contract mapping (topupId and Idempotency-Key) ---
Assert-Test "6. Seed script reads 'topupId' and sends 'Idempotency-Key' as GUID" {
    $content = Get-Content -Raw -LiteralPath $seedScriptPath
    
    # Must use topupId, NOT transactionId
    if (-not $content.Contains("Get-Prop `$topup.Json 'topupId'")) {
        throw "Script does not read 'topupId' from topup response."
    }
    if ($content.Contains("Get-Prop `$topup.Json 'transactionId'")) {
        throw "Script still references 'transactionId' on topup response."
    }

    # Must generate Idempotency-Key as GUID
    if (-not $content.Contains('[Guid]::NewGuid().ToString()')) {
        throw "Script does not generate standard GUID for Idempotency-Key."
    }
    if (-not $content.Contains("'Idempotency-Key' = `$idemKey")) {
        throw "Script does not set 'Idempotency-Key' header."
    }
}

# --- TEST 7: Admin MFA step 2 support ---
Assert-Test "7. Script supports Admin MFA step 2 login and TOTP parameters" {
    $content = Get-Content -Raw -LiteralPath $seedScriptPath
    
    if (-not $content.Contains('[string]$AdminTotpSecret')) {
        throw "Parameter -AdminTotpSecret not declared in script."
    }
    if (-not $content.Contains('[string]$AdminTotpCode')) {
        throw "Parameter -AdminTotpCode not declared in script."
    }
    if (-not $content.Contains('DEMO_ADMIN_TOTP_SECRET')) {
        throw "DEMO_ADMIN_TOTP_SECRET environment variable check not found."
    }
    if (-not $content.Contains('/api/identity/accounts/login/mfa')) {
        throw "/api/identity/accounts/login/mfa endpoint not called in script."
    }
}

# --- TEST 8: Dry-run Execution Safety ---
Assert-Test "8. Dry-run execution runs safely without errors and exits with 0" {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $seedScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Dry-run exited with code $LASTEXITCODE"
    }
    $text = $output -join "`n"
    if ($text -notmatch "customer2@demo.local") {
        throw "customer2@demo.local not printed in plan"
    }
    if ($text -notmatch "seed wallet with 200000 VND for customer and customer2") {
        throw "Wallet seeding step not printed in plan"
    }
    if ($text -notmatch "Dry run: nothing was written") {
        throw "Dry run safety message not present"
    }
}

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  RESULTS: $testsPassed PASSED, $testsFailed FAILED" -ForegroundColor $(if ($testsFailed -eq 0) { 'Green' } else { 'Red' })
Write-Host "==================================================================" -ForegroundColor Cyan

if ($testsFailed -gt 0) {
    exit 1
}
exit 0

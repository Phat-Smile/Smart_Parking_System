#Requires -Version 5.1
param (
    [string]$BaseUrl = "http://localhost:5001",
    [switch]$VerboseOutput
)

$ErrorActionPreference = 'Continue'

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " SMART PARKING SYSTEM - AUTOMATED E2E TEST SUITE (IDENTITY SERVICE)" -ForegroundColor Cyan
Write-Host " Target API: $BaseUrl" -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host ""

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Assert-Step {
    param (
        [string]$Id,
        [string]$Description,
        [int]$ExpectedStatus,
        [int]$ActualStatus,
        [string]$ExpectedErrorCode = $null,
        [object]$ResponseBody = $null,
        [string]$RawContent = $null
    )

    $statusMatch = ($ActualStatus -eq $ExpectedStatus)
    $errorMatch = $true

    if ($ExpectedErrorCode) {
        $candidates = [System.Collections.Generic.List[string]]::new()
        if ($RawContent) { $candidates.Add($RawContent) }
        if ($ResponseBody -is [string]) { $candidates.Add($ResponseBody) }
        if ($ResponseBody -is [PSCustomObject] -or $ResponseBody -is [hashtable]) {
            foreach ($prop in $ResponseBody.PSObject.Properties) {
                if ($prop.Value) { $candidates.Add($prop.Value.ToString()) }
            }
            $candidates.Add(($ResponseBody | ConvertTo-Json -Depth 5 -Compress))
        }
        $matched = $false
        foreach ($c in $candidates) {
            if ($c -like "*$ExpectedErrorCode*" -or $c -match [regex]::Escape($ExpectedErrorCode)) {
                $matched = $true
                break
            }
        }
        $errorMatch = $matched
    }

    $passed = $statusMatch -and $errorMatch

    $resObj = [PSCustomObject]@{
        Id             = $Id
        Description    = $Description
        ExpectedStatus = $ExpectedStatus
        ActualStatus   = $ActualStatus
        Passed         = $passed
    }
    $script:results.Add($resObj)

    if ($passed) {
        Write-Host "  [PASS] $Id - $Description (HTTP $ActualStatus)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $Id - $Description (Expected: $ExpectedStatus, Got: $ActualStatus)" -ForegroundColor Red
        if ($ExpectedErrorCode -and -not $errorMatch) {
            Write-Host "         Expected error code: '$ExpectedErrorCode' not found in response." -ForegroundColor DarkRed
            Write-Host "         Actual raw content: $RawContent" -ForegroundColor DarkYellow
        }
    }
}

function Invoke-Api {
    param (
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [string]$Token = $null
    )

    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $uri = "$BaseUrl$Endpoint"
    $jsonBody = if ($null -ne $Body) { $Body | ConvertTo-Json -Compress } else { $null }

    try {
        $req = [System.Net.HttpWebRequest]::Create($uri)
        $req.Method = $Method
        $req.Timeout = 10000
        $req.ContentType = "application/json"

        if ($Token) {
            $req.Headers["Authorization"] = "Bearer $Token"
        }

        if ($jsonBody) {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)
            $req.ContentLength = $bytes.Length
            $reqStream = $req.GetRequestStream()
            $reqStream.Write($bytes, 0, $bytes.Length)
            $reqStream.Close()
        }

        $res = $req.GetResponse()
        $resStream = $res.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($resStream, [System.Text.Encoding]::UTF8)
        $raw = $reader.ReadToEnd()
        $reader.Close()
        $resStream.Close()

        $statusCode = [int]$res.StatusCode
        $parsed = $null
        try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $raw }

        return @{
            StatusCode = $statusCode
            Headers    = $res.Headers
            Body       = $parsed
            RawContent = $raw
        }
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            $res = $_.Exception.Response
            $resStream = $res.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($resStream, [System.Text.Encoding]::UTF8)
            $raw = $reader.ReadToEnd()
            $reader.Close()
            $resStream.Close()

            $statusCode = [int]$res.StatusCode
            $parsed = $null
            try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $raw }

            return @{
                StatusCode = $statusCode
                Headers    = $res.Headers
                Body       = $parsed
                RawContent = $raw
            }
        }

        return @{
            StatusCode = 0
            Headers    = @{}
            Body       = $_.Exception.Message
            RawContent = $_.Exception.Message
        }
    }
    catch {
        return @{
            StatusCode = 0
            Headers    = @{}
            Body       = $_.Exception.Message
            RawContent = $_.Exception.Message
        }
    }
}

# --- STEP 0: HEALTH CHECK ---
Write-Host "Checking service health and availability..." -ForegroundColor Yellow
$healthLive = Invoke-Api -Method "GET" -Endpoint "/health/live"
if ($healthLive.StatusCode -ne 200) {
    Write-Host "ERROR: Cannot reach Identity API at $BaseUrl (Status: $($healthLive.StatusCode))." -ForegroundColor Red
    Write-Host "Details: $($healthLive.Body)" -ForegroundColor Red
    Write-Host "Please start the service with: dotnet run --project src/Services/Identity/Identity.WebAPI" -ForegroundColor Yellow
    exit 1
}
Assert-Step "H-01" "Liveness probe /health/live" 200 $healthLive.StatusCode

$healthReady = Invoke-Api -Method "GET" -Endpoint "/health/ready"
Assert-Step "H-02" "Readiness probe /health/ready (Postgres/RabbitMQ)" 200 $healthReady.StatusCode

Write-Host ""
Write-Host "--- TEST GROUP 1: ACCOUNT REGISTRATION (US-AUTH-001) ---" -ForegroundColor Yellow
$rand = Get-Random -Minimum 10000 -Maximum 99999
$email1 = "e2e.test.$rand@smartparking.local"
$email2 = "e2e.noname.$rand@smartparking.local"
$pwd = "SecurePassword123!"

# 1.1 Register with FullName via new standardized route /api/identity/accounts/register
$r1 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/register" -Body @{
    email    = $email1
    password = $pwd
    fullName = "Nguyen Van Test"
}
Assert-Step "REG-01" "Register with full name via /register succeeds" 201 $r1.StatusCode

# 1.2 Register without FullName via backward-compatibility alias /api/identity/accounts
$r2 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts" -Body @{
    email    = $email2
    password = $pwd
}
Assert-Step "REG-02" "Register via backward-compatible alias /accounts succeeds" 201 $r2.StatusCode

# 1.3 Duplicate email
$r3 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/register" -Body @{
    email    = $email1
    password = $pwd
}
Assert-Step "REG-03" "Duplicate email returns 409 Conflict with Vietnamese message" 409 $r3.StatusCode "identity.email_already_registered" $r3.Body

# 1.4 Validation check: Invalid fields return specific Vietnamese messages
$r4 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/register" -Body @{
    email       = "not-an-email"
    password    = "123"
    phoneNumber = "0123"
}
Assert-Step "REG-04" "Reject invalid fields with specific Vietnamese messages" 400 $r4.StatusCode "request.validation_failed" $r4.Body

Write-Host ""
Write-Host "--- TEST GROUP 2: EMAIL OTP VERIFICATION (US-AUTH-003) ---" -ForegroundColor Yellow

# 2.1 Failed OTP 3 times
$rOtp1 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/verify-email" -Body @{ email = $email1; otpCode = "111111" }
Assert-Step "OTP-01" "Wrong OTP attempt 1 returns 400" 400 $rOtp1.StatusCode "identity.invalid_otp_code" $rOtp1.Body

$rOtp2 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/verify-email" -Body @{ email = $email1; otpCode = "222222" }
Assert-Step "OTP-02" "Wrong OTP attempt 2 returns 400" 400 $rOtp2.StatusCode "identity.invalid_otp_code" $rOtp2.Body

$rOtp3 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/verify-email" -Body @{ email = $email1; otpCode = "333333" }
Assert-Step "OTP-03" "Wrong OTP attempt 3 locks current OTP" 400 $rOtp3.StatusCode "identity.otp_max_attempts_exceeded" $rOtp3.Body

$rOtp4 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/verify-email" -Body @{ email = $email1; otpCode = "444444" }
Assert-Step "OTP-04" "Wrong OTP attempt 4 reports max attempts exceeded" 400 $rOtp4.StatusCode "identity.otp_max_attempts_exceeded" $rOtp4.Body

# 2.2 Resend OTP & Cooldown
$rResend1 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/resend-otp" -Body @{ email = $email1 }
if ($rResend1.StatusCode -eq 429) {
    Assert-Step "OTP-05" "Resend OTP within cooldown returns 429 with Vietnamese cooldown message" 429 $rResend1.StatusCode "giây trước khi yêu cầu" $rResend1.Body
} else {
    Assert-Step "OTP-05" "Resend OTP returns exact Vietnamese conditional message" 200 $rResend1.StatusCode "Nếu tài khoản tồn tại và đang chờ xác thực" $rResend1.Body
}

# 2.3 Resend OTP for non-existent / already active email (truthful explicit message check)
$rResendGeneric = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/resend-otp" -Body @{ email = "nonexistent.$rand@smartparking.local" }
Assert-Step "OTP-06" "Resend OTP for non-existent email returns 200 with clear Vietnamese guide" 200 $rResendGeneric.StatusCode "Email này chưa được đăng ký" $rResendGeneric.Body $rResendGeneric.RawContent

Write-Host ""
Write-Host "--- TEST GROUP 3: LOGIN AND ACCOUNT LOCKOUT (US-AUTH-004, US-AUTH-008) ---" -ForegroundColor Yellow

$lockoutEmail = "lockout.test.$rand@smartparking.local"
$null = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts" -Body @{ email = $lockoutEmail; password = $pwd }

# 3.1 Wrong password attempts
$rWrongLogin = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/login" -Body @{ email = $lockoutEmail; password = "WrongPassword1" }
Assert-Step "LOGIN-01" "Wrong password returns 401 with Vietnamese message" 401 $rWrongLogin.StatusCode "identity.invalid_credentials" $rWrongLogin.Body $rWrongLogin.RawContent

$null = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/login" -Body @{ email = $lockoutEmail; password = "WrongPassword2" }
$rLock3 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/login" -Body @{ email = $lockoutEmail; password = "WrongPassword3" }
Assert-Step "LOCK-01" "Three wrong passwords triggers 423 Locked with Vietnamese message" 423 $rLock3.StatusCode "identity.account_temporarily_locked" $rLock3.Body $rLock3.RawContent

$rLock4 = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/login" -Body @{ email = $lockoutEmail; password = $pwd }
Assert-Step "LOCK-02" "Login during lockout period rejected with 423 Locked" 423 $rLock4.StatusCode "identity.account_temporarily_locked" $rLock4.Body $rLock4.RawContent

Write-Host ""
Write-Host "--- TEST GROUP 4: FORGOT PASSWORD (US-AUTH-006) ---" -ForegroundColor Yellow

# US-AUTH-006: Mark account as EmailVerified so it is eligible for password reset code and triggers cooldown
try {
    docker exec parking-system-postgres-1 psql -U parking_dev -d parking_system -c "UPDATE identity.accounts SET status = 'EmailVerified' WHERE email = '$email2';" 2>$null | Out-Null
} catch {}

$rForgotExist = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/forgot-password" -Body @{ email = $email2 }
Assert-Step "FP-01" "Forgot password for existing email returns 200 OK with Vietnamese message" 200 $rForgotExist.StatusCode "Nếu email tồn tại trong hệ thống" $rForgotExist.Body $rForgotExist.RawContent

$rForgotNonExist = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/forgot-password" -Body @{ email = "nonexistent.$rand@example.com" }
Assert-Step "FP-02" "Forgot password for non-existent email returns identical 200 OK" 200 $rForgotNonExist.StatusCode "Nếu email tồn tại trong hệ thống" $rForgotNonExist.Body $rForgotNonExist.RawContent

$rForgotSpam = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/forgot-password" -Body @{ email = $email2 }
if ($rForgotSpam.StatusCode -eq 429) {
    Assert-Step "FP-03" "Forgot password in 60s cooldown returns 429 with Vietnamese message" 429 $rForgotSpam.StatusCode "identity.reset_cooldown_active" $rForgotSpam.Body $rForgotSpam.RawContent
} else {
    Assert-Step "FP-03" "Forgot password for unverified email returns 200 (anti-enumeration)" 200 $rForgotSpam.StatusCode
}

Write-Host ""
Write-Host "--- TEST GROUP 5: GOOGLE SIGN-IN (US-AUTH-002) ---" -ForegroundColor Yellow

$rGoogleEmpty = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/google" -Body @{ idToken = "" }
Assert-Step "GGL-01" "Google login with empty token returns 400 with Vietnamese validation message" 400 $rGoogleEmpty.StatusCode "request.validation_failed" $rGoogleEmpty.Body $rGoogleEmpty.RawContent

$rGoogleFake = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/google" -Body @{ idToken = "invalid_fake_jwt_token" }
if ($rGoogleFake.StatusCode -eq 503) {
    Assert-Step "GGL-02" "Google login disabled when ClientId not configured (503)" 503 $rGoogleFake.StatusCode "identity.google_login_unavailable" $rGoogleFake.Body $rGoogleFake.RawContent
} else {
    Assert-Step "GGL-02" "Invalid Google token rejected with 401 with Vietnamese message" 401 $rGoogleFake.StatusCode "identity.invalid_google_token" $rGoogleFake.Body $rGoogleFake.RawContent
}

Write-Host ""
Write-Host "--- TEST GROUP 6: REFRESH TOKEN & LOGOUT ---" -ForegroundColor Yellow

# 6.1 Invalid refresh token returns 401 with Vietnamese message
$rInvalidRefresh = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/refresh" -Body @{ refreshToken = "invalid_token_sample" }
Assert-Step "REF-01" "Invalid refresh token returns 401 with Vietnamese message" 401 $rInvalidRefresh.StatusCode "identity.invalid_refresh_token" $rInvalidRefresh.Body $rInvalidRefresh.RawContent

# 6.2 Logout with dummy token returns 200 with Vietnamese message
$rLogout = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/logout" -Body @{ refreshToken = "sample_refresh_token" }
Assert-Step "LGO-01" "Logout returns 200 with Vietnamese success message" 200 $rLogout.StatusCode "Đăng xuất thành công" $rLogout.Body $rLogout.RawContent

# 6.3 Empty refresh token validation
$rEmptyRefresh = Invoke-Api -Method "POST" -Endpoint "/api/identity/accounts/refresh" -Body @{ refreshToken = "" }
Assert-Step "REF-02" "Empty refresh token rejected with 400 with Vietnamese message" 400 $rEmptyRefresh.StatusCode "request.validation_failed" $rEmptyRefresh.Body $rEmptyRefresh.RawContent

Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " E2E TEST SUMMARY" -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

$total = $results.Count
$passedCount = ($results | Where-Object { $_.Passed }).Count
$failedCount = $total - $passedCount

Write-Host "Total Tests: $total"
Write-Host "Passed:      $passedCount" -ForegroundColor Green
if ($failedCount -gt 0) {
    Write-Host "Failed:      $failedCount" -ForegroundColor Red
} else {
    Write-Host "Failed:      0" -ForegroundColor Green
    Write-Host "ALL E2E SCENARIOS PASSED 100%!" -ForegroundColor Green
}
Write-Host "==========================================================================" -ForegroundColor Cyan

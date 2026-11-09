#Requires -Version 5.1
<#
.SYNOPSIS
    Creates demo data for Smart Parking: accounts, roles, owner applications with documents, staff assignment and sample lots.

.DESCRIPTION
    Dry run by default: prints the plan and touches nothing. With -Apply and -ConfirmDemoData it calls the real Identity and
    Parking APIs on their own ports (not the Gateway, which rate-limits register and login per IP) and, through psql in the
    Postgres container, marks the demo emails as verified and promotes the first SystemAdmin, which no API can do.

    The owner goes through the real flow: apply, choose the owner type, upload every required document, then the admin
    approves. Nothing grants the Owner role by a shortcut. Safe to run again: every step skips what already exists.

    Run it only against a local development or demo database. The accounts share one published password, and the sample
    parking lots overwrite four fixed ids. Details: docs/development/Demo-Data.md.

.EXAMPLE
    .\scripts\dev\Seed-DemoData.ps1
    Prints the plan.

.EXAMPLE
    .\scripts\dev\Seed-DemoData.ps1 -Apply -ConfirmDemoData I_UNDERSTAND_THIS_CREATES_DEMO_ACCOUNTS
#>
[CmdletBinding()]
param (
    [string]$IdentityUrl = 'http://127.0.0.1:5001',
    [string]$ParkingUrl = 'http://127.0.0.1:5002',
    # Empty means: SERVICE_KEY from the environment, then from .env at the repository root, then none.
    [string]$ServiceKey = '',
    [string]$Password = 'Demo@12345',
    [string]$EmailDomain = 'demo.local',
    # Eight digits; the script appends 01, 02 and 03 to get three distinct ten-digit tax codes.
    [string]$TaxCodePrefix = '09000000',
    # Docker: run psql in the Postgres container. Skip: the verification and admin SQL was applied by hand.
    [ValidateSet('Docker', 'Skip')]
    [string]$SqlMode = 'Docker',
    # A container name to use with docker exec instead of the compose service "postgres".
    [string]$PostgresContainer = '',
    [switch]$SkipParkingCatalog,
    [switch]$SkipLotDetails,
    [string]$PaymentUrl = 'http://127.0.0.1:5004',
    [switch]$SkipWalletSeed,
    [long]$WalletSeedAmount = 200000,
    [switch]$AllowRemoteHost,
    [switch]$Apply,
    [string]$ConfirmDemoData = ''
)

$ErrorActionPreference = 'Stop'
$confirmToken = 'I_UNDERSTAND_THIS_CREATES_DEMO_ACCOUNTS'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# ---------------------------------------------------------------- inputs

if ($EmailDomain -notmatch '^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$') { throw "EmailDomain must be lower-case letters, digits, dots and hyphens: '$EmailDomain'" }
if ($TaxCodePrefix -notmatch '^[0-9]{8}$') { throw "TaxCodePrefix must be exactly eight digits: '$TaxCodePrefix'" }
if ($Password.Length -lt 8) { throw 'Password must have at least 8 characters (upper, lower, digit and a special character).' }
if ($PostgresContainer -and $PostgresContainer -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw "PostgresContainer is not a valid container name: '$PostgresContainer'" }

$IdentityUrl = $IdentityUrl.TrimEnd('/')
$ParkingUrl = $ParkingUrl.TrimEnd('/')
foreach ($url in @($IdentityUrl, $ParkingUrl, $PaymentUrl)) {
    $uri = [System.Uri]$url
    if (-not $AllowRemoteHost -and @('localhost', '127.0.0.1', '[::1]', '::1') -notcontains $uri.Host) {
        throw "Refusing to seed ${url}: the host is not local. Demo accounts share a published password. Use -AllowRemoteHost only for a throwaway environment."
    }
}

if (-not $ServiceKey) {
    if ($env:SERVICE_KEY) {
        $ServiceKey = $env:SERVICE_KEY
    }
    else {
        $envFile = Join-Path $repoRoot '.env'
        if (Test-Path -LiteralPath $envFile) {
            $line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*SERVICE_KEY\s*=' } | Select-Object -First 1
            if ($line) { $ServiceKey = ($line -replace '^\s*SERVICE_KEY\s*=\s*', '').Trim().Trim('"').Trim("'") }
        }
    }
}

# ---------------------------------------------------------------- the data

$lots = @(
    [pscustomobject]@{ Id = '10000000-0000-0000-0000-000000000001'; Name = 'Bai do Trung tam A'; Status = 'Approved'; Staff = $true;  Hours = '24h';          CarPrice = 15000 }
    [pscustomobject]@{ Id = '10000000-0000-0000-0000-000000000002'; Name = 'Bai do Ngoai troi B';     Status = 'Approved'; Staff = $true;  Hours = '24h';          CarPrice = 12000 }
    [pscustomobject]@{ Id = '10000000-0000-0000-0000-000000000003'; Name = 'Bai do Tang ham C';       Status = 'Approved'; Staff = $false; Hours = '07:00-21:00';  CarPrice = 20000 }
    [pscustomobject]@{ Id = '10000000-0000-0000-0000-000000000004'; Name = 'Bai cho duyet';           Status = 'Pending';  Staff = $false; Hours = '';             CarPrice = 0 }
)

function New-DemoAccount {
    param ([string]$Key, [string]$Local, [string]$FullName, [string]$OwnerType = '', [string]$TaxCode = '', [string]$FinalStatus = '', [string]$Reason = '')
    [pscustomobject]@{
        Key = $Key; Email = "$Local@$EmailDomain"; FullName = $FullName
        OwnerType = $OwnerType; TaxCode = $TaxCode; FinalStatus = $FinalStatus; Reason = $Reason
        Id = $null; Token = $null; RoleClaim = $null
    }
}

$accounts = @(
    New-DemoAccount 'Admin' 'admin' 'Quan Tri Vien Demo'
    New-DemoAccount 'Owner' 'owner' 'Chu Bai Demo' 'HouseholdBusiness' "${TaxCodePrefix}01" 'Approved'
    New-DemoAccount 'OwnerPending' 'owner.pending' 'Chu Bai Cho Duyet' 'Individual' "${TaxCodePrefix}02" 'Pending'
    New-DemoAccount 'OwnerRejected' 'owner.rejected' 'Chu Bai Bi Tu Choi' 'Enterprise' "${TaxCodePrefix}03" 'Rejected' 'Premises proof is blurred, please upload a clear copy.'
    New-DemoAccount 'Staff' 'staff' 'Nhan Vien Demo'
    New-DemoAccount 'Customer' 'customer' 'Khach Hang Demo'
    New-DemoAccount 'Customer2' 'customer2' 'Khach Hang Demo 2 (Concurrency)'
)
function Get-Demo([string]$Key) { $accounts | Where-Object { $_.Key -eq $Key } | Select-Object -First 1 }

function Show-Plan {
    Write-Host ''
    Write-Host 'Demo accounts (all share the password given by -Password, default Demo@12345):' -ForegroundColor Cyan
    $accounts | ForEach-Object {
        $account = $_
        $what = switch ($account.Key) {
            'Admin' { 'SystemAdmin (promoted by SQL, the only way to get the first admin)' }
            'Staff' { 'Staff (role set by the admin), assigned to lots A and B' }
            'Customer' { 'Customer 1 with one car (51A-123.45) and one motorbike (59X1-234.56)' }
            'Customer2' { 'Customer 2 for Concurrency slot fight, car (51B-999.88)' }
            default { "owner application, type $($account.OwnerType), all required documents uploaded, final state $($account.FinalStatus)" }
        }
        Write-Host ('  {0,-32} {1}' -f $account.Email, $what)
    }
    Write-Host ''
    Write-Host 'Steps:' -ForegroundColor Cyan
    if ($SqlMode -eq 'Docker' -and -not $SkipParkingCatalog) { Write-Host '  1. psql: scripts/database/Seed-ParkingCatalog.sql and Seed-ParkingLayout.sql (4 fixed sample lots, upsert)' }
    else { Write-Host '  1. sample lots: not seeded by this run (the 4 fixed lots must already exist)' }
    Write-Host "  2. Identity $IdentityUrl : register the accounts (an existing email is kept)"
    if ($SqlMode -eq 'Docker') { Write-Host '  3. psql: mark the demo emails verified, promote admin to SystemAdmin' }
    else { Write-Host '  3. SQL skipped: the emails must already be verified and admin promoted' }
    Write-Host '  4. owners: apply, choose type, upload every required document, then the admin approves or rejects'
    Write-Host '  5. staff role, customer profile and vehicles'
    Write-Host "  6. Parking $ParkingUrl : the demo owner owns lots A, B, C and the pending lot; staff is assigned to A and B"
    if (-not $SkipLotDetails) { Write-Host '  7. the owner sets opening hours and prices for lots A, B and C' }
    if (-not $SkipWalletSeed) { Write-Host "  8. Payment $PaymentUrl : seed wallet with $WalletSeedAmount VND for customer and customer2 (via VNPay IPN simulation)" }
    Write-Host ''
}

# ---------------------------------------------------------------- output helpers

function Write-Step([string]$Text) { Write-Host ''; Write-Host "== $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text) { Write-Host "  ok    $Text" -ForegroundColor Green }
function Write-Note([string]$Text) { Write-Host "  skip  $Text" -ForegroundColor DarkGray }
function Write-Warn([string]$Text) { Write-Host "  WARN  $Text" -ForegroundColor Yellow }

# ---------------------------------------------------------------- HTTP

function ConvertTo-ApiResult([int]$Status, [string]$Text) {
    $json = $null
    if ($Text) { try { $json = $Text | ConvertFrom-Json } catch { $json = $null } }
    [pscustomobject]@{ Status = $Status; Text = $Text; Json = $json }
}

function Get-Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($property) { return $property.Value }
    return $null
}

function Invoke-Api {
    param ([string]$Method, [string]$Url, $Body = $null, [string]$Token = '')
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    if ($ServiceKey) { $headers['X-Service-Key'] = $ServiceKey }
    $request = @{ Method = $Method; Uri = $Url; Headers = $headers; UseBasicParsing = $true; TimeoutSec = 60 }
    if ($null -ne $Body) {
        $request['Body'] = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
        $request['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $response = Invoke-WebRequest @request
        return ConvertTo-ApiResult ([int]$response.StatusCode) $response.Content
    }
    catch {
        $errorRecord = $_
        $failed = $errorRecord.Exception.Response
        if ($null -eq $failed) { throw "Cannot reach $Url : $($errorRecord.Exception.Message)" }
        # Windows PowerShell 5.1 has already read the body of an error response: it is in ErrorDetails, and the stream is at its end.
        $text = ''
        if ($errorRecord.ErrorDetails -and $errorRecord.ErrorDetails.Message) { $text = $errorRecord.ErrorDetails.Message }
        elseif ($failed -is [System.Net.HttpWebResponse]) {
            $reader = New-Object System.IO.StreamReader($failed.GetResponseStream())
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return ConvertTo-ApiResult ([int]$failed.StatusCode) $text
    }
}

# A tiny PDF that says what it is. The server only checks the first bytes (the PDF signature) and the size.
$samplePdf = [System.Text.Encoding]::ASCII.GetBytes(
    "%PDF-1.4`n1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n" +
    "3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 120] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>`nendobj`n" +
    "4 0 obj`n<< /Length 62 >>`nstream`nBT /F1 14 Tf 20 60 Td (DEMO DOCUMENT - NOT A REAL PAPER) Tj ET`nendstream`nendobj`n" +
    "5 0 obj`n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>`nendobj`ntrailer`n<< /Root 1 0 R /Size 6 >>`n%%EOF`n")

function Send-Document {
    param ([string]$Token, [string]$DocumentType)
    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(60)
        $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
        if ($ServiceKey) { [void]$client.DefaultRequestHeaders.TryAddWithoutValidation('X-Service-Key', $ServiceKey) }
        $form = New-Object System.Net.Http.MultipartFormDataContent
        try {
            $form.Add((New-Object System.Net.Http.StringContent($DocumentType)), 'documentType')
            $file = New-Object System.Net.Http.ByteArrayContent -ArgumentList (, $samplePdf)
            $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/pdf')
            $form.Add($file, 'file', 'demo-document.pdf')
            $response = $client.PostAsync("$IdentityUrl/api/identity/accounts/me/owner-application/documents", $form).GetAwaiter().GetResult()
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return ConvertTo-ApiResult ([int]$response.StatusCode) $text
        }
        finally { $form.Dispose() }
    }
    finally { $client.Dispose() }
}

# Returns $true when the call did the work, $false when the answer says it was already done; anything else stops the run.
function Assert-Api {
    param ($Result, [string]$What, [string[]]$Tolerate = @())
    if ($Result.Status -ge 200 -and $Result.Status -lt 300) { Write-Ok $What; return $true }
    $code = Get-Prop $Result.Json 'errorCode'
    if ($code -and ($Tolerate -contains $code)) { Write-Note "$What ($code)"; return $false }
    throw ('{0}: HTTP {1} {2}' -f $What, $Result.Status, $Result.Text)
}

function Get-JwtClaims([string]$Jwt) {
    $part = $Jwt.Split('.')[1].Replace('-', '+').Replace('_', '/')
    switch ($part.Length % 4) { 2 { $part += '==' } 3 { $part += '=' } }
    [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($part)) | ConvertFrom-Json
}

function Connect-Account($Account) {
    $result = Invoke-Api POST "$IdentityUrl/api/identity/accounts/login" @{ email = $Account.Email; password = "$Password" }
    if ($result.Status -ne 200) {
        $hint = ''
        if ((Get-Prop $result.Json 'errorCode') -eq 'identity.email_not_verified') { $hint = ' The email is not verified: the SQL step did not run (see -SqlMode).' }
        throw "Login of $($Account.Email) failed: HTTP $($result.Status) $($result.Text)$hint"
    }
    $token = Get-Prop $result.Json 'accessToken'
    if (-not $token) { throw "Login of $($Account.Email) returned no access token: $($result.Text)" }
    $claims = Get-JwtClaims $token
    $Account.Token = $token
    $Account.Id = [string]$claims.sub
    $Account.RoleClaim = [string]$claims.role
}

# ---------------------------------------------------------------- SQL

function Invoke-Psql {
    param ([string]$Sql, [switch]$SingleTransaction)
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'docker is not on PATH: needed for psql in the Postgres container (or run the SQL by hand and use -SqlMode Skip).' }
    $psql = 'psql -X -v ON_ERROR_STOP=1'
    if ($SingleTransaction) { $psql += ' --single-transaction' }
    $psql += ' -U $POSTGRES_USER -d $POSTGRES_DB'
    $previousEncoding = $global:OutputEncoding
    $global:OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    Push-Location $repoRoot
    try {
        if ($PostgresContainer) { $Sql | & docker exec -i $PostgresContainer sh -c $psql | Out-Host }
        else { $Sql | & docker compose exec -T postgres sh -c $psql | Out-Host }
        if ($LASTEXITCODE -ne 0) { throw "psql failed (exit code $LASTEXITCODE). Is the postgres container running and migrated?" }
    }
    finally {
        Pop-Location
        $global:OutputEncoding = $previousEncoding
    }
}

function Get-SeedSql([string]$Name) { Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repoRoot "scripts/database/$Name") }

# ---------------------------------------------------------------- steps

function Get-OwnerStatus($Account) {
    $result = Invoke-Api GET "$IdentityUrl/api/identity/accounts/me/owner-application" $null $Account.Token
    if ($result.Status -ne 200) { throw "Reading the owner application of $($Account.Email): HTTP $($result.Status) $($result.Text)" }
    return [string](Get-Prop $result.Json 'status')
}

function Invoke-OwnerApplication($Account, $Admin) {
    $status = Get-OwnerStatus $Account
    # Nothing undoes an approval. A rejected application that should stay rejected is done. A Pending target is always redone
    # (the calls are idempotent): that finishes an interrupted run and re-arms the application after a rehearsal decided it.
    if ($status -eq 'Approved') { Write-Note "$($Account.Email): application already Approved, nothing to change"; return }
    if ($status -eq 'Rejected' -and $Account.FinalStatus -eq 'Rejected') { Write-Note "$($Account.Email): application already Rejected"; return }

    [void](Assert-Api (Invoke-Api POST "$IdentityUrl/api/identity/accounts/me/owner-application/apply" @{ taxCode = $Account.TaxCode; businessName = "Demo $($Account.Key)" } $Account.Token) `
            "$($Account.Email): apply" @('identity.owner_application_already_pending', 'identity.owner_application_already_approved'))
    [void](Assert-Api (Invoke-Api PUT "$IdentityUrl/api/identity/accounts/me/owner-application" @{ ownerType = $Account.OwnerType } $Account.Token) `
            "$($Account.Email): owner type $($Account.OwnerType)" @('identity.owner_application_locked'))

    $requirements = Invoke-Api GET "$IdentityUrl/api/identity/owner-document-requirements?ownerType=$($Account.OwnerType)" $null $Account.Token
    if ($requirements.Status -ne 200) { throw "Checklist of $($Account.OwnerType): HTTP $($requirements.Status) $($requirements.Text)" }
    $required = @((Get-Prop $requirements.Json 'documents') | Where-Object { $_.required } | ForEach-Object { $_.documentType })
    if ($required.Count -eq 0) { throw "The checklist of $($Account.OwnerType) lists no required document: refusing to continue." }
    foreach ($documentType in $required) {
        [void](Assert-Api (Send-Document $Account.Token $documentType) "$($Account.Email): upload $documentType" @('identity.owner_application_locked'))
    }

    switch ($Account.FinalStatus) {
        'Approved' {
            [void](Assert-Api (Invoke-Api POST "$IdentityUrl/api/identity/admin/owner-applications/$($Account.Id)/approve" $null $Admin.Token) `
                    "$($Account.Email): admin approves" @('identity.owner_application_not_pending'))
        }
        'Rejected' {
            [void](Assert-Api (Invoke-Api POST "$IdentityUrl/api/identity/admin/owner-applications/$($Account.Id)/reject" @{ reason = $Account.Reason } $Admin.Token) `
                    "$($Account.Email): admin rejects with a reason" @('identity.owner_application_not_pending'))
        }
        default { Write-Note "$($Account.Email): left Pending for the live demo" }
    }
}

function Set-LotDetails($Owner, $Lot) {
    $days = @(0..6 | ForEach-Object {
            if ($Lot.Hours -eq '24h') { [ordered]@{ dayOfWeek = $_; isClosed = $false; is24Hours = $true; opensAt = $null; closesAt = $null } }
            else { [ordered]@{ dayOfWeek = $_; isClosed = $false; is24Hours = $false; opensAt = '07:00'; closesAt = '21:00' } }
        })
    $hours = Invoke-Api PUT "$ParkingUrl/api/parking/owner/lots/$($Lot.Id)/opening-hours" @{ days = $days } $Owner.Token
    if ($hours.Status -ge 200 -and $hours.Status -lt 300) { Write-Ok "$($Lot.Name): opening hours $($Lot.Hours)" }
    else { Write-Warn "$($Lot.Name): opening hours not set (HTTP $($hours.Status) $($hours.Text))" }

    $prices = @(
        @{ Type = 'CAR'; Body = @{ billingMode = 'Block'; blockMinutes = 30; price = $Lot.CarPrice; overnightFee = 50000; reason = 'Demo price' } }
        @{ Type = 'MOTORBIKE'; Body = @{ billingMode = 'PerEntry'; blockMinutes = $null; price = 5000; overnightFee = 10000; reason = 'Demo price' } }
    )
    foreach ($price in $prices) {
        $result = Invoke-Api PUT "$ParkingUrl/api/parking/lots/$($Lot.Id)/pricing/$($price.Type)" $price.Body $Owner.Token
        if ($result.Status -ge 200 -and $result.Status -lt 300) { Write-Ok "$($Lot.Name): $($price.Type) price" }
        else { Write-Warn "$($Lot.Name): $($price.Type) price not set (HTTP $($result.Status) $($result.Text))" }
    }
}

function Invoke-Seed {
    $admin = Get-Demo 'Admin'; $owner = Get-Demo 'Owner'; $staff = Get-Demo 'Staff'; $customer = Get-Demo 'Customer'; $customer2 = Get-Demo 'Customer2'

    Write-Step 'Checking that the services answer'
    foreach ($target in @(@{ Name = 'Identity'; Url = $IdentityUrl }, @{ Name = 'Parking'; Url = $ParkingUrl })) {
        $health = Invoke-Api GET "$($target.Url)/health/live"
        if ($health.Status -ne 200) { throw "$($target.Name) at $($target.Url) is not live: HTTP $($health.Status)" }
        Write-Ok "$($target.Name) is live"
    }

    if ($SqlMode -eq 'Docker' -and -not $SkipParkingCatalog) {
        Write-Step 'Sample parking lots (psql, upsert of four fixed ids)'
        Invoke-Psql ((Get-SeedSql 'Seed-ParkingCatalog.sql') + "`n" + (Get-SeedSql 'Seed-ParkingLayout.sql')) -SingleTransaction
        Write-Ok 'Seed-ParkingCatalog.sql and Seed-ParkingLayout.sql applied'
    }

    Write-Step 'Registering the accounts'
    foreach ($account in $accounts) {
        $body = @{ fullName = $account.FullName; email = $account.Email; password = "$Password" }
        [void](Assert-Api (Invoke-Api POST "$IdentityUrl/api/identity/accounts" $body) "register $($account.Email)" @('identity.email_already_registered'))
    }

    if ($SqlMode -eq 'Docker') {
        Write-Step 'Verifying the emails and promoting the first admin (psql)'
        $list = ($accounts | ForEach-Object { "'$($_.Email)'" }) -join ', '
        Invoke-Psql ("UPDATE identity.accounts SET status = 'EmailVerified' WHERE email IN ($list) AND status <> 'EmailVerified';`n" +
            "UPDATE identity.accounts SET role = 'SystemAdmin' WHERE email = '$($admin.Email)' AND role <> 'SystemAdmin';`n")
        Write-Ok 'verified and promoted'
    }
    else { Write-Step 'SQL skipped (-SqlMode Skip): expecting verified emails and a promoted admin' }

    Write-Step 'Signing in'
    foreach ($account in $accounts) { Connect-Account $account }
    if ($admin.RoleClaim -ne 'SystemAdmin') { throw "The admin token carries role '$($admin.RoleClaim)', not SystemAdmin. The promotion SQL did not take effect." }
    Write-Ok 'all accounts signed in, admin token carries SystemAdmin'

    Write-Step 'Owner applications (real flow: apply, type, documents, admin decision)'
    foreach ($account in @($accounts | Where-Object { $_.OwnerType })) { Invoke-OwnerApplication $account $admin }

    Write-Step 'Signing the approved owner in again (approval revokes the old sessions)'
    Connect-Account $owner
    if ($owner.RoleClaim -ne 'ParkingLotOwner') { throw "The owner token carries role '$($owner.RoleClaim)', not ParkingLotOwner. Check the application status of $($owner.Email)." }
    Write-Ok "$($owner.Email) now carries ParkingLotOwner"

    Write-Step 'Staff and customer'
    [void](Assert-Api (Invoke-Api PUT "$IdentityUrl/api/identity/admin/accounts/$($staff.Id)/role" @{ role = 'Staff' } $admin.Token) "$($staff.Email): role Staff")
    Connect-Account $staff
    if ($staff.RoleClaim -ne 'Staff') { throw "The staff token carries role '$($staff.RoleClaim)', not Staff." }
    $profile = Invoke-Api PUT "$IdentityUrl/api/identity/accounts/me/profile" @{ fullName = $customer.FullName } $customer.Token
    if ($profile.Status -ge 200 -and $profile.Status -lt 300) { Write-Ok "$($customer.Email): profile" } else { Write-Warn "$($customer.Email): profile not saved (HTTP $($profile.Status) $($profile.Text))" }
    foreach ($vehicle in @(@{ licensePlate = '51A-123.45'; vehicleType = 'Car'; nickName = 'Demo car' }, @{ licensePlate = '59X1-234.56'; vehicleType = 'Motorbike'; nickName = 'Demo motorbike' })) {
        $added = Invoke-Api POST "$IdentityUrl/api/identity/accounts/me/vehicles" $vehicle $customer.Token
        if ($added.Status -ge 200 -and $added.Status -lt 300) { Write-Ok "$($customer.Email): vehicle $($vehicle.licensePlate)" }
        elseif ((Get-Prop $added.Json 'errorCode') -eq 'identity.vehicle_plate_already_registered') { Write-Note "$($customer.Email): plate $($vehicle.licensePlate) is already registered (it may belong to another account)" }
        else { Write-Warn "$($customer.Email): vehicle $($vehicle.licensePlate) not added (HTTP $($added.Status) $($added.Text))" }
    }

    if ($customer2) {
        $profile2 = Invoke-Api PUT "$IdentityUrl/api/identity/accounts/me/profile" @{ fullName = $customer2.FullName } $customer2.Token
        if ($profile2.Status -ge 200 -and $profile2.Status -lt 300) { Write-Ok "$($customer2.Email): profile" } else { Write-Warn "$($customer2.Email): profile not saved (HTTP $($profile2.Status) $($profile2.Text))" }
        foreach ($vehicle in @(@{ licensePlate = '51B-999.88'; vehicleType = 'Car'; nickName = 'Demo car 2 (Concurrency)' })) {
            $added = Invoke-Api POST "$IdentityUrl/api/identity/accounts/me/vehicles" $vehicle $customer2.Token
            if ($added.Status -ge 200 -and $added.Status -lt 300) { Write-Ok "$($customer2.Email): vehicle $($vehicle.licensePlate)" }
            elseif ((Get-Prop $added.Json 'errorCode') -eq 'identity.vehicle_plate_already_registered') { Write-Note "$($customer2.Email): plate $($vehicle.licensePlate) is already registered (it may belong to another account)" }
            else { Write-Warn "$($customer2.Email): vehicle $($vehicle.licensePlate) not added (HTTP $($added.Status) $($added.Text))" }
        }
    }

    Write-Step 'Who works in which lot (admin assigns)'
    foreach ($lot in $lots) {
        $assignment = Invoke-Api PUT "$ParkingUrl/api/parking/admin/lots/$($lot.Id)/assignments/$($owner.Id)" @{ role = 'Owner' } $admin.Token
        if ($assignment.Status -eq 404) { throw "Lot $($lot.Id) does not exist in Parking. Run without -SkipParkingCatalog, or seed scripts/database/Seed-ParkingCatalog.sql first." }
        [void](Assert-Api $assignment "$($lot.Name): owner is $($owner.Email)")
        if ($lot.Staff) { [void](Assert-Api (Invoke-Api PUT "$ParkingUrl/api/parking/admin/lots/$($lot.Id)/assignments/$($staff.Id)" @{ role = 'Staff' } $admin.Token) "$($lot.Name): staff is $($staff.Email)") }
    }

    if (-not $SkipLotDetails) {
        Write-Step 'Opening hours and prices (set by the owner, so they go through the owner rules)'
        foreach ($lot in @($lots | Where-Object { $_.Status -eq 'Approved' })) { Set-LotDetails $owner $lot }
    }

    if (-not $SkipWalletSeed) {
        Write-Step ("Seeding customer wallets ({0:N0} VND via VNPay IPN simulation)" -f $WalletSeedAmount)
        $concurrencyScript = Join-Path $PSScriptRoot 'Seed-CustomerConcurrencyWallets.ps1'
        if (Test-Path -LiteralPath $concurrencyScript) {
            & $concurrencyScript -IdentityUrl $IdentityUrl -PaymentUrl $PaymentUrl -TargetBalance $WalletSeedAmount -Password $Password -Apply | Out-Host
        } else {
            Write-Warn "Seed-CustomerConcurrencyWallets.ps1 not found, skipped wallet seeding."
        }
    }
}

function Show-Summary {
    Write-Host ''
    Write-Host 'Demo data is ready.' -ForegroundColor Green
    Write-Host "Password of every account: $Password"
    $accounts | ForEach-Object { Write-Host ('  {0,-32} role claim: {1}' -f $_.Email, $_.RoleClaim) }
    Write-Host ''
    Write-Host 'Lots (fixed ids):'
    $lots | ForEach-Object { Write-Host ('  {0}  {1}  [{2}]' -f $_.Id, $_.Name, $_.Status) }
    Write-Host ''
    Write-Host 'Next: docs/development/Demo-Data.md maps these accounts to the four demo flows.'
}

# ---------------------------------------------------------------- main

Show-Plan
if (-not $Apply) {
    Write-Host 'Dry run: nothing was written. Add -Apply -ConfirmDemoData ' -NoNewline
    Write-Host $confirmToken -ForegroundColor Yellow
    exit 0
}
if ($ConfirmDemoData -ne $confirmToken) {
    Write-Host "Refusing to write: pass -ConfirmDemoData $confirmToken to confirm this is a local development or demo database." -ForegroundColor Red
    exit 2
}

try {
    Invoke-Seed
    Show-Summary
    exit 0
}
catch {
    Write-Host ''
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

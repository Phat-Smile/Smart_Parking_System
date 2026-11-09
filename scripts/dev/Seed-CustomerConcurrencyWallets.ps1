#Requires -Version 5.1
<#
.SYNOPSIS
    Tien ich nap san so du vi (200.000 VND) va dang ky xe cho Customer 1 va Customer 2 phuc vu kich ban Concurrency dau coc slot cuoi cung.

.DESCRIPTION
    Script kiem tra va chuan bi day du du lieu cho kich ban Concurrency giu cho / dau coc:
      - Tai khoan 1: customer@demo.local (Xe 51A-123.45 / 59X1-234.56, So du vi >= 200.000 VND)
      - Tai khoan 2: customer2@demo.local (Xe 51B-999.88, So du vi >= 200.000 VND)
    
    Neu tai khoan hoac xe chua ton tai, script se dang ky va bo sung.
    Neu so du vi hien tai chua du muc yeu cau (mac dinh 200.000 VND), script se tao yeu cau top-up
    va gia lap webhook VNPay IPN voi chu ky so HMAC-SHA512 chuan xac de nap tien ngay lap tuc.

    Chay che do Dry-run mac dinh: in thong tin kiem tra va so du hien tai ma khong thay doi du lieu.
    Them tham so -Apply de thuc thi nap tien va ghi du lieu that.

.EXAMPLE
    .\scripts\dev\Seed-CustomerConcurrencyWallets.ps1
    In trang thai so du vi va du kien thao tac.

.EXAMPLE
    .\scripts\dev\Seed-CustomerConcurrencyWallets.ps1 -Apply
    Thuc thi nap so du vi len 200.000 VND cho ca hai tai khoan khach hang.

.EXAMPLE
    .\scripts\dev\Seed-CustomerConcurrencyWallets.ps1 -TargetBalance 500000 -Apply
    Nap so du vi len 500.000 VND.
#>
[CmdletBinding()]
param (
    [string]$IdentityUrl = 'http://127.0.0.1:5001',
    [string]$PaymentUrl = 'http://127.0.0.1:5004',
    [string]$GatewayUrl = 'http://127.0.0.1:5000',
    [string]$Password = 'Demo@12345',
    [long]$TargetBalance = 200000,
    [string]$HashSecret = '',
    [string]$TmnCode = '',
    [string]$ServiceKey = '',
    [ValidateSet('Docker', 'Skip')]
    [string]$SqlMode = 'Docker',
    [string]$PostgresContainer = '',
    [switch]$UseGateway,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# ---------------------------------------------------------------- helpers & env
function Get-EnvValue([string]$keyName) {
    $envVal = [System.Environment]::GetEnvironmentVariable($keyName)
    if ($envVal) { return $envVal }
    $envPath = Join-Path $repoRoot '.env'
    if (Test-Path $envPath) {
        $lines = Get-Content $envPath
        foreach ($line in $lines) {
            $trimmed = $line.Trim()
            if ($trimmed -and -not $trimmed.StartsWith('#') -and $trimmed.Contains('=')) {
                $idx = $trimmed.IndexOf('=')
                $k = $trimmed.Substring(0, $idx).Trim()
                $v = $trimmed.Substring($idx + 1).Trim().Trim('"').Trim("'")
                if ($k -eq $keyName) { return $v }
            }
        }
    }
    return ''
}

if (-not $HashSecret) { $HashSecret = Get-EnvValue 'VNPAY_HASH_SECRET' }
if (-not $HashSecret) { $HashSecret = 'DEMOHASHSECRET1234567890' } # Development default

if (-not $TmnCode) { $TmnCode = Get-EnvValue 'VNPAY_TMN_CODE' }
if (-not $TmnCode) { $TmnCode = 'PARKWISE' }

if (-not $ServiceKey) { $ServiceKey = Get-EnvValue 'SERVICE_KEY' }

$IdentityUrl = $IdentityUrl.TrimEnd('/')
$PaymentUrl = $PaymentUrl.TrimEnd('/')
$GatewayUrl = $GatewayUrl.TrimEnd('/')

$targetPaymentUrl = if ($UseGateway) { $GatewayUrl } else { $PaymentUrl }
$targetIdentityUrl = if ($UseGateway) { $GatewayUrl } else { $IdentityUrl }

function Write-Step([string]$msg) { Write-Host "`n== $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  [OK]   $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Note([string]$msg) { Write-Host "  [INFO] $msg" -ForegroundColor DarkGray }

function Invoke-Api {
    param ([string]$Method, [string]$Url, $Body = $null, [string]$Token = '', [hashtable]$ExtraHeaders = @{})
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    if ($ServiceKey) { $headers['X-Service-Key'] = $ServiceKey }
    foreach ($k in $ExtraHeaders.Keys) { $headers[$k] = $ExtraHeaders[$k] }

    $req = @{ Method = $Method; Uri = $Url; Headers = $headers; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $req['Body'] = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
        $req['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $resp = Invoke-WebRequest @req
        $json = $null
        if ($resp.Content) { try { $json = $resp.Content | ConvertFrom-Json } catch { $json = $null } }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Text = $resp.Content; Json = $json }
    }
    catch {
        $ex = $_.Exception
        $failed = $ex.Response
        $status = 0
        $text = ''
        if ($failed -is [System.Net.HttpWebResponse]) {
            $status = [int]$failed.StatusCode
            $reader = New-Object System.IO.StreamReader($failed.GetResponseStream())
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } elseif ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            $text = $_.ErrorDetails.Message
        } else {
            $text = $ex.Message
        }
        $json = $null
        if ($text) { try { $json = $text | ConvertFrom-Json } catch { $json = $null } }
        return [pscustomobject]@{ Status = $status; Text = $text; Json = $json }
    }
}

function Invoke-PsqlDirect([string]$Sql) {
    if ($SqlMode -ne 'Docker') { return }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return }
    $psql = 'psql -X -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d $POSTGRES_DB'
    Push-Location $repoRoot
    try {
        if ($PostgresContainer) { $Sql | & docker exec -i $PostgresContainer sh -c $psql 2>$null | Out-Null }
        else { $Sql | & docker compose exec -T postgres sh -c $psql 2>$null | Out-Null }
    }
    catch {}
    finally { Pop-Location }
}

function Compute-VnpayHash([string]$secret, [string]$rawHashData) {
    $hmac = New-Object System.Security.Cryptography.HMACSHA512
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
    $hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rawHashData))
    $hmac.Dispose()
    return (-join ($hashBytes | ForEach-Object { $_.ToString('x2') })).ToLowerInvariant()
}

function Ensure-CustomerAccount {
    param ([string]$Email, [string]$FullName, [string]$VehiclePlate, [string]$VehicleType)
    
    # 1. Login attempt
    $login = Invoke-Api POST "$targetIdentityUrl/api/identity/accounts/login" @{ email = $Email; password = $Password }
    if ($login.Status -ne 200) {
        if (-not $Apply) {
            Write-Note "$($Email): Tai khoan chua dang nhap duoc (HTTP $($login.Status)). Se dang ky khi chay -Apply."
            return $null
        }
        # Register
        $reg = Invoke-Api POST "$targetIdentityUrl/api/identity/accounts" @{ fullName = $FullName; email = $Email; password = $Password; accountType = 'Customer' }
        if ($reg.Status -ge 200 -and $reg.Status -lt 300) {
            Write-Ok "$($Email): Da dang ky tai khoan thanh cong"
        }
        # Verify via SQL
        Invoke-PsqlDirect "UPDATE identity.accounts SET status = 'EmailVerified' WHERE email = '$Email';"
        
        # Re-login
        $login = Invoke-Api POST "$targetIdentityUrl/api/identity/accounts/login" @{ email = $Email; password = $Password }
        if ($login.Status -ne 200) {
            throw "Khong the dang nhap tai khoan $($Email) sau khi dang ky: HTTP $($login.Status) $($login.Text)"
        }
    }

    $token = $login.Json.accessToken
    Write-Ok "$($Email): Dang nhap thanh cong"

    # Profile & Vehicle
    if ($Apply) {
        $prof = Invoke-Api PUT "$targetIdentityUrl/api/identity/accounts/me/profile" @{ fullName = $FullName } $token
        $veh = Invoke-Api POST "$targetIdentityUrl/api/identity/accounts/me/vehicles" @{ licensePlate = $VehiclePlate; vehicleType = $VehicleType; nickName = "Xe Concurrency ($VehiclePlate)" } $token
        if ($veh.Status -eq 200 -or $veh.Status -eq 201) {
            Write-Ok "$($Email): Da dang ky xe $VehiclePlate ($VehicleType)"
        } elseif ($veh.Text -like '*already_registered*') {
            Write-Note "$($Email): Bien so $VehiclePlate da duoc dang ky truoc do"
        }
    }

    return [pscustomobject]@{ Email = $Email; FullName = $FullName; Token = $token; Plate = $VehiclePlate }
}

function Ensure-WalletBalance {
    param ($Customer, [long]$RequiredBalance)
    if (-not $Customer -or -not $Customer.Token) { return }

    # 1. Check current balance
    $wallet = Invoke-Api GET "$targetPaymentUrl/api/payment/wallets/me" $null $Customer.Token
    $currentBalance = 0
    if ($wallet.Status -eq 200 -and $wallet.Json) {
        $currentBalance = [long]$wallet.Json.balance
    }

    Write-Host ("  {0,-28} So du vi hien tai: {1,10:N0} VND | Muc tieu: {2,10:N0} VND" -f $Customer.Email, $currentBalance, $RequiredBalance) -ForegroundColor White

    if ($currentBalance -ge $RequiredBalance) {
        Write-Ok "$($Customer.Email): So du da du (>= $RequiredBalance VND), khong can nap them."
        return
    }

    $deficit = $RequiredBalance - $currentBalance

    if (-not $Apply) {
        Write-Note "[DRY-RUN] Du kien nap them: $deficit VND qua Webhook IPN VNPay (chay voi -Apply de nap that)."
        return
    }

    Write-Host "  -> Dang tao yeu cau top-up $deficit VND..." -ForegroundColor Gray
    $idemKey = "seed-concurrency-" + [Guid]::NewGuid().ToString("N")
    $topup = Invoke-Api POST "$targetPaymentUrl/api/payment/wallets/top-up" @{ amount = $deficit } $Customer.Token @{ "Idempotency-Key" = $idemKey }
    
    if ($topup.Status -ne 200 -and $topup.Status -ne 201) {
        Write-Warn "$($Customer.Email): Tao top-up that bai: HTTP $($topup.Status) $($topup.Text)"
        return
    }

    $txnRef = $topup.Json.transactionId
    if (-not $txnRef) {
        Write-Warn "$($Customer.Email): Khong nhan duoc transactionId"
        return
    }

    Write-Host "  -> Dang tinh toan chu ky HMAC-SHA512 va ban Webhook IPN cho TxnRef: $txnRef..." -ForegroundColor Gray
    $now = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7))
    $dateStr = $now.ToString('yyyyMMddHHmmss')
    $vnpAmount = ($deficit * 100).ToString()

    $params = [ordered]@{
        'vnp_Amount'            = $vnpAmount
        'vnp_BankCode'          = 'NCB'
        'vnp_BankTranNo'        = "BT$dateStr"
        'vnp_CardType'          = 'ATM'
        'vnp_Command'           = 'pay'
        'vnp_CreateDate'        = $dateStr
        'vnp_CurrCode'          = 'VND'
        'vnp_OrderInfo'         = "Nap tien vi $txnRef"
        'vnp_PayDate'           = $dateStr
        'vnp_ResponseCode'      = '00'
        'vnp_TmnCode'           = $TmnCode
        'vnp_TransactionNo'     = "TR$dateStr"
        'vnp_TransactionStatus' = '00'
        'vnp_TxnRef'            = $txnRef
        'vnp_Version'           = '2.1.0'
    }

    $rawList = New-Object 'System.Collections.Generic.List[string]'
    foreach ($k in $params.Keys) {
        $encKey = [System.Net.WebUtility]::UrlEncode($k)
        $encVal = [System.Net.WebUtility]::UrlEncode([string]$params[$k])
        $rawList.Add("$encKey=$encVal")
    }
    $rawQuery = [string]::Join('&', $rawList)
    $secureHash = Compute-VnpayHash $HashSecret $rawQuery
    $finalQuery = "$rawQuery&vnp_SecureHash=$secureHash"

    $ipnUrl = "$targetPaymentUrl/api/payment/vnpay/ipn?$finalQuery"
    $ipnRes = Invoke-Api GET $ipnUrl

    if ($ipnRes.Status -eq 200) {
        Write-Ok "$($Customer.Email): Nap thanh cong $deficit VND qua IPN (RspCode: 00)"
        
        # Check balance again
        $after = Invoke-Api GET "$targetPaymentUrl/api/payment/wallets/me" $null $Customer.Token
        if ($after.Status -eq 200 -and $after.Json) {
            $newBal = [long]$after.Json.balance
            Write-Ok "$($Customer.Email): So du moi: {0:N0} VND" -f $newBal
        }
    } else {
        Write-Warn "$($Customer.Email): Goi Webhook IPN that bai: HTTP $($ipnRes.Status) $($ipnRes.Text)"
    }
}

# ===================================================================================
# MAIN EXECUTION
# ===================================================================================
Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host "  SEED SO DU VI & TAI KHOAN PHUC VU KICH BAN CONCURRENCY DEMO TUAN 4   " -ForegroundColor Magenta
Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host "Identity URL : $targetIdentityUrl"
Write-Host "Payment URL  : $targetPaymentUrl"
Write-Host ("Muc tieu vi  : {0:N0} VND" -f $TargetBalance)
Write-Host "Che do chay  : $(if ($Apply) { 'APPLY (Ghi du lieu that)' } else { 'DRY-RUN (Xem truoc)' })"
Write-Host ""

try {
    Write-Step "1. Kiem tra & Chuan bi tai khoan Customer 1 & Customer 2"
    $c1 = Ensure-CustomerAccount 'customer@demo.local'  'Khach Hang Demo 1' '51A-123.45' 'Car'
    $c2 = Ensure-CustomerAccount 'customer2@demo.local' 'Khach Hang Demo 2' '51B-999.88' 'Car'

    Write-Step ("2. Kiem tra & Nap so du vi (Target: {0:N0} VND)" -f $TargetBalance)
    Ensure-WalletBalance $c1 $TargetBalance
    Ensure-WalletBalance $c2 $TargetBalance

    Write-Host "`n======================================================================" -ForegroundColor Green
    if ($Apply) {
        Write-Host "  HOAN TAT: DU LIEU SAN SANG CHO KICH BAN CONCURRENCY DAU COC SLOT!" -ForegroundColor Green
    } else {
        Write-Host "  DRY-RUN XONG: Chay them -Apply de thuc thi nap so du vi." -ForegroundColor Yellow
    }
    Write-Host "======================================================================" -ForegroundColor Green
}
catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

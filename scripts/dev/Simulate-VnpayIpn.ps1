#Requires -Version 5.1
<#
.SYNOPSIS
    Simulates a VNPay IPN webhook callback for local development and demo testing.

.DESCRIPTION
    Computes a valid HMAC-SHA512 signature matching VnpayPaymentGateway.VerifyIpnSignature
    and sends the callback to the payment service through the API Gateway (or directly).

    Dry run by default: prints the calculated query parameters and signature.
    Use -Apply to send the HTTP GET request.

.EXAMPLE
    .\scripts\dev\Simulate-VnpayIpn.ps1 -TxnRef "txn_12345" -AmountVnd 50000

.EXAMPLE
    .\scripts\dev\Simulate-VnpayIpn.ps1 -TxnRef "txn_12345" -AmountVnd 50000 -Apply
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$TxnRef,

    [Parameter(Mandatory = $true)]
    [long]$AmountVnd,

    [ValidateSet('Success', 'Failed')]
    [string]$Result = 'Success',

    [string]$GatewayUrl = 'http://127.0.0.1:5000',
    [string]$BankCode = 'NCB',
    [string]$TmnCode = '',
    [string]$HashSecret = '',
    [string]$ServiceKey = '',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Get-EnvFileValue([string]$keyName) {
    $envPath = Join-Path $repoRoot '.env'
    if (Test-Path $envPath) {
        $lines = Get-Content $envPath
        foreach ($line in $lines) {
            $trimmed = $line.Trim()
            if ($trimmed -and -not $trimmed.StartsWith('#') -and $trimmed.Contains('=')) {
                $idx = $trimmed.IndexOf('=')
                $k = $trimmed.Substring(0, $idx).Trim()
                $v = $trimmed.Substring($idx + 1).Trim().Trim('"').Trim("'")
                if ($k -eq $keyName) {
                    return $v
                }
            }
        }
    }
    return ''
}

if (-not $HashSecret) {
    $HashSecret = $env:VNPAY_HASH_SECRET
    if (-not $HashSecret) {
        $HashSecret = Get-EnvFileValue 'VNPAY_HASH_SECRET'
    }
}

if (-not $TmnCode) {
    $TmnCode = $env:VNPAY_TMN_CODE
    if (-not $TmnCode) {
        $TmnCode = Get-EnvFileValue 'VNPAY_TMN_CODE'
    }
    if (-not $TmnCode) {
        $TmnCode = 'PARKWISE'
    }
}

if (-not $ServiceKey) {
    $ServiceKey = $env:SERVICE_KEY
    if (-not $ServiceKey) {
        $ServiceKey = Get-EnvFileValue 'SERVICE_KEY'
    }
}

if ($AmountVnd -le 0) {
    throw 'AmountVnd must be greater than 0.'
}

if (-not $TxnRef.Trim()) {
    throw 'TxnRef cannot be empty.'
}

if (-not $HashSecret) {
    throw 'VNPAY_HASH_SECRET is required. Provide -HashSecret or set VNPAY_HASH_SECRET in .env or environment.'
}

$GatewayUrl = $GatewayUrl.TrimEnd('/')
$now = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7))
$dateStr = $now.ToString('yyyyMMddHHmmss')

$responseCode = if ($Result -eq 'Success') { '00' } else { '24' }
$transStatus = if ($Result -eq 'Success') { '00' } else { '02' }
$vnpAmount = ($AmountVnd * 100).ToString([System.Globalization.CultureInfo]::InvariantCulture)

$params = New-Object 'System.Collections.Generic.SortedDictionary[string,string]' ([System.StringComparer]::Ordinal)
$params['vnp_Amount'] = $vnpAmount
$params['vnp_BankCode'] = $BankCode
$params['vnp_BankTranNo'] = "BT$dateStr"
$params['vnp_CardType'] = 'ATM'
$params['vnp_Command'] = 'pay'
$params['vnp_CreateDate'] = $dateStr
$params['vnp_CurrCode'] = 'VND'
$params['vnp_OrderInfo'] = "Nap tien vi $TxnRef"
$params['vnp_PayDate'] = $dateStr
$params['vnp_ResponseCode'] = $responseCode
$params['vnp_TmnCode'] = $TmnCode
$params['vnp_TransactionNo'] = "TR$dateStr"
$params['vnp_TransactionStatus'] = $transStatus
$params['vnp_TxnRef'] = $TxnRef
$params['vnp_Version'] = '2.1.0'

$rawList = New-Object 'System.Collections.Generic.List[string]'
foreach ($k in $params.Keys) {
    $v = $params[$k]
    if ($v) {
        $encKey = [System.Net.WebUtility]::UrlEncode($k)
        $encVal = [System.Net.WebUtility]::UrlEncode($v)
        $rawList.Add("$encKey=$encVal")
    }
}
$rawQuery = [string]::Join('&', $rawList)

$hmac = New-Object System.Security.Cryptography.HMACSHA512
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($HashSecret)
$hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rawQuery))
$secureHash = (-join ($hashBytes | ForEach-Object { $_.ToString('x2') })).ToLowerInvariant()
$hmac.Dispose()

$finalQuery = "$rawQuery&vnp_SecureHash=$secureHash"
$requestUrl = "$GatewayUrl/api/payment/vnpay/ipn?$finalQuery"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "           VNPay IPN Webhook Simulation" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "TxnRef:       $TxnRef"
Write-Host "Amount VND:   $AmountVnd (vnp_Amount: $vnpAmount)"
Write-Host "Result:       $Result (ResponseCode: $responseCode, Status: $transStatus)"
Write-Host "Gateway URL:  $GatewayUrl"
Write-Host "SecureHash:   $secureHash"
Write-Host ""

if (-not $Apply) {
    Write-Host "[DRY-RUN] Script running in preview mode. No HTTP request was sent." -ForegroundColor Yellow
    Write-Host "To execute this request against the server, re-run with -Apply:" -ForegroundColor Yellow
    Write-Host "    .\scripts\dev\Simulate-VnpayIpn.ps1 -TxnRef '$TxnRef' -AmountVnd $AmountVnd -Apply" -ForegroundColor White
    Write-Host ""
    Write-Host "Target Request URL:" -ForegroundColor Gray
    Write-Host $requestUrl -ForegroundColor DarkGray
    return
}

Write-Host "[APPLY] Sending GET request to VNPay IPN endpoint..." -ForegroundColor Green

$headers = @{}
if ($ServiceKey) {
    $headers['X-Service-Key'] = $ServiceKey
}

try {
    $response = Invoke-RestMethod -Uri $requestUrl -Method Get -Headers $headers -TimeoutSec 10
    Write-Host "IPN Response received successfully:" -ForegroundColor Green
    $response | Format-List
} catch {
    Write-Host "Failed to invoke IPN webhook:" -ForegroundColor Red
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    } else {
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
    throw
}

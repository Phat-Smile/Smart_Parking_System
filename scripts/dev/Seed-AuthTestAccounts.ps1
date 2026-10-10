[CmdletBinding()]
param(
    # Gọi thẳng Identity (không qua Gateway) để chạy lại nhiều lần không bị rate limit đăng ký 5 lần/phút.
    [string]$IdentityUrl = 'http://127.0.0.1:5001',
    # Mật khẩu chung cho tài khoản test, chỉ dùng ở máy dev.
    [string]$Password = 'Test@12345',
    [string]$PostgresContainer = 'parking-system-postgres-1'
)

# Tạo 4 tài khoản đã xác thực email, mỗi tài khoản một role, để FE / tester thử menu và quyền theo role.
# Chạy lại an toàn: email đã có thì bỏ qua bước đăng ký, chỉ đặt lại trạng thái và role.
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

$accounts = [ordered]@{
    'customer@parking.test' = 'Customer'
    'staff@parking.test'    = 'Staff'
    'owner@parking.test'    = 'ParkingLotOwner'
    'admin@parking.test'    = 'SystemAdmin'
}

# Cùng mặc định với docker-compose.yml; giá trị trong .env được ưu tiên.
$settings = @{ POSTGRES_DB = 'parking_system'; POSTGRES_USER = 'parking_dev' }
$envFile = Join-Path $repositoryRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    foreach ($line in Get-Content -LiteralPath $envFile) {
        if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and $settings.ContainsKey($Matches[1]) -and $Matches[2]) {
            $settings[$Matches[1]] = $Matches[2]
        }
    }
}

foreach ($email in $accounts.Keys) {
    $body = @{ email = $email; password = $Password } | ConvertTo-Json
    try {
        Invoke-RestMethod -Method Post -Uri "$IdentityUrl/api/identity/accounts" -ContentType 'application/json' -Body $body | Out-Null
        Write-Host "Đã đăng ký $email"
    }
    catch {
        $status = [int]$_.Exception.Response.StatusCode
        if ($status -ne 409) { throw "Đăng ký $email thất bại (HTTP $status): $($_.ErrorDetails.Message)" }
        Write-Host "$email đã có, bỏ qua bước đăng ký"
    }
}

# Bỏ qua OTP: tài khoản test được đánh dấu đã xác thực và gán role trực tiếp trong DB dev.
$updates = foreach ($email in $accounts.Keys) {
    "UPDATE identity.accounts SET status = 'EmailVerified', role = '$($accounts[$email])', failed_login_attempts = 0, lockout_end_utc = NULL WHERE email = '$email';"
}
$sql = $updates -join ' '
docker exec $PostgresContainer psql -U $settings.POSTGRES_USER -d $settings.POSTGRES_DB -v ON_ERROR_STOP=1 -q -c $sql | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Không cập nhật được DB. Kiểm tra container PostgreSQL đang chạy.' }

$accounts.GetEnumerator() | ForEach-Object { [pscustomobject]@{ Email = $_.Key; Role = $_.Value; Password = $Password } } |
    Format-Table -AutoSize

#Requires -Version 5.1
<#
.SYNOPSIS
    Thiết lập kịch bản "Hết chỗ cho xe không đặt trước" (Walk-in Capacity = 0) cho bước demo 4.6.

.DESCRIPTION
    Kịch bản theo kich-ban-demo-sau-dot-6.md (Phân đoạn 4, Bước 4.6) và Tin 270:
      - Bãi đỗ Trung tâm A có 2 chỗ ô tô (CAR-A: A-01 và A-02).
      - Sử dụng thuần túy REST API của các microservices (TUYỆT ĐỐI KHÔNG GHI TRỰC TIẾP VÀO DATABASE):
        1. Đặt chỗ trước qua Booking API (POST /api/booking/holds cho xe 51A-123.45).
        2. Check-in qua Gate API (POST /api/parking/sessions cho xe 51B-888.88 để chiếm chỗ A-01).
        3. Chuyển slot A-02 sang trạng thái bảo trì qua Owner API (PUT /api/parking/owner/lots/{lotId}/slots/{slotId}/maintenance)
           nhằm đảm bảo bãi thực sự hết chỗ cho xe vãng lai (do StubHeldQuotaReader trên develop trả held=0).
    
    KẾT QUẢ ĐẠT ĐƯỢC:
      - Xe vãng lai không đặt trước (ví dụ: 51B-999.88) vào cổng:
        Hệ thống từ chối với lỗi: "Hết chỗ cho xe không đặt trước." (parking.walk_in.full).
      - Hỗ trợ tham số -Reset để khôi phục slot A-02 về Sẵn sàng và hủy phiên test qua REST API.

.PARAMETER IdentityUrl
    Base URL của Identity service (mặc định: http://127.0.0.1:5001 hoặc qua Gateway).

.PARAMETER ParkingUrl
    Base URL của Parking service (mặc định: http://127.0.0.1:5002 hoặc qua Gateway).

.PARAMETER BookingUrl
    Base URL của Booking service (mặc định: http://127.0.0.1:5003 hoặc qua Gateway).

.PARAMETER LotId
    Mã định danh bãi xe (mặc định: Bãi đỗ Trung tâm A: 10000000-0000-0000-0000-000000000001).

.PARAMETER SlotA02Id
    Mã định danh ô đỗ A-02 (mặc định: 40000000-0000-0000-0000-000000000002).

.PARAMETER Reset
    Khôi phục lại trạng thái ban đầu của bãi xe hoàn toàn qua REST API.

.PARAMETER Apply
    Thực thi thay đổi. Mặc định là Dry-run (chỉ in kế hoạch gọi REST API).

.EXAMPLE
    .\scripts\dev\Seed-WalkInFullScenario.ps1
    In kế hoạch gọi REST API (Dry-run).

.EXAMPLE
    .\scripts\dev\Seed-WalkInFullScenario.ps1 -Apply
    Thực thi thiết lập tình huống walk-in capacity = 0 qua REST API.

.EXAMPLE
    .\scripts\dev\Seed-WalkInFullScenario.ps1 -Reset -Apply
    Khôi phục lại bãi xe về trạng thái bình thường qua REST API.
#>
[CmdletBinding()]
param (
    [string]$IdentityUrl = 'http://127.0.0.1:5001',
    [string]$ParkingUrl = 'http://127.0.0.1:5002',
    [string]$BookingUrl = 'http://127.0.0.1:5003',
    [string]$LotId = '10000000-0000-0000-0000-000000000001',
    [string]$SlotA02Id = '40000000-0000-0000-0000-000000000002',
    [string]$CarZoneId = '30000000-0000-0000-0000-000000000001',
    [string]$DemoPassword = $(if ($env:DEMO_PASSWORD) { $env:DEMO_PASSWORD } else { 'Demo@12345' }),
    [switch]$Reset,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$DemoPassword = if ($env:DEMO_PASSWORD) { $env:DEMO_PASSWORD } else { $DemoPassword }

function Write-Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text)   { Write-Host "  [OK]   $Text" -ForegroundColor Green }
function Write-Note([string]$Text) { Write-Host "  [INFO] $Text" -ForegroundColor DarkGray }
function Write-Warn([string]$Text) { Write-Host "  [WARN] $Text" -ForegroundColor Yellow }

# -----------------------------------------------------------------------------
# REST API HELPER FUNCTIONS (TUYỆT ĐỐI KHÔNG CHẠY SQL)
# -----------------------------------------------------------------------------

function Get-AuthToken {
    param (
        [string]$Email,
        [string]$Password
    )
    $loginUrl = "$IdentityUrl/api/identity/accounts/login"
    $body = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri $loginUrl -Method Post -Body $body -ContentType "application/json"
        if ($response.accessToken) {
            return $response.accessToken
        }
        throw "Không tìm thấy accessToken trong phản hồi đăng nhập của $Email."
    } catch {
        throw "Lỗi đăng nhập tài khoản $Email tại $loginUrl: $_"
    }
}

function Set-SlotMaintenanceViaApi {
    param (
        [string]$TargetLotId,
        [string]$TargetSlotId,
        [bool]$UnderMaintenance,
        [string]$Reason,
        [string]$Token
    )
    $url = "$ParkingUrl/api/parking/owner/lots/$TargetLotId/slots/$TargetSlotId/maintenance"
    $body = @{
        underMaintenance = $UnderMaintenance
        reason = $Reason
    } | ConvertTo-Json
    $headers = @{
        Authorization = "Bearer $Token"
    }

    try {
        $response = Invoke-RestMethod -Uri $url -Method Put -Headers $headers -Body $body -ContentType "application/json"
        return $response
    } catch {
        throw "Lỗi gọi API thiết lập bảo trì slot $TargetSlotId tại $url: $_"
    }
}

function Open-ParkingSessionViaGateApi {
    param (
        [string]$TargetLotId,
        [string]$PlateNumber,
        [string]$VehicleType,
        [string]$Token
    )
    $url = "$ParkingUrl/api/parking/sessions"
    $body = @{
        lotId = $TargetLotId
        plateNumber = $PlateNumber
        vehicleType = $VehicleType
    } | ConvertTo-Json
    $headers = @{
        Authorization = "Bearer $Token"
    }

    try {
        $response = Invoke-RestMethod -Uri $url -Method Post -Headers $headers -Body $body -ContentType "application/json"
        return $response
    } catch {
        # Nếu đã có phiên mở sẵn cho biển này, ghi nhận thông báo
        Write-Warn "Không thể mở phiên mới cho $PlateNumber (có thể phiên đã tồn tại): $_"
        return $null
    }
}

function Create-BookingHoldViaApi {
    param (
        [string]$TargetLotId,
        [string]$PlateNumber,
        [string]$Token
    )
    $url = "$BookingUrl/api/booking/holds"
    $now = [DateTimeOffset]::UtcNow
    $body = @{
        lotId = $TargetLotId
        plateNumber = $PlateNumber
        vehicleGroup = "Car"
        reservationType = "Scheduled"
        appointmentTime = $now.AddMinutes(15).ToString("o")
        durationMinutes = 120
        prepaidAmount = 50000
    } | ConvertTo-Json
    $headers = @{
        Authorization = "Bearer $Token"
    }

    try {
        $response = Invoke-RestMethod -Uri $url -Method Post -Headers $headers -Body $body -ContentType "application/json"
        return $response
    } catch {
        Write-Warn "Không thể tạo giữ chỗ tạm cho $PlateNumber tại Booking API: $_"
        return $null
    }
}

function Cancel-OpenParkingSessionViaApi {
    param (
        [string]$TargetLotId,
        [string]$PlateNumber,
        [string]$Token
    )
    $headers = @{
        Authorization = "Bearer $Token"
    }

    try {
        $findUrl = "$ParkingUrl/api/parking/sessions/open?lotId=$TargetLotId&plateNumber=$PlateNumber"
        $openSession = Invoke-RestMethod -Uri $findUrl -Method Get -Headers $headers
        if ($openSession -and $openSession.sessionId) {
            $cancelUrl = "$ParkingUrl/api/parking/sessions/$($openSession.sessionId)/cancel"
            $body = @{ reason = "Khoi phuc sau kich ban WalkInFull" } | ConvertTo-Json
            Invoke-RestMethod -Uri $cancelUrl -Method Post -Headers $headers -Body $body -ContentType "application/json" | Out-Null
            return $true
        }
    } catch {
        Write-Note "Không tìm thấy phiên mở của $PlateNumber cần hủy."
    }
    return $false
}

# -----------------------------------------------------------------------------
# MAIN FLOW
# -----------------------------------------------------------------------------
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " SMART PARKING SYSTEM - SCENARIO SETUP: WALK-IN FULL (DEMO STEP 4.6)    " -ForegroundColor Cyan
Write-Host " [PURE REST API MODE - ZERO DIRECT DATABASE SQL EXECUTION]              " -ForegroundColor Green
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "Lot ID:    $LotId (Bai do Trung tam A)" -ForegroundColor Gray
Write-Host "Slot A-02: $SlotA02Id" -ForegroundColor Gray
Write-Host "Mode:      $(if ($Reset) { 'RESET SCENARIO VIA REST API' } else { 'SEED WALK-IN FULL VIA REST API' })" -ForegroundColor Yellow

if (-not $Apply) {
    Write-Host "`n[DRY RUN] Script đang chạy ở chế độ mô phỏng. Truyền thêm -Apply để thực thi gọi REST API." -ForegroundColor Yellow
    Write-Host "Kế hoạch gọi REST API:"
    if ($Reset) {
        Write-Host "  1. Đăng nhập chủ bãi (owner@demo.local) lấy JWT Token."
        Write-Host "  2. Gọi Owner API: PUT /api/parking/owner/lots/$LotId/slots/$SlotA02Id/maintenance (underMaintenance: false)"
        Write-Host "     -> Khôi phục slot A-02 về trạng thái Sẵn sàng (Available)."
        Write-Host "  3. Đăng nhập nhân viên (staff@demo.local) lấy JWT Token."
        Write-Host "  4. Gọi Gate API: Tìm và hủy phiên đỗ xe 51B-888.88 qua POST /api/parking/sessions/{id}/cancel."
        Write-Host "     -> Giải phóng slot A-01 về Sẵn sàng (Available)."
    } else {
        Write-Host "  1. Đăng nhập khách hàng (customer@demo.local) -> Gọi Booking API giữ chỗ xe 51A-123.45."
        Write-Host "  2. Đăng nhập nhân viên (staff@demo.local) -> Gọi Gate API POST /api/parking/sessions xe 51B-888.88 để chiếm chỗ A-01."
        Write-Host "  3. Đăng nhập chủ bãi (owner@demo.local) -> Gọi Owner API PUT /api/parking/owner/lots/$LotId/slots/$SlotA02Id/maintenance (underMaintenance: true)."
        Write-Host "  -> Slot A-01 đã chiếm, slot A-02 đang bảo trì => Số chỗ ô tô sẵn sàng cho xe vãng lai = 0."
        Write-Host "  -> Xe vãng lai 51B-999.88 vào cổng sẽ bị từ chối chính xác: 'Hết chỗ cho xe không đặt trước.' (parking.walk_in.full)."
    }
    return
}

# -----------------------------------------------------------------------------
# ĐĂNG NHẬP VÀ LẤY TOKEN (QUA REST API)
# -----------------------------------------------------------------------------
Write-Step "1. Đang đăng nhập tài khoản Chủ bãi và Nhân viên..."
$ownerToken = Get-AuthToken -Email 'owner@demo.local' -Password $DemoPassword
$staffToken = Get-AuthToken -Email 'staff@demo.local' -Password $DemoPassword
Write-Ok "Đã lấy JWT Token hợp lệ cho Owner và Staff."

if ($Reset) {
    Write-Step "2. Gọi Owner API khôi phục slot A-02 về trạng thái Sẵn sàng..."
    Set-SlotMaintenanceViaApi -TargetLotId $LotId -TargetSlotId $SlotA02Id -UnderMaintenance $false -Reason "Khôi phục sau demo WalkInFull" -Token $ownerToken | Out-Null
    Write-Ok "Slot A-02 đã được mở lại (Available) qua Owner API."

    Write-Step "3. Gọi Gate API kiểm tra và hủy phiên đỗ xe 51B-888.88..."
    $cancelled = Cancel-OpenParkingSessionViaApi -TargetLotId $LotId -PlateNumber '51B-888.88' -Token $staffToken
    if ($cancelled) {
        Write-Ok "Đã hủy phiên đỗ của xe 51B-888.88 và giải phóng slot A-01."
    } else {
        Write-Note "Không có phiên đỗ nào của 51B-888.88 cần hủy."
    }

    Write-Ok "Hoàn tất khôi phục bãi đỗ xe về trạng thái ban đầu qua REST API."
    return
}

# -----------------------------------------------------------------------------
# THỰC THI SEED WALK-IN FULL (QUA REST API)
# -----------------------------------------------------------------------------
Write-Step "2. Đăng nhập khách hàng qua Identity REST API..."
$customerToken = Get-AuthToken -Email 'customer@demo.local' -Password $DemoPassword
Write-Ok "Đã lấy JWT Token cho Customer."

Write-Step "2. Gọi Booking API giữ chỗ cho xe 51A-123.45..."
$holdResult = Create-BookingHoldViaApi -TargetLotId $LotId -PlateNumber '51A-123.45' -Token $customerToken
if ($holdResult) {
    Write-Ok "Booking hold tạo thành công qua Booking API."
} else {
    Write-Note "Bỏ qua tạo booking hold (có thể đã có sẵn đặt chỗ)."
}

Write-Step "3. Gọi Gate API mở phiên đỗ xe 51B-888.88 để chiếm slot A-01..."
$sessionResult = Open-ParkingSessionViaGateApi -TargetLotId $LotId -PlateNumber '51B-888.88' -VehicleType 'Car' -Token $staffToken
if ($sessionResult) {
    Write-Ok "Phiên đỗ mở thành công cho xe 51B-888.88 (đã chiếm slot A-01)."
} else {
    Write-Note "Phiên đỗ xe 51B-888.88 đã tồn tại hoặc slot đã được chiếm."
}

Write-Step "4. Gọi Owner API chuyển slot A-02 sang trạng thái bảo trì (UnderMaintenance)..."
Set-SlotMaintenanceViaApi -TargetLotId $LotId -TargetSlotId $SlotA02Id -UnderMaintenance $true -Reason "Bảo trì phục vụ kịch bản WalkInFull (Tin 270)" -Token $ownerToken | Out-Null
Write-Ok "Slot A-02 đã chuyển sang trạng thái UnderMaintenance qua Owner API."

Write-Host "`nChi tiết trạng thái bãi xe hiện tại (qua REST API):" -ForegroundColor Green
Write-Host "  - Slot A-01: Occupied (xe 51B-888.88 đang đỗ qua Gate API)"
Write-Host "  - Slot A-02: UnderMaintenance (đã kích hoạt bảo trì qua Owner API)"
Write-Host "  - Số chỗ sẵn sàng cho xe vãng lai (Car Walk-in Capacity) = 0"
Write-Host "  ==> Sẵn sàng cho bước 4.6 của kịch bản demo (từ chối xe vãng lai với mã parking.walk_in.full)!" -ForegroundColor Cyan

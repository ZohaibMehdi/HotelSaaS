# ============================================================================
# HotelSaaS - End-to-end API acceptance tests (PRD sections 59, 60, 65)
# Run: powershell -File scripts\test-api.ps1   (API must be on localhost:5080)
# ============================================================================
$ErrorActionPreference = "Stop"
$Base = "http://localhost:5080"
$script:pass = 0; $script:fail = 0

function Invoke-Api {
    param($Method, $Uri, $Body = $null, $Token = $null)
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 } else { $null }
    try {
        $resp = Invoke-RestMethod -Method $Method -Uri "$Base$Uri" -Headers $headers `
            -ContentType "application/json" -Body $json -TimeoutSec 30
        return @{ ok = $true; data = $resp }
    } catch {
        $code = 0; $detail = ""
        try { $code = $_.Exception.Response.StatusCode.Value__ } catch {}
        try { $detail = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { $detail = $_.Exception.Message }
        return @{ ok = $false; status = $code; detail = $detail }
    }
}

function Check { param($Name, $Condition, $Info = "")
    if ($Condition) { $script:pass++; Write-Host "PASS  $Name" -ForegroundColor Green }
    else { $script:fail++; Write-Host "FAIL  $Name  $Info" -ForegroundColor Red }
}

Write-Host "`n=== HotelSaaS E2E Acceptance Tests ===`n" -ForegroundColor Cyan

# ---- 1. Authentication -----------------------------------------------------
$sa = Invoke-Api Post "/api/auth/login" @{ email = "superadmin@hotelsaas.com"; password = "Admin@123" }
Check "SuperAdmin login" ($sa.ok -and $sa.data.data.accessToken) ($sa.detail)
$saTok = $sa.data.data.accessToken

$fdA = Invoke-Api Post "/api/auth/login" @{ email = "frontdesk@saibaan.com"; password = "Admin@123" }
Check "FrontDesk (Saibaan) login" ($fdA.ok) ($fdA.detail)
$fdATok = $fdA.data.data.accessToken

$adB = Invoke-Api Post "/api/auth/login" @{ email = "admin@hotelabc.com"; password = "Admin@123" }
Check "TenantAdmin (HotelABC) login" ($adB.ok) ($adB.detail)
$adBTok = $adB.data.data.accessToken

$badLogin = Invoke-Api Post "/api/auth/login" @{ email = "frontdesk@saibaan.com"; password = "wrong-password" }
Check "Wrong password rejected 401" (-not $badLogin.ok -and $badLogin.status -eq 401) ($badLogin.detail)

# ---- 2. Platform administration --------------------------------------------
$tenants = Invoke-Api Get "/api/tenants?page=1&pageSize=10" -Token $saTok
Check "SuperAdmin lists tenants (2 seeded)" ($tenants.ok -and $tenants.data.data.totalCount -ge 2)

$tenantsDenied = Invoke-Api Get "/api/tenants" -Token $fdATok
Check "FrontDesk denied platform endpoints 403" (-not $tenantsDenied.ok -and $tenantsDenied.status -eq 403)

# ---- 3. Tenant isolation (PRD section 60) ----------------------------------
$roomsA = Invoke-Api Get "/api/rooms?page=1&pageSize=50" -Token $fdATok
Check "Saibaan sees own rooms (9)" ($roomsA.ok -and $roomsA.data.data.items.Count -ge 9)
$roomAId = ($roomsA.data.data.items | Where-Object { $_.roomNumber -eq "101" }).roomId

$crossRead = Invoke-Api Get "/api/rooms/$roomAId" -Token $adBTok
Check "HotelABC CANNOT read Saibaan room by id (404)" (-not $crossRead.ok) ("status=$($crossRead.status)")

$crossWrite = Invoke-Api Patch "/api/rooms/$roomAId/status" -Body @{ status = 4 } -Token $adBTok
Check "HotelABC CANNOT modify Saibaan room (404/403/401)" (-not $crossWrite.ok -and $crossWrite.status -in @(400,401,403,404)) ("status=$($crossWrite.status)")

$guestsB = Invoke-Api Get "/api/guests?search=Ahmed" -Token $adBTok
Check "HotelABC search does NOT leak Saibaan guests" ($guestsB.ok -and $guestsB.data.data.items.Count -eq 0) ("count=$($guestsB.data.data.items.Count)")

# ---- 4. Booking workflow (PRD section 65 walk-in scenario) ------------------
$today = (Get-Date).ToString("yyyy-MM-ddT14:00:00")
$out = (Get-Date).AddDays(2).ToString("yyyy-MM-ddT12:00:00")
$avail = Invoke-Api Get "/api/bookings/availability?from=$([Uri]::EscapeDataString((Get-Date).Date.AddDays(20).ToString('o')))&to=$([Uri]::EscapeDataString((Get-Date).Date.AddDays(22).ToString('o')))" -Token $fdATok
Check "Availability returns rooms with flags" ($avail.ok -and $avail.data.data.Count -ge 9)

$freeRoom = $avail.data.data | Where-Object { $_.isAvailable } | Select-Object -First 1
$guests = Invoke-Api Get "/api/guests?page=1&pageSize=50" -Token $fdATok

$bk = Invoke-Api Post "/api/bookings" @{
    guestId = $guests.data.data.items[0].guestId; bookingSource = 0
    checkInDate = (Get-Date).Date.AddDays(20).ToString("yyyy-MM-ddTHH:mm:ss")
    checkOutDate = (Get-Date).Date.AddDays(22).ToString("yyyy-MM-ddTHH:mm:ss")
    roomIds = @($freeRoom.roomId); numberOfAdults = 2
} -Token $fdATok
Check "Create reservation (Confirmed)" ($bk.ok -and $bk.data.data.status -eq 1) ($bk.detail)
$bookingId = $bk.data.data.bookingId

# Double-booking rejection (PRD section 60 acceptance criteria)
$duplicate = Invoke-Api Post "/api/bookings" @{
    guestId = $guests.data.data.items[0].guestId; bookingSource = 0
    checkInDate = (Get-Date).Date.AddDays(21).ToString("yyyy-MM-ddTHH:mm:ss")
    checkOutDate = (Get-Date).Date.AddDays(23).ToString("yyyy-MM-ddTHH:mm:ss")
    roomIds = @($freeRoom.roomId); numberOfAdults = 1
} -Token $fdATok
Check "Overlapping booking REJECTED 409" (-not $duplicate.ok -and $duplicate.status -eq 409) ("status=$($duplicate.status) $($duplicate.detail)")

# Check-in / charges / check-out
$ci = Invoke-Api Post "/api/bookings/$bookingId/check-in" -Token $fdATok
Check "Check-in succeeds" ($ci.ok)
$roomAfterCi = Invoke-Api Get "/api/rooms/$($freeRoom.roomId)" -Token $fdATok
Check "Room is Occupied after check-in" ($roomAfterCi.data.data.status -eq 2)

$charge = Invoke-Api Post "/api/bookings/$bookingId/charges" @{
    chargeType = 1; description = "Test dinner"; quantity = 2; unitPrice = 750
} -Token $fdATok
Check "Food charge added during stay" ($charge.ok -and $charge.data.data.totalAmount -eq 1500)

$co = Invoke-Api Post "/api/bookings/$bookingId/check-out" -Token $fdATok
Check "Check-out generates invoice" ($co.ok -and $co.data.data.invoiceId -gt 0)
$invoiceId = $co.data.data.invoiceId

$inv = Invoke-Api Get "/api/invoices/$invoiceId" -Token $fdATok
$expectedTotal = [math]::Round(($bk.data.data.rooms[0].ratePerNight * 2 + 1500), 2)
Check "Invoice total matches folio ($expectedTotal)" ([math]::Abs($inv.data.data.totalAmount - $expectedTotal) -lt 0.01) ("got=$($inv.data.data.totalAmount)")

$pay = Invoke-Api Post "/api/invoices/$invoiceId/payments" @{ amount = $expectedTotal / 2; method = 0; transactionReference = "TEST-PAY-1" } -Token $fdATok
Check "Partial payment recorded -> PartiallyPaid" ($pay.ok)
$invAfterPay = Invoke-Api Get "/api/invoices/$invoiceId" -Token $fdATok
Check "Balance reflects payment" ($invAfterPay.data.data.status -eq 2)

$overpay = Invoke-Api Post "/api/invoices/$invoiceId/payments" @{ amount = 999999; method = 0 } -Token $fdATok
Check "Overpayment blocked without flag 422" (-not $overpay.ok -and $overpay.status -eq 422)

$settle = Invoke-Api Post "/api/invoices/$invoiceId/payments?allowOverpayment=false" @{ amount = $expectedTotal / 2; method = 0; transactionReference = "TEST-PAY-2" } -Token $fdATok
Check "Final payment settles invoice" ($settle.ok)
$invPaid = Invoke-Api Get "/api/invoices/$invoiceId" -Token $fdATok
Check "Invoice now PAID" ($invPaid.data.data.status -eq 3)

# Room lifecycle after checkout
$hkRoomStatus = Invoke-Api Get "/api/rooms/$($freeRoom.roomId)" -Token $fdATok
Check "Room in Cleaning after check-out" ($hkRoomStatus.data.data.status -eq 3)

# ---- 5. B2B corporate rate application (PRD section 66) --------------------
$agreements = Invoke-Api Get "/api/b2b-agreements?status=1" -Token $fdATok
$agr = @($agreements.data.data) | Select-Object -First 1
$f30 = (Get-Date).Date.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ss")
$t32 = (Get-Date).Date.AddDays(32).ToString("yyyy-MM-ddTHH:mm:ss")
$corpRooms = Invoke-Api Get "/api/bookings/availability?from=$f30&to=$t32&agreementId=$($agr.agreementId)" -Token $fdATok
$stdRoom = $corpRooms.data.data | Where-Object { $_.roomTypeName -eq "Standard" -and $_.isAvailable } | Select-Object -First 1
Check "Contracted Standard rate = 5000" ($stdRoom.ratePerNight -eq 5000) ("got=$($stdRoom.ratePerNight)")
Check "Contracted rate differs from rack rate" ($stdRoom.ratePerNight -lt 5001)

$corpBooking = Invoke-Api Post "/api/bookings" @{
    guestId = $guests.data.data.items[0].guestId; bookingSource = 3; bookingType = 1
    corporateCustomerId = $agr.corporateCustomerId; agreementId = $agr.agreementId
    checkInDate = $f30; checkOutDate = $t32
    roomIds = @($stdRoom.roomId); numberOfAdults = 1
} -Token $fdATok
Check "Corporate booking uses contracted rate" ($corpBooking.ok -and $corpBooking.data.data.rooms[0].ratePerNight -eq 5000) ($corpBooking.detail)

# Expired/suspended agreement cannot apply rates (ownership-correct token)
$adA = Invoke-Api Post "/api/auth/login" @{ email = "admin@saibaan.com"; password = "Admin@123" }
$adATok = $adA.data.data.accessToken
$null = Invoke-RestMethod -Method Post -Uri "$Base/api/b2b-agreements/$($agr.agreementId)/status/3" -Headers @{ Authorization = "Bearer $adATok" }
$corpBlocked = Invoke-Api Post "/api/bookings" @{
    guestId = $guests.data.data.items[0].guestId; bookingSource = 3; bookingType = 1
    corporateCustomerId = $agr.corporateCustomerId; agreementId = $agr.agreementId
    checkInDate = (Get-Date).Date.AddDays(34).ToString("yyyy-MM-ddTHH:mm:ss")
    checkOutDate = (Get-Date).Date.AddDays(36).ToString("yyyy-MM-ddTHH:mm:ss")
    roomIds = @($stdRoom.roomId); numberOfAdults = 1
} -Token $fdATok
Check "SUSPENDED agreement blocks corporate booking 422" (-not $corpBlocked.ok -and $corpBlocked.status -eq 422)
Invoke-RestMethod -Method Post -Uri "$Base/api/b2b-agreements/$($agr.agreementId)/status/1" -Headers @{ Authorization = "Bearer $adATok" } | Out-Null

# ---- 6. Dashboards & reports ------------------------------------------------
$dash = Invoke-Api Get "/api/dashboard/tenant" -Token $fdATok
Check "Tenant dashboard loads" ($dash.ok -and $dash.data.data.totalRooms -ge 9)
$platform = Invoke-Api Get "/api/dashboard/platform" -Token $saTok
Check "Platform dashboard (SuperAdmin)" ($platform.ok -and $platform.data.data.totalTenants -ge 2)
$platformDenied = Invoke-Api Get "/api/dashboard/platform" -Token $fdATok
Check "FrontDesk blocked from platform dashboard 403" (-not $platformDenied.ok -and $platformDenied.status -eq 403)
$occFrom = (Get-Date).Date.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ss")
$occTo = (Get-Date).Date.ToString("yyyy-MM-ddTHH:mm:ss")
$occ = Invoke-Api Get "/api/reports/occupancy?from=$occFrom&to=$occTo" -Token $adBTok
Check "Occupancy report returns daily points" ($occ.ok -and @($occ.data.data).Count -eq 8) ("count=$(@($occ.data.data).Count)")

# ---- 7. Audit trail ----------------------------------------------------------
$audit = Invoke-Api Get "/api/audit-logs?page=1&pageSize=10" -Token $adATok
Check "Audit logs recorded for critical actions" ($audit.ok -and @($audit.data.data.items).Count -ge 5) ("count=$(@($audit.data.data.items).Count)")
$auditFd = Invoke-Api Get "/api/audit-logs" -Token $fdATok
Check "FrontDesk blocked from audit logs 403" (-not $auditFd.ok -and $auditFd.status -eq 403)

# ---- Summary -----------------------------------------------------------------
Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "RESULT: $script:pass passed, $script:fail failed" -ForegroundColor $(if ($script:fail -eq 0) { "Green" } else { "Red" })
if ($script:fail -eq 0) { exit 0 } else { exit 1 }

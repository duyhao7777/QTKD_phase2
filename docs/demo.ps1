# Demo tuần 06: Customer -> Contract -> Submit (thất bại rồi thành công), Pricing
# Cách chạy: khởi động API trước, sửa $base cho đúng port, rồi chạy: ./docs/demo.ps1

$base  = "http://localhost:5209"
$sales = @{ "X-User-Id" = "sales01"; "X-User-Role" = "Sales" }

function Call {
    param($Method, $Url, $Body = $null, $Headers = @{})
    $p = @{ Method = $Method; Uri = "$base$Url"; Headers = $Headers }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 5
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $p.ContentType = "application/json; charset=utf-8"
    }
    try {
        $r = Invoke-RestMethod @p
        Write-Host "  -> OK" -ForegroundColor Green
        Write-Host ($r | ConvertTo-Json -Depth 5)
        return $r
    }
    catch {
        Write-Host "  -> HTTP $([int]$_.Exception.Response.StatusCode)" -ForegroundColor Red
        Write-Host $_.ErrorDetails.Message
        return $null
    }
}

function Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

$suffix = Get-Date -Format "HHmmss"

Step "1. Tạo Customer"
$cust = Call POST "/api/customers" @{ name = "Công ty Demo $suffix" }

Step "2. Tạo Contract Draft (thiếu nội dung và nhóm dịch vụ)"
$contract = Call POST "/api/contracts" @{
    contractNo = "HD-DEMO-$suffix"
    customerId = $cust.id
    startDate  = "2026-10-01"
    endDate    = "2027-09-30"
}
$id  = $contract.id
$key = "demo-key-$suffix"

Step "3. Gửi trình duyệt khi thiếu dữ liệu (mong đợi 400, CTR-02)"
Call POST "/api/contracts/$id/submit" $null ($sales + @{ "Idempotency-Key" = $key }) | Out-Null

Step "4. Gửi khi không có quyền (mong đợi 403)"
Call POST "/api/contracts/$id/submit" $null @{ "X-User-Id" = "kt01"; "X-User-Role" = "Accountant" } | Out-Null

Step "5. Bổ sung dữ liệu (PUT)"
Call PUT "/api/contracts/$id" @{
    customerId   = $cust.id
    startDate    = "2026-10-01"
    endDate      = "2027-09-30"
    serviceGroup = "Dịch vụ cảng"
    content      = "Phạm vi dịch vụ và điều khoản thanh toán."
} | Out-Null

Step "6. Gửi lại (mong đợi 200, PendingApproval)"
$first = Call POST "/api/contracts/$id/submit" @{ comment = "Đề nghị duyệt" } ($sales + @{ "Idempotency-Key" = $key })

Step "7. Gửi lặp cùng Idempotency-Key (mong đợi 200, cùng approvalId)"
Call POST "/api/contracts/$id/submit" $null ($sales + @{ "Idempotency-Key" = $key }) | Out-Null

Step "8. Gửi lặp khác khóa (mong đợi 409)"
Call POST "/api/contracts/$id/submit" $null ($sales + @{ "Idempotency-Key" = "khac-$suffix" }) | Out-Null

Step "9. Sửa hợp đồng khi đã PendingApproval (mong đợi 409, CTR-01)"
Call PUT "/api/contracts/$id" @{
    customerId   = $cust.id
    startDate    = "2026-10-01"
    endDate      = "2027-09-30"
    serviceGroup = "Dịch vụ cảng"
    content      = "Thử sửa sau khi gửi"
} | Out-Null

Step "10. Xem lại hợp đồng (Status = PendingApproval)"
Call GET "/api/contracts/$id" | Out-Null

Step "11. Pricing: tạo bảng giá và thêm PriceItem"
$pl = Call POST "/api/pricelists" @{
    name          = "Bảng giá demo $suffix"
    customerId    = $cust.id
    serviceGroup  = "Dịch vụ cảng"
    effectiveFrom = "2027-01-01"
    effectiveTo   = "2027-12-31"
}
Call POST "/api/pricelists/$($pl.id)/items" @{ serviceCode = "CANG-101"; unitPrice = 150000 } | Out-Null
Call POST "/api/pricelists/$($pl.id)/items" @{ serviceCode = "CANG-102"; unitPrice = 220000 } | Out-Null
Call GET "/api/pricelists/$($pl.id)" | Out-Null

Write-Host "`nHoàn tất. Mở pgAdmin để kiểm tra Contracts, ApprovalInstances, ContractHistories." -ForegroundColor Yellow

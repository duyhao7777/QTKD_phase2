# QTKD – Hệ thống quản trị kinh doanh (Phase 2, Tuần 05)

## Yêu cầu
- .NET SDK 10.0
- PostgreSQL (port 5432)
- Công cụ `dotnet-ef`: `dotnet tool install --global dotnet-ef`

## Cấu hình database
Mật khẩu không lưu trong Git, dùng User Secrets:

```powershell
cd src/QTKD.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=qtkd;Username=postgres;Password=<mật khẩu>"
cd ../..
```

## Chạy migration
```powershell
dotnet ef database update --project src/QTKD.Api
```
Lệnh tạo database `qtkd`, 4 bảng và dữ liệu mẫu (2 Customer, 1 Contract Draft, 1 PriceList với 2 PriceItem).

## Chạy API
```powershell
dotnet run --project src/QTKD.Api
```

## Thử endpoint
```powershell
Invoke-RestMethod http://localhost:<port>/api/customers
Invoke-RestMethod http://localhost:<port>/api/contracts/33333333-3333-3333-3333-333333333333
```
Hợp đồng mẫu: HD-2026-018, Công ty Minh Phát, trạng thái Draft.

## Giả định và điểm cần xác nhận
- Enum `ContractStatus` gồm các trạng thái theo sơ đồ trạng thái Phase 1 và thêm `PendingApproval` theo US03. Cần xác nhận quan hệ giữa `Submitted` và `PendingApproval`.
- Contract có thêm `ServiceGroup` và `Content` để tuần 06 kiểm tra CTR-02. Danh sách nội dung/tệp bắt buộc chưa được xác nhận.
- PriceList dùng `EffectiveFrom/EffectiveTo`, `Status`, `CustomerId` để chuẩn bị cho PRC-01 và PRC-02.
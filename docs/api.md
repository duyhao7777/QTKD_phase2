# Tài liệu API – QTKD (Phase 2, Tuần 06)

## 1. Quy ước chung

**Base URL:** `http://localhost:<port>`

**Giả lập người dùng** (chưa có đăng nhập thật): các thao tác cần phân quyền đọc 2 header.

| Header | Ý nghĩa | Ví dụ |
|---|---|---|
| `X-User-Id` | Mã người dùng, dùng để ghi lịch sử | `sales01` |
| `X-User-Role` | Vai trò. Chỉ `Sales` (Nhân viên Kinh doanh) được gửi hợp đồng trình duyệt | `Sales` |
| `Idempotency-Key` | Khóa chống gửi lặp, dùng cho `POST /api/contracts/{id}/submit` (không bắt buộc) | `abc-123` |

**Mẫu phản hồi lỗi thống nhất:**

```json
{
  "code": "CTR-02",
  "message": "Hợp đồng chưa đủ điều kiện gửi trình duyệt",
  "errors": [
    { "code": "CTR-02.CONTENT_MISSING", "message": "Thiếu nội dung hợp đồng" }
  ]
}
```

| HTTP | Khi nào |
|---|---|
| 400 | Dữ liệu không hợp lệ, hoặc không đạt CTR-02 |
| 403 | Không có quyền |
| 404 | Không tìm thấy đối tượng |
| 409 | Sai trạng thái, trùng dữ liệu hoặc xung đột |
| 400 | INVALID_REQUEST | Body sai định dạng

## 2. Danh sách endpoint

### Customer

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/customers` | Danh sách khách hàng |
| GET | `/api/customers/{id}` | Chi tiết khách hàng |
| POST | `/api/customers` | Tạo khách hàng (`name` bắt buộc) |
| PUT | `/api/customers/{id}` | Cập nhật khách hàng |

Request tạo/cập nhật:
```json
{ "name": "Công ty Demo", "status": "Active" }
```
Response 201:
```json
{ "id": "…", "name": "Công ty Demo", "status": "Active" }
```
Lỗi mẫu (thiếu tên) – 400:
```json
{ "code": "VALIDATION_FAILED", "message": "Dữ liệu không hợp lệ",
  "errors": [ { "code": "CUSTOMER_NAME_REQUIRED", "message": "Tên khách hàng là bắt buộc" } ] }
```

### Contract

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/contracts` | Danh sách hợp đồng |
| GET | `/api/contracts/{id}` | Chi tiết hợp đồng |
| POST | `/api/contracts` | Tạo hợp đồng, luôn ở trạng thái `Draft` |
| PUT | `/api/contracts/{id}` | Cập nhật hợp đồng (CTR-01) |
| POST | `/api/contracts/{id}/submit` | Gửi trình duyệt (US03) |

Request tạo (`POST /api/contracts`):
```json
{
  "contractNo": "HD-2026-019",
  "customerId": "11111111-1111-1111-1111-111111111111",
  "startDate": "2026-10-01",
  "endDate": "2027-09-30",
  "serviceGroup": null,
  "content": null
}
```

Request cập nhật (`PUT /api/contracts/{id}`):
```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "startDate": "2026-10-01",
  "endDate": "2027-09-30",
  "serviceGroup": "Dịch vụ cảng",
  "content": "Phạm vi dịch vụ, điều khoản thanh toán…"
}
```

Cập nhật khi hợp đồng không ở Draft/RevisionRequested – 409:
```json
{ "code": "CTR-01", "message": "Chỉ được chỉnh sửa hợp đồng ở trạng thái Draft hoặc RevisionRequested", "errors": [] }
```

### US03 – Gửi hợp đồng trình duyệt

`POST /api/contracts/{id}/submit`

```
X-User-Id: sales01
X-User-Role: Sales
Idempotency-Key: <key>
```
Body (không bắt buộc): `{ "comment": "Đề nghị duyệt gấp" }`

**200 OK**
```json
{ "contractId": "…", "status": "PendingApproval", "approvalId": "…" }
```

| Mã | Tình huống | `code` |
|---|---|---|
| 400 | Không đạt CTR-02 (kèm danh sách điều kiện chưa đạt) | `CTR-02` |
| 403 | Không có quyền gửi | `FORBIDDEN` |
| 404 | Không tìm thấy hợp đồng | `CONTRACT_NOT_FOUND` |
| 409 | Sai trạng thái hoặc đã gửi (khác khóa) | `INVALID_STATE` |
| 409 | Hai yêu cầu đồng thời, yêu cầu thua | `CONCURRENT_SUBMIT` |

Gửi lặp **cùng `Idempotency-Key`**: trả lại đúng kết quả của lần đầu (cùng `approvalId`), không tạo thêm bản ghi.

### Pricing

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/pricelists` | Danh sách bảng giá |
| GET | `/api/pricelists/{id}` | Chi tiết bảng giá kèm các PriceItem |
| POST | `/api/pricelists` | Tạo bảng giá (`Draft`) |
| GET | `/api/pricelists/{id}/items` | Xem các PriceItem |
| POST | `/api/pricelists/{id}/items` | Thêm PriceItem |

Request tạo bảng giá:
```json
{
  "name": "Bảng giá 2027",
  "customerId": "11111111-1111-1111-1111-111111111111",
  "serviceGroup": "Dịch vụ cảng",
  "effectiveFrom": "2027-01-01",
  "effectiveTo": "2027-12-31"
}
```
Request thêm PriceItem:
```json
{ "serviceCode": "CANG-003", "unitPrice": 180000 }
```

## 3. Bảng đối chiếu US03 / CTR với mã nguồn

| Yêu cầu | Nội dung | Vị trí xử lý |
|---|---|---|
| AC01 | Không có quyền → từ chối, không đổi hợp đồng | `UserContext.CanSubmitContract`; bước 1 của `ContractService.SubmitAsync` → 403 |
| AC02 | Sai trạng thái → từ chối | Bước 3 của `SubmitAsync` → 409 `INVALID_STATE` |
| AC03 | Không đạt CTR-02 → không tạo quy trình, trả danh sách điều kiện | `ContractService.ValidateForSubmit` → 400 `CTR-02` |
| AC04 | Đủ điều kiện → tạo quy trình, chuyển `PendingApproval` | Bước 5 của `SubmitAsync` (một lần `SaveChanges` gồm đổi trạng thái, `ApprovalInstance`, `ContractHistory`) |
| AC05 | Gửi lặp/đồng thời không tạo nhiều phiên | Replay theo `Idempotency-Key` + chỉ mục duy nhất một phần `UX_Approval_OnePendingPerContract` |
| CTR-01 | Chỉ sửa ở Draft/RevisionRequested | `ContractRules.IsEditable`; `ContractService.UpdateAsync` |
| CTR-02 | Chỉ gửi khi khách hàng, thời gian hiệu lực, nội dung bắt buộc hợp lệ | `ContractService.ValidateForSubmit` |
| CTR-03 | Không chuyển thẳng Draft → Approved | `ContractRules.CanTransition` (Draft chỉ sang PendingApproval); `SubmitAsync` kiểm tra trước khi đổi trạng thái |
| PRC-01 | Bảng giá gắn khách hàng hoặc nhóm dịch vụ | `PriceListService.CreateAsync` |
| PRC-02 | Ngày bắt đầu không lớn hơn ngày kết thúc | `PriceListService.CreateAsync` |

## 4. Giả định và giới hạn của phiên bản hiện tại

Các điểm dưới đây **chưa được xác nhận trong Phase 1**, nhóm tự đặt ra để chạy demo:

1. **Nội dung bắt buộc (CTR-02):** `Content` và `ServiceGroup` của hợp đồng không được trống. Chưa kiểm tra tệp đính kèm vì chưa có danh sách tệp bắt buộc chính thức (Q2).
2. **Thời gian hiệu lực hợp lệ:** `StartDate <= EndDate`.
3. **Khách hàng hợp lệ:** tồn tại và có trạng thái `Active`.
4. **Ai được gửi (Q1):** giả lập bằng header `X-User-Role = Sales`. Chưa xác định chỉ người tạo hay mọi nhân viên Kinh doanh.
5. **Chỉ gửi được từ `Draft`.** Gửi lại từ `RevisionRequested` chưa triển khai.
6. **Trạng thái sau khi gửi:** dùng `PendingApproval` theo US03. Mối quan hệ với `Submitted` của sơ đồ trạng thái Phase 1 cần xác nhận.
7. **Mô phỏng quy trình phê duyệt:** bảng `ApprovalInstances` chỉ ghi nhận một phiên ở trạng thái `Pending`. Chưa có bước rà soát, phê duyệt hay từ chối.
8. **Chống gửi lặp:** cùng `Idempotency-Key` trả lại kết quả cũ; khác khóa trả 409. Chống đồng thời bằng chỉ mục duy nhất một phần (chỉ một `Pending` cho mỗi hợp đồng) ở mức database.
9. **Thêm PriceItem:** chỉ khi bảng giá ở `Draft`.
10. **Phân quyền thật (đăng nhập, JWT)** chưa triển khai, ngoài phạm vi phase này.

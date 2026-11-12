# Báo cáo kiểm thử – QTKD (Phase 2, Tuần 07)

## 1. Tóm tắt kết quả

Kết quả lấy từ file TRX của lần chạy cuối cùng, sau khi đã sửa lỗi TC10.

| Mục | Giá trị |
|---|---|
| Ngày chạy | 12/11/2026, 12:49:59 đến 12:50:03 (giờ Việt Nam) |
| Tổng số test | **71** |
| Đạt | **71** |
| Lỗi / Timeout / Bỏ qua | 0 / 0 / 0 |
| Thời gian chạy | khoảng 3,5 giây |
| Unit test | 43 |
| Integration test | 28 |
| Môi trường | .NET 10.0.9, xUnit (VSTest adapter 3.1.4), Entity Framework Core 10.0.14, PostgreSQL |
| Database của integration test | `qtkd_test` (riêng, xóa và tạo lại bằng migration mỗi lần chạy) |

Lưu ý: số 71 tính cả từng dòng dữ liệu của các test dạng `[Theory]` (ví dụ một test kiểm tra 8 trạng thái được tính là 8 test).

## 2. Cách chạy

```powershell
# Toàn bộ
dotnet test

# Chỉ unit test (không cần PostgreSQL)
dotnet test --filter "FullyQualifiedName~Unit"

# Chỉ integration test (cần database kiểm thử qtkd_test)
dotnet test --filter "FullyQualifiedName~Integration"

# Lưu kết quả dạng TRX
dotnet test --logger "trx;LogFileName=test-results.trx" --results-directory docs/test-results
```

Cấu hình database kiểm thử: xem mục "Kiểm thử" trong `README.md`. Tên database bắt buộc chứa chữ `test`, nếu không bộ test từ chối chạy để tránh xóa nhầm dữ liệu thật.

## 3. Cách kiểm tra

| Loại | Thư mục | Nội dung | Cần PostgreSQL |
|---|---|---|---|
| Unit test | `tests/QTKD.Tests/Unit` | Quy tắc CTR-01/02/03 (logic thuần) và `ContractService` trên database in-memory | Không |
| Integration test | `tests/QTKD.Tests/Integration` | Gọi API thật qua `WebApplicationFactory` vào PostgreSQL `qtkd_test` | Có |

Mỗi integration test kiểm tra **đủ ba thứ**: mã HTTP, nội dung JSON trả về và dữ liệu thực tế trong database sau khi gọi API (không kết luận thành công chỉ vì API trả 200).

## 4. Bảng TC01–TC08

| Test | Dữ liệu / thao tác | Kết quả mong đợi | Cách chạy | Test method | Pass/Fail |
|---|---|---|---|---|---|
| TC01 | Tạo Customer hợp lệ | Tạo được (201), đọc lại được qua API và từ DB | Tự động (integration) | `CustomerApiTests.TC01_CreateValidCustomer_ReturnsCreated_AndIsPersisted` | **Pass** |
| TC02 | Tạo Customer thiếu thông tin bắt buộc | Bị từ chối (400), có thông báo lỗi, không lưu gì | Tự động (integration) | `CustomerApiTests.TC02_CreateCustomerWithoutName_ReturnsBadRequest_AndNothingIsSaved` | **Pass** |
| TC03 | Sửa Contract ở Draft | Thành công (200), dữ liệu được lưu | Tự động (integration + unit) | `ContractApiTests.TC03_UpdateDraftContract_ReturnsOk_AndIsPersisted`; `ContractServiceTests.TC03_Unit_UpdateDraftContract_Succeeds` | **Pass** (2/2) |
| TC04 | Sửa Contract ở PendingApproval | Bị từ chối (409, CTR-01), dữ liệu giữ nguyên | Tự động (integration + unit) | `ContractApiTests.TC04_UpdateContractInPendingApproval_ReturnsConflict_AndDataUnchanged`; `ContractServiceTests.TC04_Unit_UpdateContractInPendingApproval_IsRejected_AndDataUnchanged` | **Pass** (2/2) |
| TC05 | Submit Contract thiếu điều kiện CTR-02 | Bị từ chối (400), vẫn là Draft, không có approval và lịch sử | Tự động (integration + unit) | `SubmitContractApiTests.TC05_SubmitContractMissingCtr02Conditions_ReturnsBadRequest_AndStaysDraft`; `ContractServiceTests.TC05_Unit_SubmitMissingContent_IsRejected_AndStaysDraft` | **Pass** (2/2) |
| TC06 | Submit Contract hợp lệ | Chuyển sang PendingApproval; có 1 approval Pending và 1 dòng lịch sử | Tự động (integration + unit) | `SubmitContractApiTests.TC06_SubmitValidContract_ReturnsOk_AndMovesToPendingApproval`; `ContractServiceTests.TC06_Unit_SubmitValidContract_MovesToPendingApproval_WithApprovalAndHistory` | **Pass** (2/2) |
| TC07 | Thử chuyển Draft thẳng Approved | Bị từ chối (CTR-03) | Tự động (unit). Hệ thống không có API chuyển trạng thái trực tiếp nên kiểm tra ở lớp quy tắc | `ContractRulesTests.TC07_CTR03_DraftToApproved_IsRejected` | **Pass** |
| TC08 | Submit lặp một Contract | Không tạo thêm kết quả xử lý trùng | Tự động (integration + unit) | `SubmitContractApiTests.TC08_SubmitTwiceWithSameKey_ReturnsSameApproval_AndCreatesOnlyOne`; `SubmitContractApiTests.TC08_SubmitTwiceWithDifferentKey_ReturnsConflict_AndCreatesOnlyOne`; `ContractServiceTests.TC08_Unit_SubmitTwiceWithSameKey_...`; `ContractServiceTests.TC08_Unit_SubmitTwiceWithDifferentKey_...` | **Pass** (4/4) |

**Kết quả TC01–TC08: 8/8 đạt.**

## 5. Test bổ sung (ngoài TC01–TC08)

| Test | Mục đích | Cách chạy | Số test | Pass/Fail |
|---|---|---|---|---|
| `SubmitContractApiTests.TC09_ConcurrentSubmits_CreateExactlyOneApproval` | 8 yêu cầu gửi đồng thời chỉ tạo đúng 1 phiên phê duyệt, 7 yêu cầu còn lại nhận 409 (AC05, TC06 trong slide) | Tự động (integration) | 1 | **Pass** |
| `ContractApiTests.TC10_MalformedBody_ReturnsUnifiedErrorFormat` | Body sai định dạng phải trả lỗi theo mẫu thống nhất `{code, message, errors}` | Tự động (integration) | 1 | **Pass** (sau khi sửa, xem mục 8) |
| `AC01_*` (integration và unit), `SubmitWithoutRoleHeader_*` | Không có quyền hoặc thiếu vai trò thì 403, không đổi hợp đồng (AC01) | Tự động | 3 | **Pass** |
| `AC02_Unit_*` | Gửi khi không ở Draft (Approved, Rejected, Active) bị từ chối, trạng thái giữ nguyên (AC02) | Tự động (unit) | 3 | **Pass** |
| `FullFlow_FailThenFixThenSubmit_Succeeds` | Luồng như video demo: thiếu dữ liệu, bổ sung, gửi lại thành công | Tự động (integration) | 1 | **Pass** |
| `SubmitContractOfInactiveCustomer_*` | CTR-02: khách hàng không còn hoạt động thì không gửi được | Tự động (integration) | 1 | **Pass** |
| `SubmitUnknownContract_*`, `GetUnknown*_ReturnsNotFound` | 404 cho hợp đồng, khách hàng, bảng giá không tồn tại | Tự động (integration) | 4 | **Pass** |
| `CreateContract*`, `CreateCustomerWithDuplicateName_*` | Tạo hợp đồng ở Draft, chặn trùng số hợp đồng, trùng tên khách hàng, khách hàng không tồn tại, ngày bắt đầu lớn hơn ngày kết thúc | Tự động (integration) | 5 | **Pass** |
| `ContractRulesTests` (CTR-01, CTR-03) | Xem mục 6 | Tự động (unit) | 22 (ngoài TC07) | **Pass** |
| `Ctr02ValidationTests` | Xem mục 6 | Tự động (unit) | 10 | **Pass** |
| `PricingApiTests` | PRC-01, PRC-02, tạo bảng giá, thêm và đọc PriceItem, đơn giá không hợp lệ, trùng mã dịch vụ | Tự động (integration) | 6 | **Pass** |

Ghi chú về TC09: log của lần chạy cho thấy 7 yêu cầu bị chỉ mục `UX_Approval_OnePendingPerContract` từ chối (mã lỗi PostgreSQL 23505) và API đã chuyển thành phản hồi 409. Các dòng `fail:` trong log là hành vi mong đợi của test này, không phải test lỗi.

## 6. Đối chiếu quy tắc nghiệp vụ

Mỗi quy tắc có ít nhất một trường hợp đúng và một trường hợp sai.

| Quy tắc | Trường hợp đúng (được chấp nhận) | Trường hợp sai (bị từ chối) | Test |
|---|---|---|---|
| CTR-01: chỉ sửa ở Draft hoặc RevisionRequested | Draft, RevisionRequested có thể sửa | 8 trạng thái còn lại không sửa được; sửa hợp đồng PendingApproval trả 409 | `ContractRulesTests.CTR01_*`, TC03, TC04 |
| CTR-02: điều kiện gửi trình duyệt | Hợp đồng hợp lệ không có lỗi; ngày bắt đầu bằng ngày kết thúc được chấp nhận | Khách hàng không hoạt động; ngày bắt đầu sau ngày kết thúc; thiếu nội dung (null, rỗng, khoảng trắng); thiếu nhóm dịch vụ; thiếu cùng lúc nhiều điều kiện | `Ctr02ValidationTests.*`, TC05 |
| CTR-03: không chuyển Draft thẳng Approved | Draft sang PendingApproval | Draft sang 8 trạng thái khác (gồm Approved); các trạng thái cuối (Rejected, Cancelled, Expired) không chuyển đi đâu được | `ContractRulesTests.CTR03_*`, TC07 |
| PRC-01: bảng giá gắn khách hàng hoặc nhóm dịch vụ | Tạo bảng giá có nhóm dịch vụ | Không gắn gì cả thì 400 | `PricingApiTests.PRC01_*` |
| PRC-02: ngày bắt đầu không lớn hơn ngày kết thúc | Bảng giá có khoảng ngày hợp lệ | Ngày bắt đầu sau ngày kết thúc thì 400 | `PricingApiTests.PRC02_*` |

Đối chiếu tiêu chí chấp nhận US03:

| Tiêu chí | Test |
|---|---|
| AC01: không có quyền | `AC01_SubmitWithoutPermission_*` (integration, unit), `SubmitWithoutRoleHeader_*` |
| AC02: sai trạng thái | `AC02_Unit_*`, `TC08_*DifferentKey_*` |
| AC03: không đạt CTR-02 | `TC05_*` (integration, unit), `SubmitContractOfInactiveCustomer_*` |
| AC04: đủ điều kiện thì chuyển PendingApproval | `TC06_*` (integration, unit), `FullFlow_*` |
| AC05: gửi lặp và đồng thời | `TC08_*` (4 test), `TC09_*` |

## 7. Phần chưa tự động hóa

| Nội dung | Lý do | Kiểm tra thủ công |
|---|---|---|
| Đăng nhập và phân quyền thật | Chưa xây dựng, giả lập bằng header `X-User-Role` | Gọi API với các giá trị `X-User-Role` khác nhau (video demo tuần 06) |
| Quy trình Approval đầy đủ (rà soát, phê duyệt, từ chối) | Ngoài phạm vi Phase 2, chỉ mô phỏng bằng `ApprovalInstances` | Không áp dụng |
| Kiểm tra tệp đính kèm bắt buộc của CTR-02 | Phase 1 chưa xác nhận danh sách tệp (câu hỏi Q2) | Không áp dụng |
| Chuyển trạng thái trực tiếp Draft sang Approved qua API | Hệ thống không có API này | TC07 kiểm tra ở lớp quy tắc |

## 8. Lỗi đã tìm thấy

### Lỗi 1: Body sai định dạng không trả lỗi theo mẫu thống nhất

> Mục này mô tả theo cơ chế mặc định của ASP.NET Core và test TC10. Nếu bạn còn thông báo assert của lần chạy đỏ đầu tiên, dán vào dòng "Kết quả thực tế" cho chính xác.

| Mục | Nội dung |
|---|---|
| Test phát hiện | `ContractApiTests.TC10_MalformedBody_ReturnsUnifiedErrorFormat` |
| Cách tái hiện | `POST /api/contracts` với body JSON hợp lệ nhưng `customerId` không phải GUID, ví dụ `"customerId": "khong-phai-guid"` |
| Kết quả thực tế (trước khi sửa) | API trả 400 theo mẫu `ProblemDetails` mặc định của ASP.NET Core, không có trường `code`, nên test báo response lỗi thiếu trường `code` |
| Kết quả mong đợi | 400 với `{ "code": "INVALID_REQUEST", "message": "...", "errors": [...] }`, đúng mẫu lỗi thống nhất ghi trong `docs/api.md` |
| Nguyên nhân | Thuộc tính `[ApiController]` tự động kiểm tra model và trả lỗi trước khi request vào controller, nên `GlobalExceptionHandler` không xử lý trường hợp này |
| Cách sửa | Cấu hình `ConfigureApiBehaviorOptions` với `InvalidModelStateResponseFactory` trong `Program.cs` để trả đúng mẫu `{ code, message, errors }` |
| Commit thêm test (đỏ) | 456ab3f Week 07: add unit and integration tests (TC10 fails: error format inconsistent) |
| Commit sửa lỗi | ef81550 (HEAD -> main) Fix: return unified error format for malformed request bodies (TC10) |
| Kết quả sau khi sửa | TC10 đạt, toàn bộ 71 test đạt (TRX ngày 12/11/2026) |

### Cải tiến phòng ngừa (không phải lỗi đã quan sát)

- Trong `ContractService.SubmitAsync`, khối `catch` bắt thêm lỗi deadlock của PostgreSQL (mã 40P01) ngoài lỗi vi phạm ràng buộc duy nhất (23505). Lý do: khi nhiều yêu cầu cùng cập nhật một hợp đồng, database có thể hủy một giao dịch do deadlock thay vì báo trùng. Trong lần chạy TC09 chưa quan sát thấy deadlock.

## 9. Lỗi và giới hạn còn tồn tại

Chưa phát hiện lỗi nào còn mở qua bộ test hiện tại. Các giới hạn thiết kế đã biết (không phải lỗi, nhưng cần xác nhận ở Phase 1):

1. Danh sách nội dung và tệp bắt buộc của CTR-02 chưa được xác nhận. Bản hiện tại chỉ yêu cầu `Content` và `ServiceGroup` không trống (Q2).
2. Ai được gửi hợp đồng chưa rõ. Bản hiện tại cho mọi người dùng có vai trò `Sales` (Q1).
3. Chỉ gửi được từ `Draft`. Gửi lại từ `RevisionRequested` chưa triển khai.
4. Quan hệ giữa trạng thái `Submitted` (sơ đồ Phase 1) và `PendingApproval` (US03) cần xác nhận.
5. Tên khách hàng được coi là duy nhất (không phân biệt hoa thường) vì chưa có mã khách hàng hoặc mã số thuế.
6. Phân quyền chỉ giả lập bằng header, chưa có đăng nhập thật.

## 10. Demo cuối: clone, cấu hình, migration, chạy API, chạy test

Điền sau khi thực hiện trên bản clone mới (máy khác hoặc thư mục khác):

- [ ] Clone repository
- [ ] Cấu hình User Secrets cho API và cho project test theo README
- [ ] `dotnet ef database update --project src/QTKD.Api`
- [ ] `dotnet run --project src/QTKD.Api` và gọi thử API
- [ ] `dotnet test` đạt toàn bộ
- [ ] Những chỗ README chưa rõ đã được sửa: **[điền, hoặc ghi "Không có"]**

## 11. Minh chứng

| Minh chứng | Vị trí |
|---|---|
| File TRX của lần chạy cuối (71/71 đạt) | `docs/test-results/test-results.trx` |
| Mã nguồn test | `tests/QTKD.Tests/` |
| Tài liệu API | `docs/api.md` |

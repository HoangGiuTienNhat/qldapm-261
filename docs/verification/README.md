# Kết quả kiểm tra bản MVP local

Ngày02/11/2026. Chỉ ghi những kiểm tra đã thực hiện; không suy diễn thành nghiệm thu toàn bộ QC.

| Kiểm tra | Kết quả |
|---|---|
| Backend build/publish trong SDK.NET10 container | Đạt |
| Frontend Next16.4.4 production build và TypeScript | Đạt |
| npm dependency audit lúc tạo lockfile | 0 vulnerabilities tại thời điểm kiểm tra |
| Unit tests xUnit | 13/13 đạt: transition, tổng SERVED snapshot, CASH thiếu tiền, transfer thiếu reference, ZERO, version stale |
| HTTP smoke trên PostgreSQL thật qua gateway | Đạt; [smoke-result.json](smoke-result.json) chứa132 kiểm tra ở lần chạy ghi nhận |
| Dine-in + gọi thêm duyệt + CASH + đóng lượt/revoke khách | Đạt trong smoke |
| Manager/admin tự duyệt, reject/withdraw, retry key, RBAC403, món hết409 | Đạt trong smoke |
| BANK_TRANSFER và ZERO sau hủy món đã nấu; chặn hủy toàn đơn sau nấu | Đạt trong smoke |
| Backend/migrate/Frontend/gateway trạng thái cuối | Backend/frontend/gateway Up, PostgreSQL healthy, migrate Exited(0); health/live, health/ready và OpenAPI HTTP200 |
| Migration ban đầu rồi migration integrity trên DB hiện có | Đã áp dụng thành công, seed chạy lại không reset dữ liệu |
| UI browser: đăng nhập admin, sơ đồ bàn, menu, thêm món vào giỏ | Đạt ở kiểm tra thao tác trực tiếp |
| UI responsive: desktop, mobile390px, tablet768px | Đã xem ảnh; không tràn ngang document ở hai viewport nhỏ, nav mobile có vùng cuộn riêng |

[Ảnh desktop](desktop-menu.png) · [Ảnh mobile](mobile-menu.png) · [Ảnh tablet](tablet-menu.png).

Smoke tạo dữ liệu demo có thanh toán/hủy và tăng version một số tài nguyên. Các lần sửa lỗi ban đầu có thể để lại lượt trống, có thể hủy bằng UI với lý do; không tự xóa dữ liệu vì có thể người dùng đang thử cùng lúc.

Chưa chạy: benchmark50 thiết bị, UAT5 người, bộ race/concurrency stress đầy đủ, restore drill. Chưa tuyên bố mọi QC ở SSOT đã pass. [Giới hạn triển khai](../../ADR/0008-mvp-local-va-quyet-dinh-trien-khai.md).

## QR cố định — cập nhật03/11/2026

| Kiểm tra mới | Kết quả |
|---|---|
| HTTP QR: đọc lặp không đổi mã, join không mã, hai thiết bị cùng đơn, cookie lặp giữ phiên, sai bàn, đóng/mở lại, thay QR, inactive | 72 kiểm tra đạt — [qr-result.json](qr-result.json) |
| Hồi quy toàn luồng đặt món/duyệt/bếp/serve/CASH/BANK_TRANSFER/ZERO | 131 kiểm tra đạt ở lần chạy mới — [smoke-result.json](smoke-result.json); số request phụ thuộc có cần đổi availability tạm khi test không |
| Unit backend sau thay đổi entity/migration | 13/13 đạt trong Docker |
| Unit frontend copy đúng full URL, clipboard bị từ chối, clipboard không tồn tại | 3/3 đạt, `docker build --target tests -t restaurant-ordering-frontend-tests ./Frontend` |
| Giải mã PNG320×320 lấy từ UI thật | Khớp chính xác URL hiển thị — [qr-image-result.json](qr-image-result.json) |
| Recreate backend/frontend, chạy lại migration/seed rồi đọc QR | Token/URL bàn B30 không đổi — [qr-persistence-result.json](qr-persistence-result.json) |
| UI mở QR, ảnh/link/input cùng URL, tải PNG | Đã xem trực tiếp — [hộp thoại QR](fixed-table-qr-dialog.png) |
| UI mở lượt B30 rồi mở link QR vào menu khách không hỏi mã | Đạt — [menu khách](fixed-qr-guest-menu.png); lượt trống tạo để thử đã được hủy riêng, không tác động bàn khác |

Trong IAB, nút copy báo thành công từ navigator.clipboard, nhưng virtual clipboard của công cụ browser trả rỗng khi đọc/dán nên không ghi nhận OS clipboard E2E là đạt. Logic gửi đúng toàn URL và fallback Ctrl+C được kiểm bằng3 unit test; input/link/ảnh được đối chiếu và PNG giải mã thật. Đây là giới hạn của kiểm chứng automation, không bỏ qua fallback thủ công.

Môi trường helper giải mã ảnh: node24 container tạm, jsqr1.4.0 + pngjs7.0.0; script `scripts/check-qr-image.cjs`. Không xóa image Docker, không dùng prune hoặc reset volume. Mã token trong JSON log HTTP được che; ảnh QR là dữ liệu demo localhost có chủ đích theo yêu cầu tính năng.

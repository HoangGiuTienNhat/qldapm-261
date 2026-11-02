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

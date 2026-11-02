# Danh mục và quản trị SSOT

Phiên bản hiện hành: **1.2 · 03/11/2026 · đã triển khai MVP localhost**. Các xác nhận trực tiếp của chủ dự án trong chat ngày này đã được tích hợp. Những giả định thiết kế còn lại được đánh dấu tại [ADR-0007](../ADR/0007-truy-vet-va-xac-nhan.md), không coi là phản hồi của khách hàng.

## Một phân hệ, một tài liệu

| Chủ sở hữu quy tắc | File | Đầu ra chính |
|---|---|---|
| P1.1 | [Tài khoản](P1.1-tai-khoan-va-phan-quyen.md) | Xác thực và RBAC |
| P1.2 | [Thực đơn](P1.2-thuc-don-va-tinh-trang-mon.md) | Menu, giá, còn/hết |
| P1.3 | [Bàn và lượt khách](P1.3-ban-va-luot-khach.md) | Phiên bàn và quyền khách |
| P1.4 | [Đặt món](P1.4-dat-mon-va-dieu-chinh-don.md) | Đơn, đợt gọi, phê duyệt, sửa/hủy |
| P1.5 | [Bếp](P1.5-xu-ly-mon-tai-bep.md) | Tiến độ chế biến |
| P1.6 | [Phục vụ](P1.6-theo-doi-va-phuc-vu.md) | Bàn giao món |
| P1.7 | [Thanh toán](P1.7-thanh-toan-va-hoan-tat.md) | Chốt tiền và kết thúc lượt |

Quy tắc phê duyệt/sửa/hủy chỉ định nghĩa tại P1.4; KDS tham chiếu. Bảng, token và đóng lượt thuộc P1.3; P1.7 chỉ gọi thao tác đóng lượt trong giao dịch thanh toán. Từ điển schema hiện hành nằm ở [docs/db](../docs/db/README.md), mỗi bảng một file; [ADR-0003](../ADR/0003-database.md) giải thích quyết định cấu trúc. Các SSOT chỉ dẫn bảng/tài liệu, không chép lại schema.

## Quy trình thay đổi bắt buộc

1. Ghi yêu cầu mới, nguồn, quy tắc bị ảnh hưởng và mã issue.
2. Cập nhật **đúng file SSOT** trước hoặc trong cùng PR với code; tăng revision, ghi ngày và lý do ở lịch sử file.
3. Nếu đổi cách thực hiện, cập nhật/thêm ADR; nếu đổi schema, thêm migration mới, không sửa migration đã áp dụng.
4. Sửa API/OpenAPI, UI, test và kế hoạch liên quan trong cùng PR. Liên kết mã BR/QC tương ứng.
5. Reviewer kiểm tra SSOT ↔ API ↔ migration ↔ code ↔ test ↔ docs/db và ADR/GUIDE liên quan. Chạy `python scripts/generate_db_docs.py --check` để phát hiện tài liệu/nguồn backend lệch; review ngữ nghĩa vẫn bắt buộc. Chưa đồng bộ thì thay đổi chưa hoàn tất.
6. Nếu code đang lệch SSOT, mở issue xác định bug hay thay đổi được chấp thuận; không tự coi code là yêu cầu mới.

Hiện bổ sung `Backend/Restaurant.Api/openapi.json` làm hợp đồng giao tiếp sinh từ catalog transport và đối chiếu endpoint backend; đây không phải nguồn nghiệp vụ thay thế SSOT. Có script kiểm route/catalog và sinh API docs; chưa có CI/PR template tự kiểm toàn bộ SSOT. Quy trình review SSOT/code vẫn bắt buộc.

## Chuẩn cấu trúc

Mỗi file có nhận diện, bảng mô tả sản phẩm theo mẫu P1.4 gốc, phạm vi, actor, quy tắc BR, luồng, hợp đồng dữ liệu/API, UI/lỗi, tiêu chí QC có cách kiểm tra, phụ thuộc và lịch sử. Trạng thái “đặc tả baseline” không có nghĩa phần mềm đã hoàn thành.

QR hiện hành: [P1.3 QR cố định](P1.3-ban-va-luot-khach.md) và [ADR-0009](../ADR/0009-qr-co-dinh-tai-ban.md) thay mã8 chữ số bằng quét/mở link trực tiếp, có ảnh QR và copy URL.

# Danh mục Architecture Decision Records

Ngày baseline: 02/11/2026. Trạng thái chung: **lựa chọn thiết kế để triển khai, chưa hiện thực**. Các xác nhận nghiệp vụ của chủ dự án được ghi nguồn riêng ở ADR-0007; không đồng nghĩa toàn bộ ADR đã được khách hàng nghiệm thu.

| ADR | Quyết định / nội dung | Trạng thái |
|---|---|---|
| [0001](0001-tech-stack.md) | C#/.NET 10, Next.js, PostgreSQL; bằng chứng máy và so sánh | Đã lựa chọn cho baseline |
| [0002](0002-kien-truc-va-cau-truc.md) | Modular monolith và cấu trúc Backend/Frontend | Đã lựa chọn cho baseline |
| [0003](0003-database.md) | Schema, ERD, trường và ràng buộc | Thiết kế baseline |
| [0004](0004-docker-va-migration.md) | Docker, thứ tự khởi động, EF migration, backup/restore | Thiết kế baseline |
| [0005](0005-api-va-nhat-quan.md) | API, phiên, concurrency, retry, polling | Thiết kế baseline |
| [0006](0006-implementation-plan.md) | Roadmap, WBS, AC, kiểm thử và cổng hoàn tất | Kế hoạch, chưa thực hiện |
| [0007](0007-truy-vet-va-xac-nhan.md) | Ma trận nguồn và xử lý điểm mâu thuẫn | Ghi nhận baseline |
| [0009](0009-qr-co-dinh-tai-ban.md) | QR cố định có ảnh/link/copy, bỏ mã tham gia, migration003 | Đã triển khai |
| [0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) | Xác nhận1A–7A/ASAP, MVP localhost, cách triển khai và giới hạn thực tế | Đã triển khai |

ADR giải thích **vì sao và thực hiện thế nào**. [SSOT](../SSOT/README.md) là nguồn duy nhất của yêu cầu feature. Bản MVP đã có endpoint; hợp đồng/payload thực tế ở [docs/api](../docs/api/README.md). ADR0001–0007 giữ bối cảnh thiết kế ban đầu; ADR0008 ghi các điểm superseded và giới hạn, [trạng thái triển khai](../docs/IMPLEMENTATION-STATUS.md) ghi việc đã làm.

Khi quyết định thay đổi đáng kể, thêm ADR mới ghi “Supersedes ADR-xxxx” và sửa trạng thái ADR cũ; không xóa lý do lịch sử. Đồng thời cập nhật SSOT liên quan trong cùng PR. Sửa lỗi trình bày được cập nhật trực tiếp kèm ngày.

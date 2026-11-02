# Hệ thống quản lý đặt món nhà hàng

CO3011 · Nhóm 5 · Baseline tài liệu ngày **02/11/2026**.

**Trạng thái:** đã phân tích và thiết kế; chưa triển khai ứng dụng, Dockerfile, Compose hay migration thực thi. `Backend/` và `Frontend/` giữ nguyên ranh giới triển khai.

## Bắt đầu đọc

1. [Danh mục SSOT và quy trình đồng bộ](SSOT/README.md).
2. [Quyết định stack và khảo sát máy](ADR/0001-tech-stack.md).
3. [Kiến trúc và cấu trúc dự án](ADR/0002-kien-truc-va-cau-truc.md).
4. [Thiết kế database, từ điển dữ liệu và ràng buộc](ADR/0003-database.md).
5. [Migration và Docker Compose dự kiến](ADR/0004-docker-va-migration.md).
6. [API, bảo mật và xử lý đồng thời](ADR/0005-api-va-nhat-quan.md).
7. [Lộ trình, task và acceptance criteria](ADR/0006-implementation-plan.md).
8. [Nguồn yêu cầu, mâu thuẫn và xác nhận](ADR/0007-truy-vet-va-xac-nhan.md).

## Phân hệ

| Mã | Đặc tả duy nhất | Phạm vi |
|---|---|---|
| P1.1 | [Tài khoản và phân quyền](SSOT/P1.1-tai-khoan-va-phan-quyen.md) | S1 |
| P1.2 | [Thực đơn và tình trạng món](SSOT/P1.2-thuc-don-va-tinh-trang-mon.md) | S2, S9 |
| P1.3 | [Bàn và lượt khách](SSOT/P1.3-ban-va-luot-khach.md) | S3 |
| P1.4 | [Đặt món và điều chỉnh đơn](SSOT/P1.4-dat-mon-va-dieu-chinh-don.md) | S4, S5, S8, S11 |
| P1.5 | [Xử lý món tại bếp](SSOT/P1.5-xu-ly-mon-tai-bep.md) | S6 |
| P1.6 | [Theo dõi và xác nhận phục vụ](SSOT/P1.6-theo-doi-va-phuc-vu.md) | S7, bàn giao S5 |
| P1.7 | [Thanh toán và hoàn tất đơn](SSOT/P1.7-thanh-toan-va-hoan-tat.md) | S10 |

Stack lựa chọn: **C# / ASP.NET Core .NET 10 + EF Core/Npgsql + PostgreSQL 16; React / Next.js App Router / TypeScript; Docker Compose cho toàn bộ môi trường**. Chi tiết lý do và giới hạn bằng chứng nằm trong ADR-0001.

`temp-docs/` là đầu vào lịch sử, không phải đặc tả đang có hiệu lực. Không tạo bản mô tả phân hệ thứ hai ngoài SSOT. ADR ghi cách thực hiện và lý do; quy tắc nghiệp vụ được sở hữu bởi từng SSOT.

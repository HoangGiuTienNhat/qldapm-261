# Hướng dẫn dự án Bếp Nhà

Thư mục này là nơi tập trung hướng dẫn vận hành, phát triển và mẹo sử dụng. Quy tắc nghiệp vụ nằm trong [SSOT](../SSOT/README.md); quyết định kỹ thuật nằm trong [ADR](../ADR/README.md).

| Bạn cần làm gì? | Đọc tài liệu |
|---|---|
| Chạy Docker lần đầu, chạy các lần sau, dừng và dựng lại | [DOCKER.md](DOCKER.md) |
| Môi trường hoàn toàn mới nhưng giữ dữ liệu và image cũ | [DOCKER.md — môi trường sạch](DOCKER.md#7-chạy-môi-trường-mới-hoàn-toàn-mà-không-xóa-dữ-liệu-cũ) |
| Dùng ảnh QR cố định, link bàn và vai khách | [QR-TAI-BAN.md](QR-TAI-BAN.md) |
| Thử API bằng Postman | [Postman](../docs/api/POSTMAN.md) và [từng endpoint](../docs/api/README.md) |
| Tìm luồng chức năng và các giới hạn bản hiện tại | [README](../README.md), [trạng thái triển khai](../docs/IMPLEMENTATION-STATUS.md) |

## Mẹo cho người sử dụng

- Mỗi profile trình duyệt chia sẻ cookie giữa các tab. Để thử nhiều nhân viên đồng thời, dùng profile/browser/ẩn danh khác nhau.
- F12 → Ctrl+Shift+M mở Device Toolbar để xem mobile/tablet; thao tác này không biến localhost thành địa chỉ truy cập được từ điện thoại khác.
- Khi request chưa rõ kết quả do mất mạng, kiểm tra đơn hiện tại hoặc gửi lại cùng Idempotency-Key; không bấm tạo đơn mới để thử vận may.
- “Chờ phê duyệt” chưa phải món đã vào bếp. Thanh toán chỉ chốt khi xử lý hết các đợt chờ và bàn giao hết món.

## Mẹo cho developer

- Cập nhật SSOT, migration và API docs cùng thay đổi feature. Script `python scripts/generate_api_docs.py` kiểm catalog khớp route backend.
- Unit test: `docker build --target tests -t restaurant-ordering-tests ./Backend`.
- Smoke HTTP: `python scripts/smoke.py`; script tạo dữ liệu test có thanh toán/hủy, không tự xóa dữ liệu người dùng.
- `.env` chứa cấu hình local, không stage/commit. Stage theo danh sách file hoặc folder đúng phạm vi; tránh `git add .` khi có tài liệu riêng trong `temp-docs`.
- Theo quy ước chủ dự án: hoàn tất từng feature thì stage và commit cục bộ với mô tả thay đổi/kiểm chứng; không tự push.

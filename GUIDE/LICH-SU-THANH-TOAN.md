# Xem lịch sử thanh toán

1. Đăng nhập tài khoản có role **ADMIN** → **Lịch sử thanh toán**, hoặc mở `http://localhost:3000/admin/payments`.
2. Nhập mã đơn, biên nhận, bàn, mã/tên khách mang về hoặc tên/username người thu; chọn thêm ngày, phương thức, loại đơn và người nhận khi cần.
3. Bấm **Áp dụng bộ lọc**. Ngày tính theo giờ Việt Nam UTC+07:00, bao gồm toàn bộ ngày kết thúc. Bỏ trống ngày để xem mọi thời điểm.
4. Dùng **Trước/Sau** để xem tiếp; 20 đơn/trang, không giới hạn tổng số đơn lịch sử. **Làm mới** cập nhật dữ liệu mới; **Xóa bộ lọc** trở về toàn bộ lịch sử.
5. Bấm **Xem** để đọc thông tin thanh toán, người nhận, thời điểm, tiền khách đưa/trả lại hoặc nội dung chuyển khoản, toàn bộ đợt gọi món và ghi chú. Dòng hủy/từ chối/rút được giữ để tra cứu nhưng không tính tiền.

Tên/username người thu được lưu tại lúc nhận tiền. Đổi tên hoặc khóa tài khoản sau đó không sửa thông tin đã lưu. Tên và đơn giá món cũng giữ theo lúc gọi. Nếu dữ liệu dev cũ thiếu snapshot tên, màn hình ghi rõ “Chưa ghi nhận tên”, vẫn hiển thị ID người nhận; không suy đoán tên lịch sử.

Chỉ ADMIN xem được danh sách lịch sử toàn hệ thống. Thu ngân/manager vẫn xem biên nhận trong luồng vận hành theo quyền đã có. Không chỉnh sửa/xóa hoặc mở lại đơn đã thanh toán từ màn hình này.

Developer: [SSOT P1.7](../SSOT/P1.7-thanh-toan-va-hoan-tat.md), [ADR-0010](../ADR/0010-lich-su-thanh-toan-admin.md), [API và payload](../docs/api/README.md). Chạy `python scripts/payment_history_smoke.py` để tạo dữ liệu kiểm thử; script không dọn dữ liệu hiện có. Kết quả kiểm thử là dữ liệu giả, không phải doanh thu thực.

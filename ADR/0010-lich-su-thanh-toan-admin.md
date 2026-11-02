# ADR-0010 — Lưu và tra cứu lịch sử thanh toán

03/11/2026 · Đã triển khai. Bổ sung ADR-0003/0008. Yêu cầu duy nhất của feature nằm ở [SSOT P1.7](../SSOT/P1.7-thanh-toan-va-hoan-tat.md).

## Bối cảnh và quyết định

Admin cần xem lại mọi đơn đã thanh toán, thời điểm nhận tiền và người thực nhận. Danh sách đơn vận hành giới hạn 200 dòng không phù hợp cho lịch sử lâu dài. Thông tin nhân viên hiện tại có thể đổi tên hoặc bị khóa, nên không dùng join hồ sơ hiện tại để thay cho thông tin người nhận lúc thu tiền.

Giữ `payments` là một bản ghi duy nhất cho mỗi đơn; không tạo bảng invoice trùng dữ liệu. Migration `202611030004_PaymentRecipientHistory` thêm `received_by_username_snapshot varchar(64)` và `received_by_display_name_snapshot varchar(120)`. Cùng `received_by_staff_id` và `paid_at`, chúng xác định người nhận và thời điểm nhận. Server lấy thông tin từ phiên nhân viên, đọc hồ sơ với `FOR SHARE` trong transaction; trigger INSERT đối chiếu snapshot với hồ sơ. Client không được chỉ định người thu hoặc thời điểm.

Hai cột cho phép NULL đồng thời để migration áp dụng được lên dữ liệu dev cũ. INSERT mới bắt buộc có cả hai và phải khớp hồ sơ. Theo xác nhận của chủ dự án, không backfill dữ liệu cũ; giao diện ghi rõ khi thiếu snapshot. Việc dọn dữ liệu dev được thực hiện riêng theo yêu cầu, không đưa lệnh xóa vào migration tự chạy.

`paid_at` lưu `timestamptz`, server sinh với độ chính xác millisecond để phản hồi lần đầu, idempotency replay và dữ liệu đọc lại giống nhau. Giao diện và bộ lọc ngày dùng UTC+07:00; ngày kết thúc được chuyển thành cận trên loại trừ của ngày kế tiếp. Không suy thời điểm thanh toán từ thời điểm tạo đơn.

## API và giao diện

- GET `/api/v1/admin/payments`: ADMIN, phân trang mặc định 20, tối đa 100/trang; không giới hạn tổng lịch sử. Sắp `paid_at DESC, id DESC`.
- Lọc theo ngày thanh toán, phương thức, loại đơn, ID người nhận; tìm từ khóa theo mã đơn/biên nhận/bàn/mang về và snapshot username/tên. Escape `%`, `_`, `\` để tìm ký tự nguyên văn, tham số hóa SQL.
- GET `/api/v1/admin/payments/{id}`: ID payment; trả toàn bộ đơn, các đợt và dòng, kể cả hủy/từ chối/rút. Quyền đọc receipt vận hành hiện có được giữ nguyên.
- Admin có mục “Lịch sử thanh toán”, lọc, phân trang, chi tiết chỉ đọc; không mở lại PAID, không sửa hoặc xóa thanh toán.

Index phục vụ lịch sử: `(paid_at DESC,id DESC)`, `(received_by_staff_id,paid_at DESC,id DESC)`, `(method,paid_at DESC,id DESC)`. Trigger bảo vệ payment/order đã có; thêm bảo vệ DELETE các batch/item thuộc đơn PAID, gồm cả dòng không tính tiền. Tên/giá món và mã bàn dùng snapshot đã có.

## Kiểm chứng và giới hạn

25 backend unit tests, 5 frontend unit tests; smoke riêng lịch sử kiểm tra quyền, filter, retry, snapshot sau đổi hồ sơ/menu và toàn bộ các đợt. Hồi quy luồng đặt món/thanh toán bằng HTTP thật. [Bằng chứng](../docs/verification/README.md).

Phân trang offset có thể dịch chuyển khi có thanh toán mới; dùng Làm mới để cập nhật, chưa có snapshot phiên tra cứu. Chưa có xuất Excel, báo cáo doanh thu, hoàn tiền hoặc đối soát ngân hàng. Các biện pháp bất biến nghiệp vụ không chống được thao tác trực tiếp của PostgreSQL superuser.

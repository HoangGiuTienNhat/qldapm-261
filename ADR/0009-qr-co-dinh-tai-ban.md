# ADR-0009 — QR cố định, vào lượt không cần mã

03/11/2026 · Đã triển khai theo lựa chọn mới của chủ dự án. Thay cơ chế mã tham gia trong ADR-0003/0005/0007/0008; [P1.3](../SSOT/P1.3-ban-va-luot-khach.md) là SSOT nghiệp vụ hiện hành.

## Quyết định

Mỗi bàn có một QR cố định. Nhân viên xem được ảnh QR, URL đầy đủ, nút sao chép, mở link gốc và tải PNG. Khách quét/mở link → frontend POST tham gia lượt OPEN của bàn → cấp guest cookie → menu/đơn. Không nhập mã, không tự mở bàn. Nhân viên vẫn mở lượt và gọi hộ như trước. Còn/hết món, phê duyệt gọi thêm, state món, tiền và đóng lượt không thay đổi.

Link bàn cố định có thể được người đã lưu dùng lại khi bàn mở lượt mới; đây là đánh đổi của phương án được chủ dự án chọn. Cookie cũ vẫn bị thu hồi khi đóng lượt, không tự có quyền ở lượt mới; muốn tham gia lại cần thực hiện POST join từ QR/link. Backend vẫn kiểm đúng visit cho mọi API đơn. Không tự join lại trong background sau401.

## Lưu trữ và nâng cấp

Migration `202611020003_FixedTableQr` thêm `restaurant_tables.qr_token_ciphertext bytea`, bỏ join_code_hmac/join_generation của visits và join_generation của guest_sessions. Token ngẫu nhiên256bit, giữ SHA256 hash để lookup; mã hóa token bằng Data Protection scope `restaurant.table-qr.v1` để nhân viên mở lại được QR. Keyring dùng volume auth_keys hiện có, không gửi token cho dịch vụ QR ngoài.

Column ciphertext cho phép NULL trong lúc nâng cấp DB cũ; seed backfill mọi bàn chưa có ciphertext trước khi API start, còn bàn mới luôn có ciphertext. Hash cũ không thể đảo ngược, nên **các bàn từ phiên bản trước được cấp QR mới một lần khi nâng cấp**; in/link cũ cần thay. Những lần seed sau không đổi QR đã khởi tạo. Guest session/order/payment hiện có không bị xóa; session cũ còn hiệu lực ở đúng visit OPEN và chưa revoked/hết hạn, không còn kiểm generation đã bỏ.

GET `/tables/{id}/qr` chỉ đọc, quyền WAITER/MANAGER/ADMIN; trả qrUrl tuyệt đối dựa PublicOrigin, qrPath, token và version. Không mỗi lần xem lại gọi rotate. POST `/qr/rotate` vẫn có cho MANAGER/ADMIN khi thật sự cần vô hiệu link đã in, UI có bước xác nhận. Rotate chặn join mới bằng link cũ nhưng không tự đóng lượt hoặc thu hồi cookie của khách đã tham gia; payment/cancel đóng lượt vẫn thu hồi mọi cookie theo cơ chế cũ.

POST `/guest/join` chỉ cần qrToken. Transaction khóa table→visit để không cấp nhầm quyền khi đóng/mở cạnh tranh. Nếu cookie còn hiệu lực ở chính visit đó, trả cùng phiên; nếu không, cấp phiên mới cho đúng visit hiện tại. Bàn chưa mở409, QR sai/bàn inactive404. Endpoint xoay mã tham gia cũ đã bỏ. Rate limit join60/phút/IP để nhiều khách cùng router có thể tham gia; vẫn có CSRF/Origin validation.

## Frontend và kiểm chứng

`TableQrDialog` dùng qrcode1.5.4 sinh PNG trên browser từ **đúng qrUrl**. Input read-only tự chọn toàn URL, clipboard có fallback Ctrl+C; ảnh và link gốc cùng nguồn dữ liệu. Download là PNG local, không gọi QR service. Xem QR không tự sao chép đè clipboard; người dùng bấm Sao chép URL khi muốn.

QR route tự join khi người dùng mở trang; bàn trống hiện hướng dẫn liên hệ nhân viên. Màn hình tham gia thủ công chỉ có ô URL bàn. Khi cookie hết hiệu lực, hiện thông báo quét lại sau khi có lượt mới, không gọi join vòng lặp.

Kiểm chứng HTTP: [qr-result.json](../docs/verification/qr-result.json), [smoke-result.json](../docs/verification/smoke-result.json). Có kiểm xem QR ổn định, không mã, hai thiết bị/cùng order, reload không nhân phiên, sai bàn, đóng lượt, tái sử dụng QR lượt sau, thay QR có chủ đích và bàn inactive. Dùng unit test hiện có và production build/typecheck. Ảnh kiểm UI và kết quả xem tại [verification](../docs/verification/README.md).

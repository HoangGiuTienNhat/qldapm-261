# Thử API bằng Postman

## Chuẩn bị

1. Chạy `docker compose up -d --build` tại repo. Base URL là `http://localhost:3000`.
2. Import [restaurant.postman_collection.json](restaurant.postman_collection.json). Cookie jar bật cho localhost; không cần bearer token.
3. Local seed: `admin`, `manager`, `waiter`, `kitchen`, `cashier`; mật khẩu mặc định **RestaurantDemo!2026**, hoặc DEMO_PASSWORD nếu bạn đã đổi cấu hình trước seed lần đầu.
4. Collection tự lấy CSRF token trước mỗi lệnh ghi và cấp Idempotency-Key. Gửi GET CSRF không cần đăng nhập. Không tắt CSRF để thử API.

## Biến cần biết

- `actor=staff` để dùng cookie nhân viên; `actor=guest` để dùng cookie khách. Có thể giữ cả ros_staff/ros_guest trong cùng jar; backend phân biệt bằng header X-Actor.
- `orderId`, `batchId`, `itemId`, `visitId`, `tableId`, `menuItemId`, `staffId`, `categoryId`: lấy từ API. Một số ID seed đã điền sẵn.
- `version`: version **của tài nguyên đang sửa**; `orderVersion`: version order khi gửi đợt mới. Không dùng version order làm version item/batch/visit.
- `qrToken`: lấy từ GET tables/{tableId}/qr; không cần joinCode. GET xem lại không thay QR. POST qr/rotate chỉ dùng khi muốn vô hiệu link cũ.
- `retryKey`: để rỗng cho request mới; muốn kiểm thử retry, đặt UUID cố định và giữ nguyên route/payload. Sau khi thử xong xóa biến này trước request khác.

Collection hỗ trợ thử thủ công từng request; **không bấm Run toàn bộ collection theo thứ tự** vì các API reset mật khẩu/khóa user/đổi giá/hủy đơn có chủ đích khác nhau. Lấy response GET mới để cập nhật version đúng trước mỗi thao tác. Có thể sửa trực tiếp biến trong collection.

## Kịch bản A — tại bàn và gọi thêm

1. Login waiter/admin → GET tables → chọn bàn trống → GET tables/{tableId}/qr, lấy qrToken và qrUrl cố định.
2. POST tables/{tableId}/visits, partySize=2 → lấy visitId/orderId; không có mã tham gia.
3. POST guest/join chỉ body qrToken. Đặt actor=guest; GET guest/current và GET orders/{orderId}.
4. POST orders/{orderId}/batches với món seed MON-006: menuItemId `30000000-0000-0000-0000-000000000006`, quantity=2, observedPriceVnd=95000, note="Ít cay, không hành". Đợt đầu APPROVED.
5. GET order → gửi đợt mới dùng orderVersion mới. Đợt này PENDING, chưa vào queue bếp/chưa tính tiền chính thức.
6. actor=staff; login kitchen → GET approval-batches → approve batch với version batch. Bếp không được tự duyệt đợt mình gửi; manager/admin được tự duyệt.
7. GET kitchen/items. Với từng item: accept → prepare → ready; luôn dùng version trả về bước trước.
8. Login waiter → GET serving/items → serve mỗi món.
9. GET order → request-payment với version order mới. Login cashier → GET checkout/orders → payments, CASH/BANK_TRANSFER, amount bằng totalVnd đã chốt.
10. GET receipt; GET tables thấy bàn trống; actor=guest gọi guest/current phải 401.

## Kịch bản B — mang về

Login waiter/cashier/manager/admin → POST takeaway-orders (pickupName="Nguyễn Minh Anh", items như ví dụ) → đi qua bếp và bàn giao → request-payment → payments. Không gửi tableId/visitId. CASHIER chỉ xác nhận SERVED cho takeaway, không cho tại bàn. Thu tiền sau bàn giao theo phạm vi đã chốt.

## Kịch bản C — ngoại lệ

- Cùng key + payload: cùng kết quả, không tạo hai batch/payment. Cùng key khác payload:409.
- Menu hết:409 khi đặt mới; batch chờ duyệt có món vừa hết cũng không duyệt được.
- Item ACCEPTED: khách được giảm quantity/đổi note/hủy; PREPARING/READY: chỉ manager/admin hủy có lý do; SERVED không hủy.
- Batch PENDING phải rút/từ chối/duyệt trước chốt tiền. Order chưa có batch thì hủy lượt trống.
- Toàn món đã nấu bị hủy ngoại lệ: chốt tổng0 rồi payments `{"method":"ZERO","amountVnd":0,"tenderedVnd":null,"transferReference":null,"expectedVersion":<version>}`.

## Tự chạy smoke test

`python scripts/smoke.py` thực hiện HTTP thật bằng các cookie jar riêng, bao gồm thành công và lỗi có chủ đích. Script tạo đơn demo có thanh toán/hủy và ghi kết quả vào `docs/verification/smoke-result.json`; không xóa dữ liệu nhà hàng. Để chỉ dùng Docker, xem lệnh test tại README gốc. Không cần cài thư viện Python ngoài standard library.

## QR cố định mới

Mỗi bàn có một URL cố định qua nhiều lượt. POST guest/join chỉ cần qrToken; bàn chưa mở409, mã QR sai/inactive404. Cùng cookie còn hợp lệ giữ phiên; đóng lượt thu hồi cookie. Guest muốn tham gia lượt mới phải mở/quét lại QR; không đọc được order trước đó. Endpoint xoay join-code đã bỏ; collection hiện có GET QR và không còn biến joinCode. [Hướng dẫn ảnh/copy](../../GUIDE/QR-TAI-BAN.md).

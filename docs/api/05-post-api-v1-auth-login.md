# POST /api/v1/auth/login

Đăng nhập nhân viên. **Quyền:** Công khai.

Hợp đồng test của endpoint đã implement; nghiệp vụ do [SSOT P1.1](../../SSOT/P1.1-tai-khoan-va-phan-quyen.md) sở hữu. Các chuỗi `<...>` trong response là giá trị minh họa cần thay bằng dữ liệu server thật.

## URL và headers

`POST {{baseUrl}}/api/v1/auth/login`

`Content-Type: application/json`

- `X-CSRF-TOKEN: <token lấy từ GET /api/v1/auth/csrf>`
- `Idempotency-Key: <UUID mới cho một ý định; retry giữ nguyên>`

- Cookie jar giữ phiên đăng nhập/tham gia. Với request khách, thêm `X-Actor: guest`.

## Payload đầy đủ cho kịch bản mẫu

```json
{
  "username": "admin",
  "password": "RestaurantDemo!2026"
}
```

## Response thành công — HTTP 200

```json
{
  "id": "10000000-0000-0000-0000-000000000005",
  "username": "admin",
  "displayName": "Quản trị hệ thống",
  "roles": [
    "ADMIN"
  ],
  "isActive": true,
  "mustChangePassword": false,
  "version": 1
}
```

Seed local có admin, manager, waiter, kitchen, cashier cùng mật khẩu DEMO_PASSWORD. Cookie ros_staff là HttpOnly; Postman giữ tự động. Mật khẩu ví dụ chỉ dành cho môi trường demo localhost.

## Cách thử và lỗi cần kiểm tra

1. Xem [hướng dẫn Postman](POSTMAN.md), đăng nhập đúng role hoặc tham gia lượt.
2. Lấy ID thật từ response tạo/danh sách. Với command, lấy version mới nhất của **đúng thực thể** trước khi gửi; số `1` ở ví dụ không luôn là version hiện tại.
3. Gửi payload mẫu. Thành công trả 200; kiểm tra lại GET tài nguyên để xác nhận dữ liệu.
4. Với lệnh ghi: gửi lại cùng key/payload để kiểm tra chống lặp khi API hỗ trợ idempotency. Không đổi key khi chưa rõ kết quả request trước.

Các lỗi chung: 400 JSON/key sai; 401 hết phiên; 403 sai quyền/CSRF; 404 tài nguyên không thuộc quyền; 409 version/trạng thái/mã trùng/món hết; 410 replay hết hạn; 422 trường sai; 429 quá số lần. Lỗi dùng `{type,title,status,code,detail,traceId}`; không có side effect khi transaction thất bại. GET/login/join/logout không lưu idempotency record; command nghiệp vụ mới có replay.

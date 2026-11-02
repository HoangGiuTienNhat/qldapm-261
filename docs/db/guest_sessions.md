# `guest_sessions` — Phiên khách theo lượt

Đối chiếu schema: `202611030004_PaymentRecipientHistory` · review 03/11/2026. [Danh mục DB](README.md) · [ADR-0003](../../ADR/0003-database.md).

## 1. Vai trò và phạm vi

Phiên khách vô danh gắn đúng một visit sau khi quét QR. Nhiều thiết bị cùng bàn có thể có nhiều phiên cùng lượt; không cần customer account.

Quy tắc nghiệp vụ: [P1.1](../../SSOT/P1.1-tai-khoan-va-phan-quyen.md), [P1.3](../../SSOT/P1.3-ban-va-luot-khach.md).

## 2. Các trường

Kiểu, NULL và DEFAULT dưới đây là **định nghĩa tại PostgreSQL**, không phải giá trị mặc định của C#. “Không có” nghĩa là DB không tự cấp giá trị; backend có thể cấp khi tạo entity. PK/UQ/FK/CHECK được ghi đầy đủ ở mục 3.

| Trường | Kiểu PostgreSQL | Cho NULL | DEFAULT tại DB | Ý nghĩa và cách sử dụng |
|---|---|---|---|---|
| `id` | `uuid` | Không | Không có | Định danh UUID của bản ghi, khóa chính. Ứng dụng sinh Guid.NewGuid(); seed có thể dùng UUID cố định. Không có DEFAULT UUID tại DB. |
| `visit_id` | `uuid` | Không | Không có | Lượt duy nhất mà phiên được quyền truy cập, FK RESTRICT đến visits.id. |
| `token_hash` | `bytea` | Không | Không có | SHA-256 của token cookie ros_guest, đúng 32 byte và UNIQUE. Không lưu token rõ. |
| `created_at` | `timestamp with time zone` | Không | Không có | Thời điểm cấp phiên UTC do entity/server gán; DB không DEFAULT. |
| `expires_at` | `timestamp with time zone` | Không | Không có | Hạn phiên phải sau created_at; backend đặt 12 giờ, DB không CHECK đúng 12 giờ. |
| `revoked_at` | `timestamp with time zone` | Có | Không có | Thời điểm thu hồi hoặc NULL nếu chưa thu hồi; nếu có phải >= created_at. Đóng/hủy lượt sẽ thu hồi các phiên. |

## 3. Khóa, quan hệ và ràng buộc tại DB

Các định nghĩa sau lấy từ PostgreSQL catalog sau khi áp dụng đủ chuỗi migration đã ghi ở đầu file. FK `RESTRICT` ngăn xóa bản ghi đang được tham chiếu; không có cascade xóa nghiệp vụ. CHECK không thay cho phân quyền và validation ở service.

Tham chiếu tới: [visits](visits.md).

Được tham chiếu bởi: [audit_events](audit_events.md), [idempotency_records](idempotency_records.md), [order_batches](order_batches.md).

### `guest_sessions_check`

```sql
CHECK (expires_at > created_at)
```

### `guest_sessions_check1`

```sql
CHECK (revoked_at >= created_at)
```

### `guest_sessions_pkey`

```sql
PRIMARY KEY (id)
```

### `guest_sessions_token_hash_check`

```sql
CHECK (octet_length(token_hash) = 32)
```

### `guest_sessions_token_hash_key`

```sql
UNIQUE (token_hash)
```

### `guest_sessions_visit_id_fkey`

```sql
FOREIGN KEY (visit_id) REFERENCES visits(id) ON DELETE RESTRICT
```

## 4. Index và đường truy vấn

Danh sách gồm cả index tự sinh bởi PRIMARY KEY/UNIQUE; không tạo thêm bản trùng. Partial index chỉ áp dụng các dòng khớp WHERE.

```sql
CREATE UNIQUE INDEX guest_sessions_pkey ON public.guest_sessions USING btree (id);
```

```sql
CREATE UNIQUE INDEX guest_sessions_token_hash_key ON public.guest_sessions USING btree (token_hash);
```

```sql
CREATE INDEX ix_guest_visit ON public.guest_sessions USING btree (visit_id);
```

## 5. Trigger và quy tắc liên bảng

Không có user-defined trigger trên bảng này. Các FK/CHECK ở trên vẫn được PostgreSQL thực thi.

## 6. Vòng đời, lưu ý và giới hạn

- Backend cấp phiên tối đa 12 giờ theo service hiện tại. Quyền còn phụ thuộc visit OPEN và revoked_at, nên cookie cũ không dùng được sau đóng lượt.
- QR bàn cố định không phải guest session. Khi có lượt mới cần tham gia lại; migration003 đã bỏ join_generation.
- Audit và đợt món có thể tham chiếu phiên; giữ bản ghi để bảo toàn lịch sử, chỉ thu hồi quyền.

## 7. Cập nhật tài liệu

File được sinh từ catalog metadata và phần giải thích đã review trong `scripts/db_doc_catalog.py`. Khi đổi schema/logic liên quan: thêm migration mới nếu cần, cập nhật catalog giải thích, SSOT và ADR cùng thay đổi; export metadata chỉ đọc rồi sinh lại. Chạy `python scripts/generate_db_docs.py --check` trước commit. Xem [quy trình đồng bộ](README.md#quy-trình-đồng-bộ-bắt-buộc).

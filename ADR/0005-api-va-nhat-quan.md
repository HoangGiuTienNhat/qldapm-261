# ADR-0005 — API, bảo mật, đồng thời và đồng bộ giao diện

> Thay đổi QR hiện hành03/11/2026: [ADR-0009](0009-qr-co-dinh-tai-ban.md) thay mã tham gia bằng QR cố định. Những đoạn mô tả joinCode/HMAC/QR chỉ có link bên dưới là lịch sử quyết định trước; xem P1.3 và API docs cho hành vi mới.

> Cập nhật triển khai02/11/2026: bản MVP localhost đã chạy. [ADR-0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) thay thế các lựa chọn/giới hạn khác biệt bên dưới; [API thực thi](../docs/api/README.md) và [trạng thái](../docs/IMPLEMENTATION-STATUS.md) là bằng chứng hiện tại. Phần ghi chưa triển khai ở nội dung gốc mô tả thời điểm thiết kế ban đầu.

Ngày 02/11/2026 · Thiết kế baseline. Nghiệp vụ từng endpoint nằm tại SSOT tương ứng; tài liệu này chỉ sở hữu quy ước kỹ thuật dùng chung.

## 1. Hợp đồng API

Prefix `/api/v1`; JSON camelCase; UUID dạng chuỗi; datetime ISO-8601 UTC `Z`; VND số nguyên. Ghi theo command endpoint, không PATCH status tùy ý. Không nhận actorId, role, snapshot giá, tổng tiền hoặc timestamp quyết định từ client làm nguồn tin cậy. `observedPriceVnd` chỉ dùng kiểm tra giá đã thay, không dùng làm đơn giá lưu.

Read danh sách trả `{items, nextCursor, serverTime}`; default 50/max100. Read order trả version/batch/item version. Command có `expectedVersion` (hoặc expectedOrderVersion khi tạo batch); thiếu →422, mismatch →409. Response command chứa resource đã cập nhật và version mới. Sau mutation invalidate query liên quan, không chờ hết chu kỳ polling.

| HTTP | Mã lỗi ví dụ | Ý nghĩa / UI |
|---|---|---|
| 400 | INVALID_JSON / INVALID_IDEMPOTENCY_KEY | Payload/header không đọc được |
| 401 | UNAUTHENTICATED | Phiên/mã tham gia không hợp lệ, đưa về login/join |
| 403 | FORBIDDEN / CSRF_FAILED | Đúng phiên nhưng thiếu quyền hoặc CSRF sai |
| 404 | RESOURCE_NOT_FOUND | Không có hoặc khách không có quyền tài nguyên; tránh dò order |
| 409 | VERSION_CONFLICT / INVALID_TRANSITION / TABLE_OCCUPIED | Tải trạng thái mới, không tự ghi đè |
| 409 | MENU_UNAVAILABLE / PRICE_CHANGED / PENDING_APPROVAL_EXISTS | Lỗi điều kiện nghiệp vụ có hướng dẫn xử lý |
| 409 | IDEMPOTENCY_KEY_REUSED | Cùng key nhưng khác payload/path/method |
| 410 | IDEMPOTENCY_REPLAY_EXPIRED | Key cũ không còn response; tra order, không chạy lại |
| 422 | VALIDATION_FAILED | Danh sách lỗi theo trường |
| 429 | RATE_LIMITED | Retry-After; không retry ghi bằng key mới |
| 503 | DATABASE_UNAVAILABLE | Không xác định kết quả nếu mất response; tra/retry key cũ |

Lỗi `application/problem+json`: type, title, status, code, detail an toàn, traceId, errors?; không SQL/stack trace. Ví dụ version conflict có currentVersion khi người gọi có quyền đọc. Không dùng HTTP 200 chứa success=false cho lỗi. API create trả 201, command khác 200/204 phù hợp; replay giữ response_status ban đầu.

OpenAPI sinh từ ASP.NET endpoint và commit export khi triển khai; frontend types sinh từ schema hoặc kiểm kiểu tương đương. API contract nằm cạnh Backend, không tạo tài liệu feature thứ hai. Unit test serialization và contract test kiểm response/error cơ bản, không test sao chép từng dòng implementation.

## 2. Xác thực và bảo vệ phiên

Chọn opaque session token thay JWT: cần thu hồi ngay khi khóa user, đổi quyền, đóng lượt; thêm DB lookup nhỏ phù hợp 50 thiết bị. Cookie staff/guest tách tên, HttpOnly, SameSite=Lax, Secure khi HTTPS, Path=/; token ngẫu nhiên ≥256 bit, DB chỉ hash. Staff absolute TTL 8h, guest 12h; không refresh im lặng vô hạn, không lưu token trong localStorage. Có thể cùng browser có staff/guest cookie: route staff chỉ staff, route khách chỉ guest; endpoint chung order có cả hai thì ưu tiên staff và audit đúng principal, không âm thầm đổi principal khi retry.

Mọi mutation cookie-authenticated dùng anti-CSRF token header được cấp từ endpoint bootstrap `/api/v1/auth/csrf`; token ràng buộc phiên, UI có thể đọc token CSRF nhưng không đọc session cookie. Validate Origin khớp PUBLIC_ORIGIN; không CORS `*` với credentials. Login/join cũng kiểm Origin và CSRF trước phiên bằng antiforgery cookie/token. Logout và rotate là POST, không GET gây side effect.

QR token là định danh bàn, không bí mật cấp quyền; joinCode mới là yếu tố chứng minh biết lượt. QR GET/ảnh có thể public nhưng không public danh sách order. Không ghi join code/token vào log/URL. Nhập mã rate limit 5 lần/phút/IP+lượt; login 5 lần/phút/IP+username; các API khác giới hạn mềm theo principal/IP, cấu hình đo lại ở F02. MVP một instance dùng limiter memory; nhiều instance phải đổi shared limiter hoặc gateway policy trước scale.

Admin ban đầu cấp từ seed secret, không password mặc định trong repo. Mật khẩu hash với PasswordHasher, không tự chọn SHA-256 cho mật khẩu; SHA-256 token entropy cao là trường hợp khác. Kiểm quyền và active mỗi request; request đã bắt đầu hợp lệ trước lúc khóa user có thể hoàn tất transaction đang chạy, lần request kế tiếp phải bị chặn. Không hứa hủy ngược tác vụ đã commit.

## 3. Transaction và thứ tự khóa

PostgreSQL READ COMMITTED + row locks + optimistic version. Không dùng SERIALIZABLE toàn hệ thống khi chưa cần. Thứ tự khóa chung để tránh đảo khóa:

1. Transaction advisory lock theo hash principal/key, nếu command có key; đọc record để replay hoặc tiếp tục. Hash collision chỉ làm tuần tự thêm, không đổi kết quả vì luôn so principal/key gốc.
2. `restaurant_tables` → `visits` khi order tại bàn; xác định ID bằng read trước rồi revalidate sau khóa. Takeaway bỏ qua hai bước này.
3. `orders` → `order_batches` → `order_items` (nhiều hàng cùng bảng theo UUID tăng dần).
4. `menu_categories` → `menu_items` theo UUID tăng dần nếu submit/approve cần kiểm menu.
5. Ghi payment/audit, kết thúc visit/revoke guest và persist response cùng transaction.

Mọi mutation item/batch/order đều khóa order trước và tăng order.version, kể cả sửa note, review và SERVED. Tác vụ chỉ menu khóa category→item, không quay về order; tác vụ bàn/visit khóa table→visit, không khóa ngược; quản trị tài khoản dùng khóa riêng không chờ order. Không giữ transaction khi chờ con người nhập mã/duyệt/nhận tiền hoặc khi tải ảnh/HTTP bên ngoài.

Khóa table cho đơn tại bàn làm tuần tự các lệnh cùng bàn, phù hợp tải MVP; các bàn khác và takeaway vẫn song song. Dùng compare expectedVersion để người dùng biết dữ liệu cũ, không dựa riêng row lock để âm thầm chấp nhận ý định trên dữ liệu đã đổi.

### Ranh giới các use case quan trọng

| Use case | Toàn bộ thay đổi nguyên tử |
|---|---|
| OpenVisit | Kiểm bàn trống/active → visit + order rỗng + joinCode HMAC → audit → idempotency response mã hóa |
| SubmitBatch | Kiểm order SERVING/version → sequence → lock menu/revalidate giá/availability → batch/items theo first/additional → audit |
| ApproveBatch | Kiểm role/người gửi/version/PENDING → menu còn hợp lệ → APPROVED + eligible_at + items PLACED → audit |
| Reject/Withdraw | Một quyết định batch và toàn bộ trạng thái item đồng bộ, audit |
| Amend/Cancel/Prepare | Khóa order/item, kiểm tiến độ/version/quyền/lý do rồi update + audit; request thua không ghi gì |
| RequestPayment | Không pending + mọi item terminal → tính từ snapshot → AWAITING_PAYMENT + total + audit |
| RecordPayment | AWAITING_PAYMENT + amount → payment + PAID + visit CLOSED + revoke guest → audit |
| Reopen | Chưa payment → SERVING + total NULL + audit có lý do |

Mutation thất bại rollback cả audit/idempotency/effects. Deadlock (40P01)/serialization (40001) retry nội bộ tối đa 2 lần với backoff ngắn và cùng request; sau đó trả lỗi tạm thời có traceId. Unique violation phân loại business conflict (bàn, code, key), không trả SQL 500 chung. Mã random collision có retry hữu hạn trước commit, không dùng max+1 trừ sequence batch dưới order lock.

## 4. Idempotency và mạng chập chờn

Áp dụng Idempotency-Key UUID cho mọi command tạo đơn/mở lượt/gửi batch/duyệt/rút/từ chối/chuyển món/hủy/xin trả/mở lại/thu tiền. PATCH note/menu/account có version chống lặp và có thể dùng key nếu client cần retry tự động; không thiếu key cho API đã yêu cầu ở SSOT.

1. Client tạo key cho **một ý định**, giữ key/payload khi đang gửi hoặc kết quả chưa rõ; submit thêm hợp lệ tạo key mới.
2. Server xác thực và quyền, canonicalize payload, SHA-256 request; bắt đầu transaction, lấy advisory lock theo principal+key rồi SELECT record. Không INSERT placeholder vì response_status/các trường bắt buộc cần giá trị hợp lệ ngay khi insert.
3. Nếu đã có record: cùng route/method/hash trả response đã commit; khác →409; record hết replay →410 không thực hiện lại. Request cùng principal+key chờ khóa, sau đó đọc kết quả transaction trước; UNIQUE vẫn bảo vệ nếu writer khác không tuân thủ khóa.
4. Chưa có record: thực hiện command, mã hóa response, INSERT record đầy đủ và commit với business effects cùng lúc. Validation fail rollback, không cache lỗi nghiệp vụ. Không có record IN_PROGRESS hoặc job cập nhật response sau commit.
5. Auth/ownership luôn kiểm trước replay; key không thay thế phiên. Nếu phiên hết hạn trước retry, đăng nhập lại staff cùng user vẫn nhận key theo user_id; guest session mới khác principal phải tra order hiện tại, không tự replay request cũ bằng key mới.

Replay window 24h, tombstone giữ theo ADR-0003. API GET `/api/v1/requests/{key}` cho principal hiện tại tra `{status:'COMPLETED', response...}` hoặc 404 `UNKNOWN`, 410 hết replay. 404 chưa chứng minh request cũ không còn chạy; cho phép gửi lại **cùng key** để unique lock phân xử. Không tự gửi lại command với key mới khi timeout, kể cả user bấm nhiều lần.

MVP có thể giữ pending request trong sessionStorage (key + body tối thiểu, không token/PII) để refresh không mất khả năng tra; đây là retry hỗ trợ mạng, không offline queue tự đồng bộ. Nếu chưa triển khai lưu này, UI vẫn phải tra order trước khi gửi lại sau reload. Không hứa “đơn chưa gửi” khi server có thể đã commit nhưng response bị mất.

## 5. Đồng bộ giao diện

Chọn polling có kiểm soát trước WebSocket/SignalR. Không có kênh push cần outbox/Redis. KDS/serving/approval đang foreground poll **1 giây**, menu/trạng thái khách 2 giây; pause/backoff khi hidden/mất mạng, reconnect tải snapshot ngay. Chỉ một request đang chạy/query để không chồng poll. Trả ETag khi projection ổn định có thể giảm payload nhưng không bắt buộc ở MVP.

Từ commit batch APPROVED đến KDS hiển thị p95≤3s; thay đổi state tới thiết bị staff khác p95≤2s với query API p95≤500ms trong kịch bản chuẩn. Chỉ tiêu tổng API p95≤2s vẫn kiểm riêng. Thời gian chờ con người duyệt batch không thuộc độ trễ truyền tin, UI phải thể hiện rõ trạng thái chờ. Nếu poll 1s không đạt do tải DB/query, đo EXPLAIN/index/payload trước; SignalR là phương án ADR sau benchmark, không đổi chỉ tiêu để che failure.

KDS/serving queue đọc đủ các trang; snapshot mới thay queue đúng theo ID, không append trùng. Dữ liệu cũ >3 lần poll lỗi hiển thị banner mất kết nối và lastUpdatedAt, khóa lệnh cần xác nhận; trả mạng tải lại rồi mở thao tác. Không optimistic đánh dấu đã thanh toán/đã phục vụ trước server commit.

## 6. Observability và kiểm thử

RequestId/traceId, latency, status/error code, module/command, orderId được phép log; không log session token, join code, password, response ciphertext plaintext hoặc request body login. Audit nghiệp vụ là bảng riêng, log ứng dụng không thay audit. Health readiness kiểm DB/schema; không trả connection string trong lỗi.

Integration test dùng PostgreSQL 16 thật, hai connection + barrier để tạo race; unit tests dùng state policy cho nhánh đáng kể. E2E nhiều browser context cho khách/bếp/phục vụ/thu ngân. Không chọn SQLite/EF InMemory cho unique partial/deferred trigger/row lock. Các test và kết quả thực thi sẽ ghi ở ADR-0006; hiện mới là kế hoạch.

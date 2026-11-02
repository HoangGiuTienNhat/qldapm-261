# ADR-0006 — Lộ trình và kế hoạch triển khai

> Đối chiếu database 03/11/2026: hiện dùng 4 EF SQL-first migrations, chưa có ModelSnapshot/bundle và chưa hỗ trợ Down tự động. Cây/lệnh scaffolding dự kiến dưới đây không phải cách chạy hiện tại; xem [ADR-0004](0004-docker-va-migration.md), [từng bảng](../docs/db/README.md) và [hướng dẫn migration](../GUIDE/DATABASE-MIGRATIONS.md).

> Cập nhật triển khai02/11/2026: bản MVP localhost đã chạy. [ADR-0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) thay thế các lựa chọn/giới hạn khác biệt bên dưới; [API thực thi](../docs/api/README.md) và [trạng thái](../docs/IMPLEMENTATION-STATUS.md) là bằng chứng hiện tại. Phần ghi chưa triển khai ở nội dung gốc mô tả thời điểm thiết kế ban đầu.

Ngày 02/11/2026 · Trạng thái: **kế hoạch; toàn bộ task chưa bắt đầu thực thi code**. Không dùng trạng thái hoàn thành tài liệu để đánh dấu hoàn thành feature.

## 1. Cơ sở ước lượng

Giữ mốc tham chiếu nguồn **295 giờ cơ sở + 30 giờ dự phòng = 325 giờ công**, chia lại theo task bên dưới. Đây là ước lượng ban đầu, không phải cam kết chính xác: yêu cầu duyệt mỗi đợt thêm mới được làm rõ nên cần re-estimate sau prototype O02. Khối lượng phân tích/thiết kế đã thực hiện trong đợt tài liệu hiện tại có thể trừ khỏi R01/UX01 sau khi nhóm review, không tự coi tất cả 325 giờ còn lại phải làm.

Giả định nhóm 4 người dành khoảng 10–11 giờ/người/tuần trong 8 tuần hoặc nhóm 5 người khoảng 8–9 giờ/người/tuần. Không gán deadline lịch cụ thể vì deadline “21h thứ Bảy” nguồn cũ không có ngày và không đủ làm lịch hiện tại. 6 tuần cần tăng thời lượng/giảm phần tiện ích, không cắt invariant tiền, quyền hoặc chống trùng.

Tên phụ trách nguồn P1.4/P1.5/P1.7 được giữ ở SSOT. Vai trò BE/FE/QA trong task là chuyên môn cần, chưa tự gán người. Mỗi task có một owner và một reviewer được nhóm chỉ định trước bắt đầu; có thể một người làm cả BE/FE.

## 2. Roadmap theo increment

| Increment | Tuần tương đối | Kết quả demo và cổng hoàn tất |
|---|---|---|
| Chuẩn bị | 1 | Review SSOT/ADR, prototype luồng QR/duyệt/thu tiền; không còn mâu thuẫn về hành vi chính |
| I1 — Nền tảng | 2–3 | Docker dựng sạch + migration + login/role; không phải cài SDK/npm host |
| I2 — Menu/bàn/đặt món | 4–5 | Khách tham gia lượt, gửi đợt đầu, gọi thêm chờ duyệt; takeaway có mã nhận |
| I3 — Bếp | 6 | Duyệt/từ chối batch + KDS chuyển trạng thái + hết món + race hủy/nấu |
| I4 — Phục vụ/thanh toán | 7 | SERVED, chốt tiền, thu tiền một lần, đóng lượt và vô hiệu token |
| I5 — Hoàn thiện | 8 | Hai E2E + ngoại lệ, tải 50 thiết bị, restore, tài liệu/demo |

Các việc UI/schema có thể thực hiện đồng thời bởi thành viên trong nhóm sau khi chốt contract, nhưng một feature chỉ hoàn tất khi tích hợp FE–BE–DB. Không chờ cuối sprint mới ghép frontend. Buffer 30h dùng sửa lỗi/rủi ro, không đưa tính năng ngoài scope vào vì “còn giờ”.

## 3. Task breakdown và acceptance criteria

Mỗi task gồm cả code, tài liệu liên quan và kiểm tra cần thiết; giờ không nhân thêm khi có hai người phối hợp. AC cố ý ngắn và kiểm chứng được. Tham chiếu QC chi tiết ở SSOT để tránh chép lại toàn bộ yêu cầu.

| ID | Task / kỹ năng | Phụ thuộc | Giờ | Acceptance criteria vừa đủ |
|---|---|---|---:|---|
| R01 | Review baseline và ranh giới nghiệp vụ / cả nhóm | — | 20 | Review 7 SSOT; ghi owner/reviewer, xác nhận A01–A07 hoặc ghi thay đổi; mọi mâu thuẫn có phương án có nguồn |
| UX01 | Wireframe responsive / FE + nghiệp vụ | R01 | 20 | Prototype menu/QR, chờ duyệt, KDS, phục vụ, thu ngân; walkthrough một lượt và takeaway, nút/nhãn phân biệt chờ duyệt |
| F01 | Khởi tạo đúng Backend/Frontend, Docker/Compose / BE+FE | R01 | 10 | Có solution/Next app đúng folder, lockfiles, gateway, healthchecks; container hello API/UI chạy, không phụ thuộc npm host |
| F02 | Nền HTTP/errors/logging/CI và quy trình SSOT / BE | F01 | 8 | ProblemDetails+traceId, validation, CSRF nền, lint/build CI; PR template yêu cầu SSOT/ADR/migration/test liên quan |
| DB01 | EF models/schema/indices / BE | F01, R01 | 14 | Mapping đủ 15 bảng app + history; migration tạo sạch; NN/FK/UNIQUE/CHECK theo ADR-0003, SQL được review |
| DB02 | State/deferred triggers/invariants / BE | DB01 | 10 | Commit sai visit-order/batch-item/payment-total bị chặn; transition/immutability test PostgreSQL pass |
| DB03 | Bundle/seed/DB roles/backup nền / BE | DB02 | 8 | Migration one-shot fail ngăn API; chạy lại an toàn; seed idempotent; app không DDL hay sửa payment/audit |
| A01 | Tài khoản, phiên và RBAC + UI / BE+FE | F02, DB03 | 18 | QC-P1.1-01..05 pass; admin seed đổi mật khẩu, cookie/logout/revoke/CSRF đúng |
| M01 | Menu/danh mục/availability + UI / BE+FE | A01, DB03, UX01 | 16 | QC-P1.2-01..05; ảnh fallback, active/available tách biệt; không đổi snapshot món cũ |
| V01 | Bàn/lượt/QR/join + UI / BE+FE | A01, DB03, UX01 | 18 | QC-P1.3-01..05; hai request mở cùng bàn chỉ một thành công; hai khách cùng lượt không tạo hai order |
| O01 | Order/batch đầu/takeaway/idempotency + UI / BE+FE | M01, V01, F02 | 22 | Tại bàn và takeaway gửi đợt đầu vào PLACED; retry sau mất response không duplicate; giá đổi cần xác nhận |
| O02 | Gọi thêm/duyệt/từ chối/rút/sửa-hủy + UI / BE+FE | O01 | 20 | QC-P1.4-01..10; mọi đợt sau PENDING; duyệt nguyên đợt; manager/admin được tự duyệt, bếp không tự duyệt; quantity increase thành batch mới |
| K01 | KDS/tiến độ/polling + UI / BE+FE | O02 | 18 | QC-P1.5-01..05; không nhảy bước; pending không lẫn queue; đủ trang queue, báo dữ liệu cũ |
| S01 | Theo dõi/serve/takeaway bàn giao + UI / BE+FE | K01 | 14 | QC-P1.6-01..05; khách không tự SERVED, thu ngân chỉ takeaway; race phục vụ/hủy không sai trạng thái |
| B01 | Chốt tiền/thu tiền/receipt/đóng lượt + UI / BE+FE | S01, O02 | 22 | QC-P1.7-01..07; CASH/BANK_TRANSFER/ZERO đúng; transaction lỗi rollback, không thu hai lần |
| INT01 | Tích hợp contract và luồng đa vai trò / BE+FE | B01 | 14 | OpenAPI export/types khớp; hai E2E hoàn chỉnh qua gateway; lỗi 401/403/409/422 có hướng dẫn đúng |
| QA01 | Bộ race/negative/migration upgrade / QA+BE | INT01 | 17 | Ca cạnh tranh mục 4 pass trên PG16; upgrade DB cũ có dữ liệu; không secrets trong git/log |
| QA02 | Tải và usability/responsive / QA+FE | INT01 | 10 | Mục 5 đạt chỉ tiêu hoặc issue blocker có giải pháp/retest; test mobile/tablet/desktop và ≥5 người dùng |
| QA03 | Rebuild sạch/restart/restore / QA+BE | DB03, INT01 | 8 | Máy chỉ Docker dựng từ runbook; restart giữ order; restore DB mới đối soát tiền/lịch sử pass |
| DOC01 | Hướng dẫn vận hành/demo và đồng bộ hồ sơ / cả nhóm | QA01, QA02, QA03 | 8 | SSOT/ADR/code/test nhất quán; kịch bản demo, seed và kết quả test có bằng chứng; phạm vi AI hỗ trợ được khai báo |
| | **Cơ sở** | | **295** | |
| BUF | Dự phòng lỗi/tích hợp/thay đổi nhỏ có chấp thuận | Theo rủi ro | **30** | Không thêm delivery/kho/gateway/voucher |
| | **Tổng** | | **325** | |

## 4. Ma trận kiểm thử bắt buộc

| Nhóm | Kịch bản trọng yếu | Kết quả mong đợi |
|---|---|---|
| Luồng chính | Dine-in: mở→join→gửi→nấu→serve→thu tiền | Một visit/order/payment, bàn giải phóng, token thu hồi |
| Luồng gọi thêm | Lần đầu xong, gửi đợt thêm, duyệt, nấu/serve | Cùng order, batch riêng, trước duyệt chưa vào bếp/tiền |
| Từ chối/rút | Reject cần lý do, guest rút pending, duyệt-rút cạnh tranh | Một kết thúc hợp lệ, không dòng dở trạng thái |
| Takeaway | Tạo không bàn, gọi thêm cần duyệt, bàn giao, tiền mặt/chuyển khoản | Mã nhận rõ, không tác động bàn |
| Retry | Hai request cùng key; response mất sau commit; key khác cùng body | Không duplicate cùng key; key mới là đợt mới |
| Race bàn | Hai nhân viên mở cùng bàn | Một visit OPEN + order |
| Race bếp | Hai thiết bị ACCEPT/PREPARE cùng item/version | Một thắng, một 409, không nhảy bước |
| Race điều chỉnh | Guest cancel/amend cạnh tranh PREPARE | Không sửa/hủy tự do sau PREPARING |
| Race menu | Đổi hết/ẩn/giá đồng thời submit/approve | Theo commit/lock đã định, không đặt món hết sau thay đổi đã commit |
| Race chốt | Submit/approve cạnh tranh request-payment | Không vừa thêm món vừa chốt tổng bỏ sót |
| Race thu tiền | Hai cashier + cùng/different key | Một payment; PAID/CLOSED/revoke đồng bộ |
| Rollback | Tiêm lỗi audit, giữa payment và visit close | Không dữ liệu nửa chừng |
| Bảo mật | Token A đọc/sửa B; khách tự cấp quyền; CSRF sai; khóa user | Chặn server, không chỉ UI |
| Tiền | 10 hóa đơn cố định gồm hủy, pending, đổi giá, tổng0, quantity99 | Tổng 100% đúng và không overflow |
| Hủy sau chế biến | Món PREPARING đã hủy ngoại lệ, sau đó thử hủy toàn đơn | Không chuyển CANCELLED; hoàn tất bằng thanh toán ZERO nếu tổng 0 |
| Migration | Fresh, upgrade có dữ liệu, chạy bundle lặp, sai constraint | Không mất dữ liệu, fail giữ schema nhất quán |

Test unit chỉ cho policy/phép tính/state đáng kể; integration cho DB/transaction; E2E cho hành vi đa vai trò. Không viết test snapshot một-một mọi component chỉ để tăng coverage. Không có coverage % tùy tiện làm cổng nghiệm thu.

## 5. Hiệu năng và usability

Đây là **mục tiêu cần đo**, không phải kết quả hiện có. Dữ liệu seed 30 bàn, menu ≥20 món, dữ liệu lịch sử ~1000 orders + 10.000 items; 50 browser/client mô phỏng (30 khách, 10 phục vụ, 7 bếp, 2 thu ngân, 1 quản lý). Ghi rõ CPU/RAM máy host, Docker resource allocation và network trong báo cáo; không khẳng định máy nào cũng đạt như nhau.

Warm-up 2 phút, tải ổn định 10 phút, mixed read/write gồm gửi mới, gọi thêm, review, trạng thái và polling thực tế; không chỉ bắn một API GET rỗng. Không tính response business conflict cố ý vào tỷ lệ server error; vẫn báo riêng số 409. Đo từ server commit tới UI render bằng correlationId và browser timestamps/clock đã đồng bộ, tránh so hai clock lệch.

| Chỉ tiêu | Ngưỡng | Kiểm chứng |
|---|---|---|
| API tổng | p95≤2 giây; không unhandled 5xx trong kịch bản chuẩn | Metrics load tool + log |
| Query queue staff | p95≤500ms | Backend timer, query plan khi chậm |
| Batch APPROVED tới KDS | p95≤3 giây | Browser trace; batch thêm tính từ review commit |
| State tới thiết bị staff khác | p95≤2 giây | Hai context và polling 1s |
| Duplicate do retry | 0 trong bộ kịch bản | Đếm batch/item/payment và key |
| Thu tiền khi đơn sẵn sàng | ≤1 phút/đơn mẫu | UAT thao tác, không tính chờ khách chuyển tiền |
| Khách mới dùng QR | ≥80% của ≥5 người, tự gửi đợt đầu ≤3 phút | UAT script, ghi khó khăn |
| Responsive | 360px phone, tablet 768px, desktop 1280px | Không tràn thao tác chính, đủ nhãn/trạng thái |

Nguồn có mục tiêu thời gian từ PLACED tới PREPARING trung bình≤2 phút/p90≤5 phút. Đây phụ thuộc nhân sự/chế biến, đo riêng trong ca mô phỏng; không dùng test API để tuyên bố bảo đảm tốc độ nấu thực tế. Thời gian PENDING chờ duyệt báo riêng, chưa đặt SLA con người khi chủ dự án chưa yêu cầu.

## 6. Definition of Ready / Done

Ready: task có owner/reviewer, SSOT liên quan không còn câu hỏi cản thực hiện, API/schema đã thống nhất, có dữ liệu/kịch bản tối thiểu. Done: AC và QC liên quan pass, peer review ít nhất một người, migration/test cần thiết pass, FE–BE tích hợp, SSOT/ADR/OpenAPI đồng bộ, không secret/log nhạy cảm, có bằng chứng demo. PR chưa đạt chỉ tiêu thì ghi blocker và xử lý; không đánh dấu Done bằng “chạy được trên máy tôi”.

Quy trình Git: nhánh feature `codex/<ten-task>` khi agent tạo, hoặc quy ước nhóm đã chốt; merge cần review. Không tự ghi nhận tên người chịu trách nhiệm chưa được phân công. Báo cáo tuần cập nhật việc hoàn thành, rủi ro, công còn lại và re-estimate; không cần bổ sung Jira/Monday integration để thực hiện kế hoạch.

## 7. Thứ tự triển khai đề nghị sau khi review tài liệu

R01 → F01/F02/DB01–03 → A01 → M01/V01 → O01/O02 → K01 → S01/B01 → INT/QA/DOC. Bắt đầu bằng môi trường Docker và vertical slice đăng nhập/menu trước; không sinh toàn bộ CRUD bảng rồi mới xác minh luồng. Dừng tại bàn giao tài liệu trong yêu cầu hiện tại; bắt đầu task code khi người dùng yêu cầu triển khai.

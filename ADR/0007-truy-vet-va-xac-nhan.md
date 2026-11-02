# ADR-0007 — Nguồn, truy vết và xác nhận nghiệp vụ

> Thay đổi QR hiện hành03/11/2026: [ADR-0009](0009-qr-co-dinh-tai-ban.md) thay mã tham gia bằng QR cố định. Những đoạn mô tả joinCode/HMAC/QR chỉ có link bên dưới là lịch sử quyết định trước; xem P1.3 và API docs cho hành vi mới.

> Cập nhật triển khai02/11/2026: bản MVP localhost đã chạy. [ADR-0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) thay thế các lựa chọn/giới hạn khác biệt bên dưới; [API thực thi](../docs/api/README.md) và [trạng thái](../docs/IMPLEMENTATION-STATUS.md) là bằng chứng hiện tại. Phần ghi chưa triển khai ở nội dung gốc mô tả thời điểm thiết kế ban đầu.

Ngày 02/11/2026 · Hồ sơ baseline. Đã đọc **toàn bộ 5 file Markdown** hiện có trong `temp-docs/`, không chỉ báo cáo tổng hợp. Không truy cập các Google Docs/Sheets/Monday được dẫn bên trong vì thông tin cần thiết đã có trong bản nguồn local; không tuyên bố đã đọc nội dung các liên kết ngoài đó.

## 1. Danh mục nguồn

| Nguồn local | Nội dung dùng | Đánh giá |
|---|---|---|
| [Tong-hop-thong-tin.md](../temp-docs/Tong-hop-thong-tin.md) | Khảo sát thực tế câu 1–10, S1–S12, C01–C10, O1–O5, quy mô, ngoài phạm vi, 5 increments và 325 giờ | Căn cứ tổng quan chính; có phân biệt cột giả định và phản hồi thực tế |
| [phan-he-dat-mon-va-dieu-chinh-don.md](../temp-docs/phan-he-dat-mon-va-dieu-chinh-don.md) | Mẫu product identity/description/derivation/composition/format/standard/quality; QC-P1.4-01..04 | Nguồn cấu trúc; diễn giải chống trùng/gọi thêm cần làm rõ |
| [a.md](../temp-docs/a.md) | Bản bảng của mô tả P1.4, bổ sung diễn giải QC | Gần trùng mẫu gốc; không tính như yêu cầu độc lập thứ hai |
| [tailieu.md](../temp-docs/tailieu.md) | PBS bảy phân hệ, người phụ trách, QR bàn bận, bản P1.4 | Danh mục mã P1.1..P1.7; có điểm mâu thuẫn cần xử lý |
| [traloi-phanhe-xulimontaibep.md](../temp-docs/traloi-phanhe-xulimontaibep.md) | Chi tiết KDS, FIFO, trạng thái, QC-P1.5-01..04, UI, concurrency, hết món | Phân biệt yêu cầu cốt lõi với đề xuất mở rộng như xử lý hàng loạt |

Giữ nguyên toàn bộ nguồn trong `temp-docs/`; không sửa lịch sử cho khớp thiết kế mới. Nguồn lịch sử có tên người và quy mô 4/5 thành viên khác nhau; không tự hợp nhất tên hoặc gán nhân sự hiện tại. Ngày 29/09/2026 là ngày khảo sát trong nguồn, không phải ngày tạo baseline này.

## 2. Thứ tự thẩm quyền

1. Chỉ dẫn và xác nhận mới trực tiếp từ chủ dự án trong chat này.
2. Phản hồi thực tế khách hàng được ghi trong báo cáo tổng hợp (không nhầm với cột “câu trả lời giả định”).
3. Tài liệu mô tả phân hệ/PBS có nguồn rõ và không mâu thuẫn các mục trên.
4. Đề xuất kỹ thuật của bộ tài liệu này, ghi nhãn là quyết định/giả định, không gán thành lời khách hàng.

Khi có yêu cầu mới được chấp thuận, đưa vào đúng SSOT; mục này chỉ ghi xuất xứ/quyết định, không tạo đặc tả nghiệp vụ cạnh tranh với SSOT.

## 3. Các điểm đã được chủ dự án xác nhận

| Mã | Vấn đề nguồn | Xác nhận ngày 02/11/2026 | SSOT chịu trách nhiệm |
|---|---|---|---|
| C01 | QC chống trùng bị diễn giải thành chỉ gửi một lần; báo cáo nói gọi thêm vào đơn cũ | **Mỗi đợt gọi thêm cần phê duyệt**. Idempotency vẫn phân biệt retry và yêu cầu mới; không mở order mới | P1.4 |
| C02 | PBS chặn mọi QR nếu bàn đang bận, không khả thi cho khách đang ăn gọi thêm | **Cho tham gia đúng lượt qua mã**; nhân viên mở lượt/cấp mã | P1.3 |
| C03 | Mẫu P1.4 giới hạn sửa/hủy tại chỗ, takeaway chưa rõ | **Dùng phương án cho nhân viên sửa/hủy takeaway trước nấu; sau nấu manager xử lý ngoại lệ có lý do** | P1.4 |
| C04 | Thanh toán ghi chung, có nhắc giảm giá nếu có | **Tiền mặt/chuyển khoản thủ công, chưa giảm giá/VAT tách riêng** | P1.7 |

Admin được các quyền nghiệp vụ tương ứng của manager theo baseline RBAC; không phải tài khoản khách hàng thực tế mang tên giảng viên. ADMIN không được bỏ qua ràng buộc/audit.

Xác nhận mới nhất: chủ dự án chọn QR cố định có ảnh, link gốc và sao chép; không nhập mã, không tự mở lượt. Quyết định này supersedes C02 trước đó; xem ADR-0009/P1.3.

## 4. Giả định/đề xuất thiết kế cần review khi R01

Đây là baseline để tài liệu đủ cụ thể, **chưa phải tất cả đã được chủ dự án trả lời**. Không cần cản trở việc review; thay một điểm thì sửa SSOT/schema/plan liên quan trước code.

| Mã | Phương án baseline | Tác động nếu thay |
|---|---|---|
| A01 | Đợt đầu tự APPROVED, mọi đợt sau kể cả nhân viên gọi đều chờ; KITCHEN/MANAGER/ADMIN duyệt toàn đợt, không partial | Nếu chỉ khách phải duyệt hoặc phải duyệt cả đợt đầu: đổi P1.4, policy và QC |
| A02 — đã chốt lại | Theo trả lời2B: MANAGER/ADMIN được tự duyệt; KITCHEN không tự duyệt. Quyền hủy ngoại lệ vẫn manager/admin | Đã cập nhật P1.4, service/trigger và smoke self approval |
| A03 | Thanh toán sau khi mọi món đã SERVED (takeaway cũng bàn giao trước), một lần toàn đơn, không thu trước/hoàn tiền | Thu trước đòi tách financial state và fulfillment state, không chỉ đổi thứ tự nút |
| A04 | Món đã gửi/duyệt nhưng chưa ACCEPTED cũng không tự hủy khi menu hết; batch PENDING phải kiểm còn hàng lúc duyệt | Nếu cần hủy món chưa nhận, thêm explicit command/audit và thông báo, không xóa tự động |
| A05 | Sửa trước nấu chỉ giảm số lượng/note/hủy toàn dòng; tăng/đổi món qua batch thêm cần duyệt; từ PREPARING chỉ hủy toàn dòng | Partial fulfillment/cancel sau nấu cần tách item quantity/state, tăng scope |
| A06 | Staff nội bộ do admin tạo, không public signup; khách QR không tài khoản, takeaway do staff/cashier tạo; một nhà hàng, giá VND cuối | Tài khoản khách/multi-branch/takeaway online cần model/security/API mới |
| A07 | Giới hạn batch 50 dòng, quantity 99, capacity 30; opaque cookie sessions; polling 1s staff/2s customer/menu | Có thể điều chỉnh sau test, sửa validation/constraint/migration cùng lúc |

Các thiết kế như ZERO tổng0, order reopen trước nhận tiền, không sửa PAID, mã QR + code entropy/rate limit và response idempotency mã hóa đã cụ thể trong SSOT/ADR. Không còn TBD bắt buộc về field/status/API để bắt đầu review thiết kế; việc lựa chọn patch images, owner/reviewer, domain HTTPS và tài nguyên Docker sẽ thực hiện ở F01/R01.

## 5. Ma trận truy vết phạm vi

| Scope / nguồn | SSOT duy nhất | Dữ liệu liên quan | Task | Tiêu chí chính |
|---|---|---|---|---|
| S1 / câu1 | P1.1 | staff_users, roles, user_roles, staff_sessions | A01 | QC-P1.1-01..05 |
| S2, S9 / câu5–6 | P1.2 | menu_categories, menu_items | M01 | QC-P1.2-01..05 |
| S3 / câu2.1 | P1.3 | restaurant_tables, visits, guest_sessions | V01 | QC-P1.3-01..05 |
| S4 / câu2.2 + C01 | P1.4 | orders, order_batches, order_items | O01/O02 | QC-P1.4-01..05 |
| S5 / câu2 + C03 | P1.4 (đặt), P1.6 (giao), P1.7 (tiền) | orders không visit, pickup_code | O01/S01/B01 | QC-P1.4-09, QC-P1.6-04 |
| S6 / câu3 | P1.5 | order_items + audit_events | K01 | QC-P1.5-01..05 |
| S7 / câu3,7 | P1.6 | order_items + audit_events | S01 | QC-P1.6-01..05 |
| S8 / câu4 | P1.4 | batch/item + audit_events | O02 | QC-P1.4-06..08 |
| S10 / câu7 + C04 | P1.7 | payments, orders, visits | B01 | QC-P1.7-01..07 |
| S11 / câu10 | P1.4 (nghiệp vụ retry), ADR-0005 (giao thức) | idempotency_records | O01/QA01 | QC-P1.4-02,10 |
| S12 / câu8–9 | Từng SSOT (UI), ADR-0002/0006 (chung) | Toàn schema | UX01/QA02/QA03 | Hiệu năng/responsive/UAT |

## 6. Phạm vi loại trừ và xử lý phần nguồn mở rộng

Delivery, native app, offline sync toàn phần, kho/nguyên liệu, supplier, đặt bàn trước, tích điểm, voucher, payment gateway/POS hardware, báo cáo đa chi nhánh không nằm trong MVP. Cụm “giảm giá nếu có” trong PBS không tạo yêu cầu voucher/discount sau xác nhận C04.

Batch processing nhiều món/nhiều đơn, dark mode đầy đủ, âm báo và thống kê bếp nâng cao là đề xuất trong tài liệu KDS: MVP có lọc/trạng thái/nút lớn, chưa cam kết những tiện ích đó. Bếp trưởng được ánh xạ KITCHEN, không tạo role thứ bảy. FIFO lấy thời điểm đủ điều kiện vào bếp để phù hợp duyệt gọi thêm; thời điểm submit vẫn lưu để biết đã chờ duyệt bao lâu.

## 7. Kiểm chứng tài liệu và giới hạn

Đã đọc nguồn local và kiểm tra công cụ bằng lệnh phiên bản; kiểm tra links local, mã phân hệ, tổng giờ và danh mục bảng ở bước QA tài liệu. Chưa build/run app, chưa chạy Docker daemon, chưa áp dụng migration hoặc benchmark. Các acceptance criteria mô tả bằng chứng cần thu ở giai đoạn thực hiện, không phải kết quả đã đạt.

Toàn bộ tài liệu được AI hỗ trợ phân tích và soạn; nhóm cần review theo quy định liêm chính học thuật trong nguồn và chịu trách nhiệm nội dung bàn giao. Không tự đăng lên Google Drive/Monday/GitHub hoặc nhắn cho thành viên nhóm.

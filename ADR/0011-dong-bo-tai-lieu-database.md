# ADR-0011 — Từ điển từng bảng và đồng bộ với mã nguồn

03/11/2026 · Đã áp dụng. Bổ sung cách quản trị tài liệu, không thay schema hoặc nghiệp vụ. Supersedes phần từ điển thiết kế cũ của ADR-0003 và quy trình scaffolding dự kiến cũ của ADR-0004; hai ADR này đã được viết lại theo triển khai thực tế.

## Bối cảnh

Tài liệu baseline từng mô tả một số DEFAULT, CHECK, index, ModelSnapshot/bundle dự kiến như trạng thái thiết kế. Mã hiện tại dùng SQL-first EF migrations, có 4 phiên bản và chưa hỗ trợ Down. Cần tra rõ từng bảng/trường và tránh lấy kế hoạch chưa triển khai làm bảo đảm của database.

## Quyết định

1. [docs/db](../docs/db/README.md) là từ điển schema hiện hành: **mỗi bảng một Markdown**, gồm cả bảng EF. Mỗi file đi từ vai trò tới từng trường, kiểu/NULL/DEFAULT DB, constraints, index, trigger và vòng đời. Không gộp nhiều bảng vào một file.
2. Lấy metadata vật lý từ catalog PostgreSQL của đúng Compose project bằng transaction READ ONLY. Chỉ đọc định nghĩa cấu trúc và MigrationId, không xuất bản ghi kinh doanh, credentials hoặc giá trị token. Lưu `schema-metadata.json` để review diff/offline.
3. Ý nghĩa trường/giới hạn do người phát triển review trong `scripts/db_doc_catalog.py`. `generate_db_docs.py` ghép metadata và giải thích, báo lỗi nếu thiếu/thừa bảng/trường. `TRIGGERS.md` giữ định nghĩa hàm thật, không tự suy rằng DB kiểm mọi quyền nghiệp vụ.
4. `--check` so tài liệu sinh được, chuỗi migration và SHA-256 nguồn backend đã review. Backend đổi sau lần review sẽ làm check báo stale; cần đánh giá thay đổi, cập nhật ADR/SSOT/API và giải thích phù hợp trước khi sinh lại manifest. Không coi cập nhật hash là review xong.
5. SSOT giữ một file duy nhất cho mỗi feature; ADR-0003 giữ lý do cấu trúc và tham chiếu từng bảng; ADR-0004/GUIDE mô tả migration thực tế. Không chép từ điển trường nhiều nơi và không duy trì thêm bộ SQL up/down chạy song song.
6. Điều kiện hoàn tất ghi trong [AGENTS.md](../AGENTS.md): code, migration, SSOT, ADR, DB/API docs và hướng dẫn bị ảnh hưởng phải đồng bộ cùng commit. Commit local, không tự push.

## Giới hạn

Check phát hiện khác biệt cấu trúc/file và nguồn backend, không hiểu ngữ nghĩa yêu cầu, không tự chứng minh prose đúng hoặc phát hiện DDL sửa tay trên DB nếu chưa export lại. Hiện chưa có CI tự chạy; developer phải chạy trước commit. Việc thêm CI là công việc riêng nếu cần sau này.

Export metadata không chạy migration/seed. Việc test upgrade/fresh DB phải được lên kế hoạch trên đích thử riêng có quyền; không xóa dữ liệu/volumes/images hay tác động dự án khác để làm tài liệu. Lần cập nhật này chỉ đọc catalog, không thay dữ liệu.

## Kiểm chứng

- Đối chiếu đủ 4 MigrationId với source; catalog có 16 bảng, 144 trường và 10 trigger functions.
- Generator và `--check` chạy offline thành công; kiểm tra thiếu giải thích/stale fingerprint có báo lỗi.
- Kiểm liên kết tài liệu local; không chạy smoke tạo đơn, migration/seed hoặc thao tác Docker ghi/xóa dữ liệu trong công việc này.

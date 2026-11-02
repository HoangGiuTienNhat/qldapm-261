# ADR-0004 — Docker Compose, migration và dữ liệu bền vững

> Thay đổi QR hiện hành03/11/2026: [ADR-0009](0009-qr-co-dinh-tai-ban.md) thay mã tham gia bằng QR cố định. Những đoạn mô tả joinCode/HMAC/QR chỉ có link bên dưới là lịch sử quyết định trước; xem P1.3 và API docs cho hành vi mới.

> Cập nhật triển khai02/11/2026: bản MVP localhost đã chạy. [ADR-0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) thay thế các lựa chọn/giới hạn khác biệt bên dưới; [API thực thi](../docs/api/README.md) và [trạng thái](../docs/IMPLEMENTATION-STATUS.md) là bằng chứng hiện tại. Phần ghi chưa triển khai ở nội dung gốc mô tả thời điểm thiết kế ban đầu.

Ngày 02/11/2026 · Thiết kế, chưa tạo/chạy Compose, chưa pull image hoặc sửa máy.

## 1. Quyết định triển khai

Toàn bộ build, chạy, migration và test trong container. Host chỉ cần Docker Desktop Linux containers + Compose v2 và Git; SDK/Node/npm host không là điều kiện chạy dự án. Dịch vụ chính: `postgres`, `migrate`, `backend`, `frontend`, `gateway`. `backend` gọi `postgres:5432`, không `localhost`; browser gọi gateway. Không public cổng PostgreSQL mặc định.

| Service | Build/runtime | Vòng đời / phụ thuộc |
|---|---|---|
| postgres | `postgres:16-alpine`, pin digest lúc chốt | `restart: unless-stopped`, volume dữ liệu, healthcheck |
| migrate | Image backend migration target, EF bundle | one-shot, `restart: "no"`; chờ postgres healthy |
| backend | SDK .NET 10 build → aspnet runtime 10, non-root | restart unless-stopped; chỉ start sau migrate thành công |
| frontend | Node 24 build → standalone Next runtime, non-root | restart unless-stopped; readiness HTTP |
| gateway | Caddy 2 image được pin | route /api/v1 tới backend, còn lại frontend; HTTPS deployment |

## 2. Compose contract

Đây là **đoạn minh họa** cho Compose sẽ tạo tại F01/DB03, không phải file chạy hiện tại. Phần backend/frontend/gateway cần Dockerfile và health endpoint được implement trước. [Docker Compose](https://docs.docker.com/compose/how-tos/startup-order/) phân biệt container running với healthy và hỗ trợ chờ job hoàn tất thành công.

```yaml
services:
  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB:?required}
      POSTGRES_USER: ${POSTGRES_BOOTSTRAP_USER:?required}
      POSTGRES_PASSWORD: ${POSTGRES_BOOTSTRAP_PASSWORD:?required}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 3s
      retries: 20
      start_period: 10s

  migrate:
    build:
      context: ./Backend
      target: migrations
    restart: "no"
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__Database: ${MIGRATION_CONNECTION_STRING:?required}
    # Entrypoint image chạy EF bundle với connection string từ env.

  backend:
    build:
      context: ./Backend
      target: runtime
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
      migrate:
        condition: service_completed_successfully
    environment:
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Database: ${APP_CONNECTION_STRING:?required}

volumes:
  postgres_data:
```

File thật bổ sung frontend/gateway, healthchecks, mạng private cho DB, secret mounts, cấu hình log rotation và resource limits sau khi đo. Không `container_name` cố định để nhiều Compose project/test không xung đột. `compose.dev.yml` bind mount source + named volume node_modules và NuGet cache trong container; `compose.test.yml` DB/volume riêng. Không mount Docker socket vào app.

## 3. Cấu hình và secrets

`.env.example` mô tả tên biến, không secret thật. `.env` bị gitignore; môi trường deploy dùng Compose secrets/file mount đọc từ ứng dụng. Environment trong snippet chỉ để diễn đạt kết nối local, không khẳng định nó che secret khỏi người có quyền Docker inspect.

| Cấu hình | Nơi dùng | Quy tắc |
|---|---|---|
| POSTGRES_DB / BOOTSTRAP_USER/PASSWORD | postgres init | User bootstrap chỉ provisioning, không cấp cho backend |
| MIGRATION_CONNECTION_STRING | migrate | Host=postgres; Port=5432; quyền DDL trong DB dự án |
| APP_CONNECTION_STRING | backend | User DML, không superuser/CREATE/ALTER; chỉ SELECT/INSERT cho payments/audit, không UPDATE/DELETE |
| AUTH_TOKEN_PEPPER / JOIN_CODE_HMAC_KEY | backend | Secret ngoài DB; HMAC join code; rotate cần kế hoạch thu hồi mã |
| RESPONSE_ENCRYPTION_KEY / KEY_ID | backend | Mã hóa response idempotency; lưu keyring để replay/restore được |
| INTERNAL_API_URL | frontend server | http://backend:8080; không NEXT_PUBLIC |
| PUBLIC_ORIGIN | gateway/backend | Dùng Origin/CSRF validation; môi trường thực HTTPS |
| BOOTSTRAP_ADMIN_PASSWORD | seed job lần đầu | Secret cấp khi seed; bắt buộc đổi, không commit mật khẩu demo mặc định |

Postgres init scripts chỉ provision roles/grants khi volume lần đầu; migrations vẫn là nguồn schema và phải chạy cả với volume đã có. Khi bổ sung quyền cho bảng mới phải migration GRANT rõ. Không dựa vào `/docker-entrypoint-initdb.d` để nâng schema. Secret/volume permission ngoài workspace chưa kiểm tra và chưa sửa.

## 4. Chiến lược migration

Chọn **EF Core migrations + snapshot**, Npgsql cùng major 10. Migration có SQL thủ công cho partial indexes, trigger functions/constraint triggers/grants mà fluent mapping không biểu đạt hết. EF history là nguồn phiên bản áp dụng, không có Flyway/SQL scripts song song làm nguồn khác.

[Hướng dẫn EF](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) yêu cầu review/test migration trước production và hỗ trợ deployment bundle. Baseline dùng bundle theo image release, không gọi `Database.Migrate()` trong startup mọi API replica.

### Các mốc migration dự kiến

| Mốc | Nội dung |
|---|---|
| M001 InitialSchema | Toàn bộ 15 bảng ứng dụng, FK theo dependency graph, index/CHECK/UNIQUE và seed role cố định |
| M002 BusinessIntegrity | State triggers, deferred constraints visit/order/batch/item/payment, khóa bất biến và kiểm tổng |
| M003 ApplicationPrivileges | Quyền DML từng bảng, append-only audit/payment, các quyền công cụ migration |

Đây là nhóm thay đổi logic, không bắt buộc đúng ba migration vật lý; tên migration thực tế theo EF timestamp và được ghi khi DB01–03 triển khai. M001 tạo bảng đích trước khi tạo FK đến bảng đó; với quan hệ vòng thì tạo bảng trước, ALTER ADD FK sau trong cùng migration. Không cho API traffic vào database mới chỉ chạy một phần schema baseline. Tạo sẵn schema billing không đồng nghĩa tính năng B01 đã được implement.

### Quy trình thêm migration

1. Sửa SSOT/ADR-0003 nếu schema thay; sửa EF mapping và logic.
2. Trong SDK container chạy `dotnet ef migrations add <TenThayDoi>` đúng Infrastructure project và Api startup project; review migration + snapshot.
3. Export SQL idempotent để reviewer xem; không coi SQL export là migration độc lập cần sửa song song.
4. Test tạo DB sạch và upgrade từ phiên bản trước có seed đại diện; test dữ liệu cũ, constraints, index, trigger và rollback transaction.
5. CI build bundle cùng release API; migration job dùng quyền DDL, app dùng quyền DML. Hai runner cùng đích phải được ngăn bằng khóa deployment/advisory lock trước chạy bundle; job thứ hai chờ và kiểm history.
6. Bundle exit!=0: không đưa API mới lên; log/giữ DB để điều tra, không tự xóa volume.

Lệnh minh họa chỉ dùng sau F01 tạo service `backend-dev`/`backend-test` trong compose override:

```powershell
docker compose -f compose.yml -f compose.dev.yml run --rm backend-dev dotnet ef migrations add AddExample --project src/Restaurant.Infrastructure --startup-project src/Restaurant.Api
docker compose -f compose.yml -f compose.dev.yml run --rm backend-dev dotnet ef migrations script --idempotent --project src/Restaurant.Infrastructure --startup-project src/Restaurant.Api
docker compose -f compose.yml -f compose.test.yml run --rm backend-test dotnet test
```

Các service này **chưa tồn tại** ở giai đoạn tài liệu; không hướng dẫn chạy ngay trên repo trống.

## 5. Seed và nâng cấp an toàn

- Seed role là deterministic migration data; seed bootstrap admin là job riêng idempotent, chỉ tạo nếu chưa có ADMIN và không reset mật khẩu ở lần chạy sau.
- Demo seed tách profile/job: 30 bàn, menu tối thiểu 20 món/4 danh mục, user từng role, item hết/ẩn; không tự bật seed demo trong môi trường thật.
- Migration đã áp dụng không sửa/xóa. Thêm cột: expand nullable/default phù hợp → backfill → validate → set NOT NULL; đổi tên dùng rename, không drop/add làm mất dữ liệu.
- Thay state CHECK cần cho phép dữ liệu đang tồn tại, chuyển dữ liệu rồi thu hẹp. UNIQUE mới kiểm tra duplicates trước. Index concurrently cần migration không bọc transaction riêng và quy trình retry rõ, chỉ dùng khi thật cần.
- CI kiểm model pending changes, migration repeat không tạo bản ghi duplicate; không dùng EnsureCreated cho app hoặc test migration.

## 6. Backup, restore và rollback

Runbook sẽ tạo job PostgreSQL client 16 chạy `pg_dump -Fc` ra file backup có timestamp trong vùng được mount; không chỉ copy thư mục volume lúc DB đang chạy. Giữ backup DB + release image digest + migration version + keyring secrets ngoài repo; thiếu encryption/HMAC key có thể không replay được response hoặc dùng lại mã tham gia.

Trước migration phá hủy/đổi dữ liệu cần backup và thử restore. Khi release lỗi, ưu tiên fix-forward; chỉ rollback app nếu schema tương thích ngược. Không tự chạy Down trên DB có dữ liệu thật. Nếu buộc restore: dừng ghi, restore sang **database/volume mới**, kiểm tra data/constraints/login/luồng mẫu rồi mới chuyển kết nối; giữ bản cũ để điều tra. Không dùng `docker compose down -v` như cách sửa migration.

Mục tiêu đề xuất cho demo có dữ liệu: backup hằng ngày và trước migration, giữ 7 bản ngày; RPO 24h, RTO 1h là mục tiêu cần đo qua restore drill, chưa có SLA vận hành. Trong demo học phần có thể backup thủ công trước buổi nghiệm thu.

## 7. Acceptance của hạ tầng

Máy mới chỉ có Docker + Git dựng đủ stack từ README/runbook; migration fail ngăn API mới; restart container không mất order/payment; DB không public mặc định; restore backup sang DB mới chạy được kiểm tra tiền/phiên theo policy; không secret thật trong git. Được kiểm chứng tại F01, DB03 và QA03, chưa tuyên bố đã pass.

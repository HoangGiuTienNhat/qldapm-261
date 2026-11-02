# ADR-0001 — Lựa chọn tech stack

> Đối chiếu database 03/11/2026: hiện dùng 4 EF SQL-first migrations, chưa có ModelSnapshot/bundle và chưa hỗ trợ Down tự động. Cây/lệnh scaffolding dự kiến dưới đây không phải cách chạy hiện tại; xem [ADR-0004](0004-docker-va-migration.md), [từng bảng](../docs/db/README.md) và [hướng dẫn migration](../GUIDE/DATABASE-MIGRATIONS.md).

> Cập nhật triển khai02/11/2026: bản MVP localhost đã chạy. [ADR-0008](0008-mvp-local-va-quyet-dinh-trien-khai.md) thay thế các lựa chọn/giới hạn khác biệt bên dưới; [API thực thi](../docs/api/README.md) và [trạng thái](../docs/IMPLEMENTATION-STATUS.md) là bằng chứng hiện tại. Phần ghi chưa triển khai ở nội dung gốc mô tả thời điểm thiết kế ban đầu.

Ngày: 02/11/2026 · Trạng thái: **đã lựa chọn cho baseline, chưa cài thêm công cụ hay tạo code**.

## 1. Bối cảnh

Nhà hàng một cơ sở, 20–30 bàn, tối đa 50 thiết bị mô phỏng; trọng tâm là giao dịch, phân quyền, state machine, audit và migration. Frontend React/Next.js và PostgreSQL/Docker Compose là yêu cầu trực tiếp. Không có code nền trong `Backend/`/`Frontend/` khi khảo sát.

## 2. Bằng chứng trên máy

Các lệnh chỉ đọc đã chạy tại workspace; không suy diễn “đã cài” thành “production sẵn sàng”.

| Thành phần | Kết quả quan sát | Ý nghĩa |
|---|---|---|
| Docker CLI | 28.5.1 | Có CLI |
| Docker Compose | v2.40.3-desktop.1 | Có Compose plugin |
| Docker Desktop | Có executable trong Program Files | Chưa xác minh daemon/Linux containers hoạt động; CLI cảnh báo không đọc được `.docker/config.json` do quyền của phiên chạy |
| .NET SDK | 8.0.422 | Có nền tảng C#, chưa có SDK 10 trên host |
| Go | go1.25.4 windows/amd64 | Có toolchain, không dùng nguyên bản cũ làm chuẩn container |
| Java / javac | Oracle JDK 23 | Có JDK; không phải lựa chọn LTS container mặc định |
| Maven | 3.9.9, chạy thành công với JDK 23 | Java/Spring khả thi; Gradle không thấy trên PATH |
| Node.js | v22.14.0 | Có Node trên host |
| npm | Có command nhưng `npm --version` lỗi MODULE_NOT_FOUND tại npm-cli.js trong Roaming | Môi trường host chưa đáng tin cậy cho cài frontend |
| pnpm | 11.25.0 | Lệnh chạy được; vẫn dùng package manager khóa phiên bản trong Docker |
| Git / Python | Có executable | Dùng cho quản lý repo/kiểm tra tài liệu |

Không đo hiệu năng JVM/.NET/Go trên máy, không kiểm tra năng lực từng thành viên; các nhận xét về độ thuận tiện dưới đây là đánh giá thiết kế, không phải benchmark.

## 3. So sánh lựa chọn backend

| Tiêu chí | C# / ASP.NET Core | Go | Java / Spring Boot |
|---|---|---|---|
| Có trên máy | SDK 8, cần SDK 10 trong image | Có 1.25.4, vẫn cần image được hỗ trợ | JDK 23 + Maven có sẵn, có thể build trong image LTS |
| RBAC, validation, OpenAPI | Tích hợp tốt với policy/DI/middleware | Làm được; cần ghép nhiều quy ước/thư viện | Spring Security/Validation đầy đủ |
| PostgreSQL / migration | EF Core + Npgsql, migration/snapshot đồng bộ model | pgx + sqlc + goose khả thi, SQL rõ nhưng thêm bước generator | JPA + Flyway/Liquibase mạnh, cần quản lý entity/lazy loading |
| Nghiệp vụ nhiều trạng thái | C# type system và service/transaction phù hợp | Phù hợp, yêu cầu tự xây nền policy/validation nhất quán | Rất phù hợp, stack lớn hơn nhu cầu MVP |
| Container | Multi-stage SDK/runtime chuẩn | Binary nhỏ, startup nhanh | Container JVM phổ biến; cần tuning memory hợp lý |
| Đánh đổi | Cần nâng major so với host; tránh quá nhiều abstraction | Nhiều mã nền auth/admin/migration phải kết nối thủ công | Nhiều cấu hình/framework và khái niệm hơn cho nhóm nhỏ |

**Chọn C# / ASP.NET Core .NET 10**. Go hoặc Spring Boot đều đủ tải; không có bằng chứng để loại chúng vì hiệu năng. Lợi thế quyết định là tốc độ xây nghiệp vụ quan hệ, policy và EF migrations đồng bộ trong một stack vừa phải. Nếu nhóm có kinh nghiệm Java/Go vượt trội, có thể thay qua ADR mới trước F01, không cần thay yêu cầu SSOT.

Không chọn .NET 8 chỉ vì đã có sẵn: theo [chính sách hỗ trợ .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), .NET 8 hết hỗ trợ 10/11/2026, còn .NET 10 LTS đến 14/11/2028. Build/run bằng image .NET 10 nên không buộc cài SDK 10 host. [Npgsql EF provider 10](https://www.npgsql.org/efcore/release-notes/10.0.html) hỗ trợ thế hệ EF Core 10.

## 4. Bộ công nghệ mục tiêu

| Lớp | Chọn | Mục đích |
|---|---|---|
| API | ASP.NET Core 10 / C# / REST | Endpoint theo nghiệp vụ, policy, ProblemDetails |
| Dữ liệu | EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL 10 | Mapping, explicit transaction, concurrency token, migrations |
| Database | `postgres:16-alpine` | PostgreSQL 16 theo yêu cầu; pin digest khi triển khai |
| Frontend | Next.js 16 App Router + React tương thích peer dependency + TypeScript strict | Responsive theo vai trò, chạy Node server container |
| UI | Tailwind CSS; component đơn giản, không thêm design system lớn mặc định | Mobile/tablet/desktop |
| Server state | TanStack Query | Polling, invalidation, trạng thái request; không coi cache là DB |
| Form | React Hook Form + Zod | Validation trải nghiệm; backend kiểm tra độc lập |
| Node/package | Node 24 LTS image + npm đi kèm image, package-lock.json | Tách khỏi npm host đang lỗi |
| Test backend | xUnit + PostgreSQL test container/service Compose | Test quy tắc, transaction, constraint thực |
| Test frontend | Vitest/Testing Library cho logic quan trọng; Playwright cho E2E | Luồng đủ vai trò |
| API thử nghiệm | OpenAPI + Postman CLI nếu dùng collection trong repo | Postman có sẵn theo thông tin người dùng; không bắt buộc tài khoản cloud |
| Logging | ILogger structured JSON + traceId | Tra lỗi; không log token/mật khẩu |
| Triển khai | Docker multi-stage + Compose + Caddy reverse proxy | Cùng origin; HTTPS ở môi trường triển khai |

[Next.js hỗ trợ Docker với đầy đủ tính năng](https://nextjs.org/docs/app/getting-started/deploying). [Yêu cầu cài đặt Next.js](https://nextjs.org/docs/app/getting-started/installation) ghi Node tối thiểu 20.9; chọn Node 24 theo dòng [LTS của Node](https://nodejs.org/en/about/previous-releases), không dùng mức tối thiểu làm mặc định lâu dài. [Spring Boot](https://docs.spring.io/spring-boot/system-requirements.html) cũng có lựa chọn JDK được hỗ trợ; thiếu runtime không phải lý do loại Spring.

## 5. Cách khóa phiên bản

Các số major ở đây là lựa chọn kiến trúc; F01 xác minh patch còn hỗ trợ, tương thích EF/Npgsql/React/Next và pin SDK image digest, NuGet exact version, package-lock. Không dùng `latest` trong bản demo được chốt. Không sao chép kết quả trang tài liệu động thành khẳng định patch đã test. Commit lockfiles; upgrade có PR và kiểm thử luồng chính.

Không thêm Redis, RabbitMQ, Kafka, Kubernetes, microservices, Elasticsearch hay payment gateway ở MVP. Database vẫn là nguồn trạng thái duy nhất; polling đáp ứng quy mô nếu vượt qua kiểm thử tải.

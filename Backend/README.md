# Backend — bản MVP đã triển khai

**C# / ASP.NET Core .NET10**, Npgsql EF10, PostgreSQL16. `Restaurant.Api/Program.cs` có55 endpoint; Domain chứa entity/rules, Data chứa mapping/SQL-first EF migrations/seed, Services chứa use case theo nhóm nghiệp vụ. `Restaurant.Tests` có25 unit test trọng điểm. Dockerfile có target runtime/tests.

- [Kiến trúc/cấu trúc mục tiêu](../ADR/0002-kien-truc-va-cau-truc.md).
- [Schema và ràng buộc đầy đủ](../ADR/0003-database.md).
- [Docker/migration/runbook dự kiến](../ADR/0004-docker-va-migration.md).
- [API/auth/concurrency](../ADR/0005-api-va-nhat-quan.md).
- [Task triển khai và AC](../ADR/0006-implementation-plan.md).
- [SSOT nghiệp vụ](../SSOT/README.md).

Run tại repo root: `docker compose up -d --build`. Unit: `docker build --target tests -t restaurant-ordering-tests ./Backend`. Chi tiết triển khai khác với cây mục tiêu ban đầu tại [ADR-0008](../ADR/0008-mvp-local-va-quyet-dinh-trien-khai.md); hiện SQL-first migrations, chưa có code-first snapshot generator. [API từng endpoint](../docs/api/README.md) · [Postman](../docs/api/POSTMAN.md).

# Frontend — bản MVP đã triển khai

**Next.js16.4.4 / React19.3.1 / TypeScript**, Node24 container. Có production standalone build và package-lock. `src/components/RestaurantApp.tsx` là UI theo vai trò; `src/lib/api.ts` xử lý cookie/CSRF/errors/idempotency; `src/app` gồm trang gốc và catch-all route cho QR/customer/staff.

- [Kiến trúc/cây route và thư mục](../ADR/0002-kien-truc-va-cau-truc.md).
- [Lựa chọn stack và lock phiên bản](../ADR/0001-tech-stack.md).
- [API, cookies, polling và xử lý mất response](../ADR/0005-api-va-nhat-quan.md).
- [Task UI/tích hợp/UAT](../ADR/0006-implementation-plan.md).
- [SSOT nghiệp vụ](../SSOT/README.md).

Run tại repo root: `docker compose up -d --build`, mở `http://localhost:3000`; F12 → Ctrl+Shift+M để xem mobile/tablet. Browser gọi API cùng origin qua Caddy, không DB trực tiếp. CSS responsive riêng, chưa thêm Tailwind/TanStack/Zod theo [ADR-0008](../ADR/0008-mvp-local-va-quyet-dinh-trien-khai.md). Nhiều tab cùng profile chia sẻ staff cookie; dùng profile/ẩn danh riêng cho nhiều role. Khách có header X-Actor:guest để chọn cookie khách khi cùng giữ cookie staff.

QR cố định: `TableQrDialog.tsx`, qrcode1.5.4 render PNG cục bộ; `copy-table-url.ts` có3 unit tests. Chạy `docker build --target tests -t restaurant-ordering-frontend-tests ./Frontend`. QR page tự join bằng token, không mã nhập. [Hướng dẫn](../GUIDE/QR-TAI-BAN.md).

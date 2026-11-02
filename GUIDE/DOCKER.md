# Docker từ lần chạy đầu đến các lần sau

Áp dụng cho Windows/PowerShell và `compose.yml` của repo này. Tất cả lệnh bên dưới giới hạn vào project **restaurant_ordering** hoặc project thử mới được chỉ định. **Không có bước xóa Docker image, dọn Docker toàn máy hay xóa volume dữ liệu.**

## 1. Hiểu những thứ Docker đang giữ

| Thành phần | Trong dự án | Khi dừng/dựng lại |
|---|---|---|
| Image | .NET, Node, PostgreSQL, Caddy và image build của ứng dụng | Được giữ; build lại có thể tạo image/layer mới, không xóa image khác |
| Container | postgres, migrate, backend, frontend, gateway | Có thể dừng hoặc tạo lại |
| Named volume | postgres_data và auth_keys, có prefix project | Giữ DB và keyring qua việc tạo lại container |
| Network | Mạng riêng của Compose project | Có thể tạo lại khi `down`/`up` |
| Source và `.env` | File trên máy bạn | Không bị Compose xóa |

`migrate` là job một lần, **Exited(0) là thành công**, không phải service bị lỗi. Backend/frontend/gateway phải Up; postgres phải healthy.

## 2. Lần chạy đầu

1. Mở Docker Desktop, đợi Docker Engine sẵn sàng và dùng Linux containers.
2. Mở PowerShell tại repo:

```powershell
Set-Location 'E:\bku\261\QLDAPM\Repo'
docker version
docker compose version
```

`docker version` cần có phần Server; chỉ có Client hoặc lỗi pipe là Docker Engine chưa chạy/quyền kết nối chưa đủ.

3. Tạo `.env` **chỉ nếu chưa có**, tránh ghi đè cấu hình bạn đã sửa:

```powershell
if (-not (Test-Path -LiteralPath '.env')) {
    Copy-Item -LiteralPath '.env.example' -Destination '.env'
}
```

Compose có fallback `${TEN_BIEN:-gia_tri_mac_dinh}`, nên repo vẫn chạy demo được nếu thiếu `.env`. `.env.example` chỉ là mẫu và không được Compose tự đọc. Biến đang có trong PowerShell có thể ưu tiên hơn `.env`; xem [quy tắc biến môi trường Docker](https://docs.docker.com/compose/how-tos/environment-variables/envvars-precedence/).

4. Kiểm tra cấu hình và chạy:

```powershell
docker compose -p restaurant_ordering config --quiet
docker compose -p restaurant_ordering up -d --build
docker compose -p restaurant_ordering ps -a
```

Lần đầu tải image/dependency có thể lâu. Thứ tự: PostgreSQL healthy → migration/seed hoàn tất → backend → frontend/gateway. Mở [localhost:3000](http://localhost:3000), hoặc PORT bạn đặt trong `.env`. `-d` giữ service chạy nền khi đóng terminal.

Seed mặc định: 30 bàn, 20 món, tài khoản admin/manager/waiter/kitchen/cashier. Mật khẩu local theo `DEMO_PASSWORD`; xem README. Các lần seed sau không reset tài khoản/món đã chỉnh.

## 3. Những lần sử dụng sau

Khi chỉ cần tiếp tục dùng và cấu hình/code chưa đổi:

```powershell
docker compose -p restaurant_ordering up -d
```

Nếu đã `stop` và muốn bật đúng các container đang có, có thể dùng `docker compose -p restaurant_ordering start`. Lệnh `up -d` tổng quát hơn vì tạo container còn thiếu và kiểm tra dependency. Migrate được chạy lại khi cần, các migration đã ghi history không áp dụng hai lần.

## 4. Dừng đúng phạm vi

### Dừng tạm, giữ nguyên container

```powershell
docker compose -p restaurant_ordering stop
```

Không xóa container/image/volume. Có thể đóng Docker Desktop sau khi dừng; lần sau mở Docker Desktop trước rồi `up -d`.

### Dừng và gỡ container/network của riêng dự án

```powershell
docker compose -p restaurant_ordering down
```

Lệnh này **không kèm `-v` hoặc `--rmi`**, nên named volumes và images được giữ. Dữ liệu vẫn còn khi `up` lại. Đây là hành vi mặc định được mô tả trong [Docker Compose down](https://docs.docker.com/reference/cli/docker/compose/down/).

Không dùng các lệnh dừng toàn bộ container của máy; có thể ảnh hưởng những dự án khác đang chạy.

## 5. Sau khi sửa code, dependency hoặc `.env`

```powershell
docker compose -p restaurant_ordering up -d --build
```

Docker build dùng cache cho bước không đổi; `.env`/config thay đổi được đưa vào container mới. `restart` chỉ khởi động lại container cũ, **không build source mới và không áp cấu hình môi trường mới**.

Nếu cần buộc tạo lại mọi container của dự án nhưng vẫn giữ dữ liệu:

```powershell
docker compose -p restaurant_ordering up -d --build --force-recreate
```

Theo [Docker Compose up](https://docs.docker.com/reference/cli/docker/compose/up/), việc recreate giữ mounted volumes. Không cần xóa image/cache để lấy code mới. Khi restart riêng backend/frontend, nếu gateway trả502 do container đổi IP, chạy `docker compose -p restaurant_ordering restart gateway`.

**Lưu ý database:** POSTGRES_USER/PASSWORD/DB chỉ khởi tạo database khi volume rỗng. Đổi password trong `.env` không tự đổi password tài khoản đã tồn tại trong PostgreSQL. DEMO_PASSWORD cũng không reset mật khẩu user đã seed. Nếu cấu hình cũ đang dùng được, không đổi những biến này để thử sửa lỗi login; đọc log và xác định nguyên nhân trước.

## 6. Dựng lại container từ đầu, giữ database hiện tại

```powershell
docker compose -p restaurant_ordering down
docker compose -p restaurant_ordering up -d --build
```

Kết quả: container/network mới; DB, tài khoản, đơn, payment và keyring cũ còn nguyên. Không image nào bị xóa.

## 7. Chạy môi trường mới hoàn toàn mà không xóa dữ liệu cũ

Để có database trắng và seed mới, cách an toàn là dùng **project name mới** và cổng khác, thay vì xóa volume cũ. Docker [dùng project name để cô lập môi trường](https://docs.docker.com/compose/how-tos/project-name/). Ví dụ dưới dùng `restaurant_ordering_fresh_01`; nếu tên đó từng chạy thì nó sẽ dùng lại dữ liệu của chính môi trường đó, hãy chọn suffix mới khi cần thật sự trắng.

Trong PowerShell tại repo:

```powershell
$restaurantPreviousPort = $env:PORT
try {
    $env:PORT = '3001'
    docker compose -p restaurant_ordering_fresh_01 up -d --build
    docker compose -p restaurant_ordering_fresh_01 ps -a
}
finally {
    if ($null -eq $restaurantPreviousPort) {
        Remove-Item Env:PORT -ErrorAction SilentlyContinue
    }
    else {
        $env:PORT = $restaurantPreviousPort
    }
}
```

Mở [localhost:3001](http://localhost:3001). Môi trường mới có named volumes/keyring/network riêng và có thể chạy song song bản cũ3000. Image nền có thể được dùng chung; không image nào bị xóa. Cổng3001 phải đang trống. `.env` vẫn là file cấu hình chung được đọc để khởi tạo môi trường mới; thay `$env:PORT` trong khối try chỉ để override cổng và được khôi phục ngay sau đó.

Những lần chạy lại **môi trường mới này** phải dùng đúng project và PORT:

```powershell
$restaurantPreviousPort = $env:PORT
try {
    $env:PORT = '3001'
    docker compose -p restaurant_ordering_fresh_01 up -d
}
finally {
    if ($null -eq $restaurantPreviousPort) { Remove-Item Env:PORT -ErrorAction SilentlyContinue }
    else { $env:PORT = $restaurantPreviousPort }
}
```

Dừng riêng môi trường mới bằng `docker compose -p restaurant_ordering_fresh_01 stop`; muốn gỡ container/network của nó dùng `down` không thêm tùy chọn xóa. Muốn quay về bản cũ: `docker compose -p restaurant_ordering up -d` rồi mở3000 theo cấu hình cũ. Đừng dùng project mới hàng ngày nếu bạn muốn tiếp tục dữ liệu hiện tại.

## 8. Xem trạng thái và xử lý lỗi thường gặp

```powershell
docker compose -p restaurant_ordering ps -a
docker compose -p restaurant_ordering logs --tail 100 migrate
docker compose -p restaurant_ordering logs --tail 100 backend
docker compose -p restaurant_ordering logs --tail 100 frontend
docker compose -p restaurant_ordering logs --tail 100 gateway
```

| Hiện tượng | Cách xử lý |
|---|---|
| Không kết nối được Docker pipe | Mở Docker Desktop, chờ Engine sẵn sàng; kiểm quyền kết nối, không sửa/xóa image |
| Cổng3000 bận | Đổi PORT trong `.env` sang cổng trống rồi `up -d`; URL/cookie theo cổng mới |
| Migrate Exited(0) | Bình thường; job đã hoàn thành |
| Migrate Exited khác0 | Xem log migrate; không xóa volume để che lỗi; API mới chưa nên chạy |
| Đổi code nhưng UI cũ | `up -d --build`, đợi build xong rồi refresh trình duyệt |
| DB password authentication failed | Kiểm tra cấu hình khớp database đã khởi tạo; thay `.env` không tự sửa password trong volume |
| Website502 sau recreate | Kiểm tra backend/frontend đang Up rồi restart riêng gateway |
| Thấy tài khoản nhân viên khác ở tab mới | Cookie chung profile; dùng profile/browser/ẩn danh riêng |

`docker compose config` không có `--quiet` có thể in connection strings/secret; không chụp và gửi output công khai. Chỉ chia sẻ log cần thiết theo traceId, che thông tin nhạy cảm.

## 9. Backup trước những thay đổi dữ liệu

Tạo backup **bên trong container**, rồi `docker compose cp` ra host để tránh lỗi encoding khi redirect file nhị phân qua Windows PowerShell:

```powershell
New-Item -ItemType Directory -Path '.\artifacts\backups' -Force | Out-Null
docker compose -p restaurant_ordering exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /tmp/restaurant.backup'
docker compose -p restaurant_ordering cp postgres:/tmp/restaurant.backup ./artifacts/backups/restaurant.backup
```

Đổi tên file trên host theo ngày để không ghi đè bản backup cần giữ. Backup DB chưa bao gồm `/keys` trong auth_keys volume hoặc `.env`; các thành phần này cũng cần được giữ an toàn nếu muốn khôi phục replay idempotency sau restore. Chưa có restore drill đầy đủ trong MVP; không thử restore ghi đè DB đang dùng.

## 10. Những thao tác không cần cho quy trình này

Không dùng `docker system prune`, `docker image prune`, `docker volume prune`, `docker image rm`, `docker rmi` hoặc `compose down --rmi` để khởi động dự án. Không dùng `down -v` khi muốn giữ dữ liệu. Không chọn Factory reset/Clean data của Docker Desktop để xử lý riêng dự án này. Các thao tác đó có thể xóa image/dữ liệu quan trọng của bạn hoặc dự án khác.

Tài liệu này chỉ hướng dẫn dừng/dựng lại **project được chỉ định**. Người soạn chưa thực hiện bất kỳ thao tác xóa image/volume hay reset Docker toàn máy nào.

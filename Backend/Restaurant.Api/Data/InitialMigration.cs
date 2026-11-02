using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Restaurant.Api.Data;
[DbContext(typeof(AppDb))]
[Migration("202611020001_InitialSchema")]
public class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        using var stream = typeof(InitialMigration).Assembly.GetManifestResourceStream("Restaurant.Api.Data.schema.sql")!;
        using var reader = new StreamReader(stream);
        m.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Không tự động xóa dữ liệu nghiệp vụ. Restore backup hoặc thêm migration sửa lỗi.");
}

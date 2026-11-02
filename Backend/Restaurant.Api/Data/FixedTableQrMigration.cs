using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Restaurant.Api.Data;

[DbContext(typeof(AppDb))]
[Migration("202611020003_FixedTableQr")]
public class FixedTableQrMigration : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
        ALTER TABLE restaurant_tables ADD COLUMN qr_token_ciphertext bytea
          CHECK(qr_token_ciphertext IS NULL OR octet_length(qr_token_ciphertext)>0);
        CREATE OR REPLACE FUNCTION guard_visit() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE table_capacity smallint; table_active boolean;
        BEGIN
          IF TG_OP='INSERT' THEN
            SELECT capacity,is_active INTO table_capacity,table_active FROM restaurant_tables WHERE id=NEW.table_id FOR UPDATE;
            IF NOT table_active OR NEW.party_size>table_capacity THEN RAISE EXCEPTION 'table unavailable or capacity exceeded' USING ERRCODE='23514'; END IF;
          ELSE
            IF NEW.table_id<>OLD.table_id OR OLD.status<>'OPEN' THEN RAISE EXCEPTION 'invalid visit change' USING ERRCODE='23514'; END IF;
          END IF;
          RETURN NEW;
        END $$;
        ALTER TABLE visits DROP COLUMN join_code_hmac, DROP COLUMN join_generation;
        ALTER TABLE guest_sessions DROP COLUMN join_generation;
        """);
    protected override void Down(MigrationBuilder m)=>throw new NotSupportedException("Use a forward migration or restore backup.");
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Restaurant.Api.Data;

[DbContext(typeof(AppDb))]
[Migration("202611030004_PaymentRecipientHistory")]
public class PaymentHistoryMigration : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
        -- Preserve development data as-is. Only new INSERTs must carry snapshots.
        -- The existing append-only trigger remains enabled throughout this migration.
        ALTER TABLE payments ADD COLUMN received_by_username_snapshot varchar(64),
          ADD COLUMN received_by_display_name_snapshot varchar(120),
          ADD CONSTRAINT ck_payment_recipient_snapshot CHECK(
            (received_by_username_snapshot IS NULL AND received_by_display_name_snapshot IS NULL) OR
            (received_by_username_snapshot IS NOT NULL AND received_by_display_name_snapshot IS NOT NULL
             AND btrim(received_by_username_snapshot)<>'' AND btrim(received_by_display_name_snapshot)<>''));
        CREATE FUNCTION guard_payment_recipient() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE recipient_username text; recipient_name text;
        BEGIN
          SELECT username,display_name INTO recipient_username,recipient_name
            FROM staff_users WHERE id=NEW.received_by_staff_id FOR SHARE;
          IF NEW.received_by_username_snapshot IS NULL OR NEW.received_by_display_name_snapshot IS NULL OR
            NEW.received_by_username_snapshot IS DISTINCT FROM recipient_username OR
            NEW.received_by_display_name_snapshot IS DISTINCT FROM recipient_name THEN
            RAISE EXCEPTION 'payment recipient snapshot must match staff at payment time' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER payment_recipient_guard BEFORE INSERT ON payments FOR EACH ROW EXECUTE FUNCTION guard_payment_recipient();
        CREATE INDEX ix_payments_history ON payments(paid_at DESC,id DESC);
        CREATE INDEX ix_payments_receiver_history ON payments(received_by_staff_id,paid_at DESC,id DESC);
        CREATE INDEX ix_payments_method_history ON payments(method,paid_at DESC,id DESC);
        CREATE FUNCTION guard_paid_order_history_delete() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE parent_order uuid;
        BEGIN
          IF TG_TABLE_NAME='order_batches' THEN parent_order=OLD.order_id;
          ELSE SELECT order_id INTO parent_order FROM order_batches WHERE id=OLD.batch_id;
          END IF;
          IF EXISTS(SELECT 1 FROM orders WHERE id=parent_order AND status='PAID') THEN
            RAISE EXCEPTION 'paid order history must be retained' USING ERRCODE='23514';
          END IF;
          RETURN OLD;
        END $$;
        CREATE TRIGGER paid_item_history BEFORE DELETE ON order_items FOR EACH ROW EXECUTE FUNCTION guard_paid_order_history_delete();
        CREATE TRIGGER paid_batch_history BEFORE DELETE ON order_batches FOR EACH ROW EXECUTE FUNCTION guard_paid_order_history_delete();
        """);
    protected override void Down(MigrationBuilder m)=>throw new NotSupportedException("Use a forward migration or restore backup.");
}

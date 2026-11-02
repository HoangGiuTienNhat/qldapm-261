using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Restaurant.Api.Data;

[DbContext(typeof(AppDb))]
[Migration("202611020002_IntegrityGuards")]
public class IntegrityMigration : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
        DO $$ DECLARE t text; BEGIN
          FOREACH t IN ARRAY ARRAY['staff_users','menu_categories','menu_items','restaurant_tables','visits','orders','order_batches','order_items'] LOOP
            EXECUTE format('ALTER TABLE %I ALTER COLUMN created_at SET DEFAULT now(), ALTER COLUMN updated_at SET DEFAULT now(), ALTER COLUMN version SET DEFAULT 1',t);
            EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I CHECK(updated_at>=created_at)',t,'ck_'||t||'_timestamps');
          END LOOP;
        END $$;
        ALTER TABLE audit_events ADD CONSTRAINT ck_audit_json CHECK((before_data IS NULL OR jsonb_typeof(before_data)='object') AND (after_data IS NULL OR jsonb_typeof(after_data)='object'));
        ALTER TABLE audit_events ADD CONSTRAINT ck_audit_target CHECK(target_type IN ('STAFF_USER','USER_ROLE','MENU_CATEGORY','MENU_ITEM','TABLE','VISIT','GUEST_SESSION','ORDER','BATCH','ORDER_ITEM','PAYMENT'));
        ALTER TABLE order_batches ADD CONSTRAINT ck_reviewed_time CHECK(reviewed_at IS NULL OR reviewed_at>=created_at);
        ALTER TABLE order_batches ADD CONSTRAINT ck_eligible_time CHECK(eligible_at IS NULL OR eligible_at>=created_at);
        CREATE FUNCTION guard_order() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF (NEW.code,NEW.order_type,NEW.visit_id,NEW.pickup_code,NEW.created_by_staff_id) IS DISTINCT FROM (OLD.code,OLD.order_type,OLD.visit_id,OLD.pickup_code,OLD.created_by_staff_id) THEN RAISE EXCEPTION 'order identity is immutable' USING ERRCODE='23514'; END IF;
          IF OLD.status IN ('PAID','CANCELLED') THEN RAISE EXCEPTION 'terminal order is immutable' USING ERRCODE='23514'; END IF;
          IF NEW.status<>OLD.status AND NOT ((OLD.status='SERVING' AND NEW.status IN ('AWAITING_PAYMENT','CANCELLED')) OR (OLD.status='AWAITING_PAYMENT' AND NEW.status IN ('SERVING','PAID'))) THEN RAISE EXCEPTION 'invalid order transition' USING ERRCODE='23514'; END IF;
          IF NEW.status='CANCELLED' AND EXISTS(SELECT 1 FROM audit_events a JOIN order_items i ON i.id=a.target_id JOIN order_batches b ON b.id=i.batch_id WHERE b.order_id=NEW.id AND a.target_type='ORDER_ITEM' AND a.action IN ('ITEM_PREPARE','ITEM_READY','ITEM_SERVE')) THEN RAISE EXCEPTION 'prepared order requires settlement' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER order_guard BEFORE UPDATE ON orders FOR EACH ROW EXECUTE FUNCTION guard_order();
        CREATE FUNCTION guard_batch() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE guest_visit uuid; order_visit uuid;
        BEGIN
          IF TG_OP='UPDATE' THEN
            IF OLD.status<>'PENDING' OR NEW.status NOT IN ('APPROVED','REJECTED','WITHDRAWN') THEN RAISE EXCEPTION 'batch decision is final' USING ERRCODE='23514'; END IF;
            IF (NEW.order_id,NEW.sequence_no,NEW.submitted_by_staff_id,NEW.submitted_by_guest_session_id) IS DISTINCT FROM (OLD.order_id,OLD.sequence_no,OLD.submitted_by_staff_id,OLD.submitted_by_guest_session_id) THEN RAISE EXCEPTION 'batch identity is immutable' USING ERRCODE='23514'; END IF;
          END IF;
          IF NEW.reviewed_by_staff_id=NEW.submitted_by_staff_id AND NOT EXISTS(SELECT 1 FROM user_roles WHERE user_id=NEW.reviewed_by_staff_id AND role_code IN ('MANAGER','ADMIN')) THEN RAISE EXCEPTION 'only manager or admin may self review' USING ERRCODE='23514'; END IF;
          IF NEW.submitted_by_guest_session_id IS NOT NULL THEN
            SELECT visit_id INTO guest_visit FROM guest_sessions WHERE id=NEW.submitted_by_guest_session_id;
            SELECT visit_id INTO order_visit FROM orders WHERE id=NEW.order_id;
            IF guest_visit IS DISTINCT FROM order_visit OR order_visit IS NULL THEN RAISE EXCEPTION 'guest belongs to another visit' USING ERRCODE='23514'; END IF;
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER batch_guard BEFORE INSERT OR UPDATE ON order_batches FOR EACH ROW EXECUTE FUNCTION guard_batch();
        CREATE FUNCTION guard_table() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF (NEW.code,NEW.capacity,NEW.is_active) IS DISTINCT FROM (OLD.code,OLD.capacity,OLD.is_active) AND EXISTS(SELECT 1 FROM visits WHERE table_id=NEW.id AND status='OPEN') THEN RAISE EXCEPTION 'occupied table cannot change' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER table_guard BEFORE UPDATE ON restaurant_tables FOR EACH ROW EXECUTE FUNCTION guard_table();
        CREATE FUNCTION guard_visit() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE table_capacity smallint; table_active boolean;
        BEGIN
          IF TG_OP='INSERT' THEN
            SELECT capacity,is_active INTO table_capacity,table_active FROM restaurant_tables WHERE id=NEW.table_id FOR UPDATE;
            IF NOT table_active OR NEW.party_size>table_capacity THEN RAISE EXCEPTION 'table unavailable or capacity exceeded' USING ERRCODE='23514'; END IF;
          ELSE
            IF NEW.table_id<>OLD.table_id OR OLD.status<>'OPEN' OR NEW.join_generation<OLD.join_generation THEN RAISE EXCEPTION 'invalid visit change' USING ERRCODE='23514'; END IF;
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER visit_guard BEFORE INSERT OR UPDATE ON visits FOR EACH ROW EXECUTE FUNCTION guard_visit();
        -- Index every referencing FK not already covered by a leading index column.
        CREATE INDEX ix_user_roles_granter ON user_roles(granted_by_staff_id);
        CREATE INDEX ix_visit_opened_by ON visits(opened_by_staff_id);
        CREATE INDEX ix_visit_ended_by ON visits(ended_by_staff_id);
        CREATE INDEX ix_order_creator ON orders(created_by_staff_id);
        CREATE INDEX ix_batch_submit_staff ON order_batches(submitted_by_staff_id);
        CREATE INDEX ix_batch_submit_guest ON order_batches(submitted_by_guest_session_id);
        CREATE INDEX ix_batch_reviewer ON order_batches(reviewed_by_staff_id);
        CREATE INDEX ix_item_menu ON order_items(menu_item_id);
        CREATE INDEX ix_payment_staff ON payments(received_by_staff_id);
        CREATE INDEX ix_audit_staff ON audit_events(actor_staff_id);
        CREATE INDEX ix_audit_guest ON audit_events(actor_guest_session_id);
        """);
    protected override void Down(MigrationBuilder m)=>throw new NotSupportedException("Use a forward migration or restore backup.");
}

CREATE TABLE staff_users (
 id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 username varchar(64) NOT NULL UNIQUE CHECK(username ~ '^[a-z0-9._-]{3,64}$'),
 display_name varchar(120) NOT NULL CHECK(btrim(display_name)<>''), password_hash text NOT NULL,
 is_active boolean NOT NULL, must_change_password boolean NOT NULL);
CREATE TABLE roles(code varchar(16) PRIMARY KEY CHECK(code IN ('WAITER','KITCHEN','CASHIER','MANAGER','ADMIN')), display_name varchar(80) NOT NULL);
CREATE TABLE user_roles(user_id uuid REFERENCES staff_users ON DELETE RESTRICT, role_code varchar(16) REFERENCES roles ON DELETE RESTRICT,
 granted_at timestamptz NOT NULL, granted_by_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT, PRIMARY KEY(user_id,role_code));
CREATE TABLE staff_sessions(id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES staff_users ON DELETE RESTRICT,
 token_hash bytea NOT NULL UNIQUE CHECK(octet_length(token_hash)=32), created_at timestamptz NOT NULL,
 expires_at timestamptz NOT NULL CHECK(expires_at>created_at), revoked_at timestamptz CHECK(revoked_at>=created_at));
CREATE TABLE menu_categories(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 code varchar(32) NOT NULL UNIQUE CHECK(code=upper(btrim(code)) AND code<>''), name varchar(100) NOT NULL CHECK(btrim(name)<>''),
 sort_order integer NOT NULL CHECK(sort_order>=0), is_active boolean NOT NULL);
CREATE TABLE menu_items(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 category_id uuid NOT NULL REFERENCES menu_categories ON DELETE RESTRICT, code varchar(32) NOT NULL UNIQUE CHECK(code=upper(btrim(code)) AND code<>''),
 name varchar(160) NOT NULL CHECK(btrim(name)<>''), description varchar(2000) NOT NULL CHECK(btrim(description)<>''),
 image_url varchar(2048) NOT NULL CHECK(btrim(image_url)<>''), price_vnd bigint NOT NULL CHECK(price_vnd BETWEEN 0 AND 1000000000000),
 is_active boolean NOT NULL, is_available boolean NOT NULL);
CREATE TABLE restaurant_tables(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 code varchar(20) NOT NULL UNIQUE CHECK(code=upper(btrim(code)) AND code<>''), capacity smallint NOT NULL CHECK(capacity BETWEEN 1 AND 30),
 is_active boolean NOT NULL, qr_token_hash bytea NOT NULL UNIQUE CHECK(octet_length(qr_token_hash)=32));
CREATE TABLE visits(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 table_id uuid NOT NULL REFERENCES restaurant_tables ON DELETE RESTRICT, table_code_snapshot varchar(20) NOT NULL,
 status varchar(12) NOT NULL CHECK(status IN ('OPEN','CLOSED','CANCELLED')), party_size smallint NOT NULL CHECK(party_size BETWEEN 1 AND 30),
 opened_by_staff_id uuid NOT NULL REFERENCES staff_users ON DELETE RESTRICT, join_code_hmac bytea NOT NULL CHECK(octet_length(join_code_hmac)=32),
 join_generation integer NOT NULL CHECK(join_generation>0), ended_at timestamptz, ended_by_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT,
 CHECK((status='OPEN' AND ended_at IS NULL AND ended_by_staff_id IS NULL) OR (status<>'OPEN' AND ended_at>=created_at AND ended_at IS NOT NULL AND ended_by_staff_id IS NOT NULL)));
CREATE UNIQUE INDEX ux_visits_open ON visits(table_id) WHERE status='OPEN';
CREATE TABLE guest_sessions(id uuid PRIMARY KEY, visit_id uuid NOT NULL REFERENCES visits ON DELETE RESTRICT,
 token_hash bytea NOT NULL UNIQUE CHECK(octet_length(token_hash)=32), join_generation integer NOT NULL CHECK(join_generation>0),
 created_at timestamptz NOT NULL, expires_at timestamptz NOT NULL CHECK(expires_at>created_at), revoked_at timestamptz CHECK(revoked_at>=created_at));
CREATE TABLE orders(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 code varchar(32) NOT NULL UNIQUE, order_type varchar(12) NOT NULL CHECK(order_type IN ('DINE_IN','TAKEAWAY')),
 visit_id uuid UNIQUE REFERENCES visits ON DELETE RESTRICT, pickup_code varchar(16) UNIQUE, pickup_name varchar(120),
 status varchar(20) NOT NULL CHECK(status IN ('SERVING','AWAITING_PAYMENT','PAID','CANCELLED')), total_vnd bigint,
 created_by_staff_id uuid NOT NULL REFERENCES staff_users ON DELETE RESTRICT, status_changed_at timestamptz NOT NULL,
 CHECK((order_type='DINE_IN' AND visit_id IS NOT NULL AND pickup_code IS NULL AND pickup_name IS NULL) OR (order_type='TAKEAWAY' AND visit_id IS NULL AND pickup_code IS NOT NULL)),
 CHECK((status IN ('SERVING','CANCELLED') AND total_vnd IS NULL) OR (status IN ('AWAITING_PAYMENT','PAID') AND total_vnd IS NOT NULL AND total_vnd BETWEEN 0 AND 1000000000000)));
CREATE TABLE order_batches(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 order_id uuid NOT NULL REFERENCES orders ON DELETE RESTRICT, sequence_no integer NOT NULL CHECK(sequence_no>0),
 status varchar(12) NOT NULL CHECK(status IN ('PENDING','APPROVED','REJECTED','WITHDRAWN')),
 submitted_by_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT, submitted_by_guest_session_id uuid REFERENCES guest_sessions ON DELETE RESTRICT,
 reviewed_by_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT, reviewed_at timestamptz, decision_reason varchar(500), eligible_at timestamptz,
 UNIQUE(order_id,sequence_no), CHECK(num_nonnulls(submitted_by_staff_id,submitted_by_guest_session_id)=1),
 CHECK((status='APPROVED' AND eligible_at IS NOT NULL) OR (status<>'APPROVED' AND eligible_at IS NULL)),
 CHECK((sequence_no=1 AND status='APPROVED' AND reviewed_by_staff_id IS NULL AND reviewed_at IS NULL AND decision_reason IS NULL)
 OR (sequence_no>1 AND status IN ('PENDING','WITHDRAWN') AND reviewed_by_staff_id IS NULL AND reviewed_at IS NULL AND decision_reason IS NULL)
 OR (sequence_no>1 AND status='APPROVED' AND reviewed_by_staff_id IS NOT NULL AND reviewed_at IS NOT NULL AND decision_reason IS NULL)
 OR (sequence_no>1 AND status='REJECTED' AND reviewed_by_staff_id IS NOT NULL AND reviewed_at IS NOT NULL AND decision_reason IS NOT NULL AND btrim(decision_reason)<>'')));
CREATE TABLE order_items(id uuid PRIMARY KEY, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL CHECK(version>0),
 batch_id uuid NOT NULL REFERENCES order_batches ON DELETE RESTRICT, menu_item_id uuid NOT NULL REFERENCES menu_items ON DELETE RESTRICT,
 menu_code_snapshot varchar(32) NOT NULL, menu_name_snapshot varchar(160) NOT NULL CHECK(btrim(menu_name_snapshot)<>''),
 quantity smallint NOT NULL CHECK(quantity BETWEEN 1 AND 99), unit_price_vnd bigint NOT NULL CHECK(unit_price_vnd BETWEEN 0 AND 1000000000000), note varchar(500),
 status varchar(20) NOT NULL CHECK(status IN ('PENDING_APPROVAL','PLACED','ACCEPTED','PREPARING','READY','SERVED','CANCELLED','REJECTED')), status_changed_at timestamptz NOT NULL);
CREATE TABLE payments(id uuid PRIMARY KEY, order_id uuid NOT NULL UNIQUE REFERENCES orders ON DELETE RESTRICT,
 receipt_no varchar(32) NOT NULL UNIQUE, method varchar(16) NOT NULL CHECK(method IN ('CASH','BANK_TRANSFER','ZERO')),
 amount_vnd bigint NOT NULL CHECK(amount_vnd BETWEEN 0 AND 1000000000000), tendered_vnd bigint, transfer_reference varchar(120),
 paid_at timestamptz NOT NULL, received_by_staff_id uuid NOT NULL REFERENCES staff_users ON DELETE RESTRICT,
 CHECK((method='ZERO' AND amount_vnd=0 AND tendered_vnd IS NULL AND transfer_reference IS NULL)
 OR (method='CASH' AND amount_vnd>0 AND tendered_vnd IS NOT NULL AND tendered_vnd BETWEEN amount_vnd AND 1000000000000 AND transfer_reference IS NULL)
 OR (method='BANK_TRANSFER' AND amount_vnd>0 AND tendered_vnd IS NULL AND transfer_reference IS NOT NULL AND btrim(transfer_reference)<>'')));
CREATE TABLE audit_events(id uuid PRIMARY KEY, occurred_at timestamptz NOT NULL, actor_kind varchar(8) NOT NULL CHECK(actor_kind IN ('STAFF','GUEST','SYSTEM')),
 actor_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT, actor_guest_session_id uuid REFERENCES guest_sessions ON DELETE RESTRICT,
 action varchar(64) NOT NULL, target_type varchar(32) NOT NULL, target_id uuid NOT NULL, before_data jsonb, after_data jsonb, reason varchar(500), request_id uuid NOT NULL, correlation_id uuid NOT NULL,
 CHECK((actor_kind='STAFF' AND actor_staff_id IS NOT NULL AND actor_guest_session_id IS NULL) OR (actor_kind='GUEST' AND actor_staff_id IS NULL AND actor_guest_session_id IS NOT NULL) OR (actor_kind='SYSTEM' AND actor_staff_id IS NULL AND actor_guest_session_id IS NULL)),
 CHECK(before_data IS NOT NULL OR after_data IS NOT NULL));
CREATE TABLE idempotency_records(id uuid PRIMARY KEY, actor_staff_id uuid REFERENCES staff_users ON DELETE RESTRICT, actor_guest_session_id uuid REFERENCES guest_sessions ON DELETE RESTRICT,
 key uuid NOT NULL, method varchar(8) NOT NULL, route varchar(200) NOT NULL, request_hash bytea NOT NULL CHECK(octet_length(request_hash)=32),
 response_status smallint NOT NULL CHECK(response_status BETWEEN 200 AND 299), response_ciphertext bytea, encryption_key_id varchar(64),
 created_at timestamptz NOT NULL, replay_until timestamptz NOT NULL CHECK(replay_until>created_at), CHECK(num_nonnulls(actor_staff_id,actor_guest_session_id)=1));
CREATE UNIQUE INDEX ux_idempotency_staff ON idempotency_records(actor_staff_id,key) WHERE actor_staff_id IS NOT NULL;
CREATE UNIQUE INDEX ux_idempotency_guest ON idempotency_records(actor_guest_session_id,key) WHERE actor_guest_session_id IS NOT NULL;
CREATE INDEX ix_roles_user ON user_roles(role_code,user_id);
CREATE INDEX ix_sessions_user ON staff_sessions(user_id);
CREATE INDEX ix_guest_visit ON guest_sessions(visit_id);
CREATE INDEX ix_items_batch ON order_items(batch_id);
CREATE INDEX ix_items_queue ON order_items(status,status_changed_at,id);
CREATE INDEX ix_batches_pending ON order_batches(status,created_at);
CREATE INDEX ix_menu_category ON menu_items(category_id);
CREATE INDEX ix_orders_status ON orders(status,created_at);
CREATE INDEX ix_audit_target ON audit_events(target_type,target_id,occurred_at);
CREATE INDEX ix_audit_action ON audit_events(action,occurred_at);
CREATE INDEX ix_payments_time ON payments(paid_at);

CREATE FUNCTION check_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.version<>OLD.version+1 THEN RAISE EXCEPTION 'version must increase by one' USING ERRCODE='23514'; END IF;
 IF NEW.updated_at<NEW.created_at THEN RAISE EXCEPTION 'invalid timestamps' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['staff_users','menu_categories','menu_items','restaurant_tables','visits','orders','order_batches','order_items'] LOOP
 EXECUTE format('CREATE TRIGGER version_guard BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION check_version()',t);
 END LOOP;
END $$;
CREATE FUNCTION reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'append only record' USING ERRCODE='23514'; END $$;
CREATE TRIGGER immutable_payments BEFORE UPDATE OR DELETE ON payments FOR EACH ROW EXECUTE FUNCTION reject_mutation();
CREATE TRIGGER immutable_audit BEFORE UPDATE OR DELETE ON audit_events FOR EACH ROW EXECUTE FUNCTION reject_mutation();
CREATE FUNCTION check_item_transition() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE order_status text;
BEGIN
 SELECT o.status INTO order_status FROM orders o JOIN order_batches b ON b.order_id=o.id WHERE b.id=NEW.batch_id;
 IF order_status<>'SERVING' THEN RAISE EXCEPTION 'order is closed for changes' USING ERRCODE='23514'; END IF;
 IF (NEW.batch_id,NEW.menu_item_id,NEW.menu_code_snapshot,NEW.menu_name_snapshot,NEW.unit_price_vnd) IS DISTINCT FROM
 (OLD.batch_id,OLD.menu_item_id,OLD.menu_code_snapshot,OLD.menu_name_snapshot,OLD.unit_price_vnd) THEN
 RAISE EXCEPTION 'item snapshots are immutable' USING ERRCODE='23514'; END IF;
 IF NEW.status=OLD.status THEN
   IF NEW.quantity<>OLD.quantity OR NEW.note IS DISTINCT FROM OLD.note THEN
     IF OLD.status NOT IN ('PLACED','ACCEPTED') OR NEW.quantity>OLD.quantity THEN RAISE EXCEPTION 'item cannot be amended' USING ERRCODE='23514'; END IF;
   END IF;
 ELSE
 IF NOT ((OLD.status='PENDING_APPROVAL' AND NEW.status IN ('PLACED','CANCELLED','REJECTED')) OR
 (OLD.status='PLACED' AND NEW.status IN ('ACCEPTED','CANCELLED')) OR (OLD.status='ACCEPTED' AND NEW.status IN ('PREPARING','CANCELLED')) OR
 (OLD.status='PREPARING' AND NEW.status IN ('READY','CANCELLED')) OR (OLD.status='READY' AND NEW.status IN ('SERVED','CANCELLED'))) THEN
 RAISE EXCEPTION 'invalid item transition' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER item_transition BEFORE UPDATE ON order_items FOR EACH ROW EXECUTE FUNCTION check_item_transition();
CREATE FUNCTION check_business_integrity() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF EXISTS(SELECT 1 FROM visits v LEFT JOIN orders o ON o.visit_id=v.id WHERE o.id IS NULL OR
 (v.status='OPEN' AND o.status NOT IN ('SERVING','AWAITING_PAYMENT')) OR (v.status='CLOSED' AND o.status<>'PAID') OR (v.status='CANCELLED' AND o.status<>'CANCELLED')) THEN
 RAISE EXCEPTION 'visit and order must agree' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM order_batches b WHERE (SELECT count(*) FROM order_items i WHERE i.batch_id=b.id) NOT BETWEEN 1 AND 50) THEN
 RAISE EXCEPTION 'batch must have 1 to 50 items' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM order_items i JOIN order_batches b ON b.id=i.batch_id WHERE
 (b.status='PENDING' AND i.status<>'PENDING_APPROVAL') OR (b.status='REJECTED' AND i.status<>'REJECTED') OR
 (b.status='WITHDRAWN' AND i.status<>'CANCELLED') OR (b.status='APPROVED' AND i.status IN ('PENDING_APPROVAL','REJECTED'))) THEN
 RAISE EXCEPTION 'batch and items must agree' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM orders o WHERE o.status IN ('AWAITING_PAYMENT','PAID') AND (
 NOT EXISTS(SELECT 1 FROM order_batches b WHERE b.order_id=o.id) OR
 EXISTS(SELECT 1 FROM order_batches b WHERE b.order_id=o.id AND b.status='PENDING') OR
 EXISTS(SELECT 1 FROM order_items i JOIN order_batches b ON b.id=i.batch_id WHERE b.order_id=o.id AND i.status NOT IN ('SERVED','CANCELLED','REJECTED')) OR
 o.total_vnd<>(SELECT coalesce(sum(i.quantity::numeric*i.unit_price_vnd),0) FROM order_items i JOIN order_batches b ON b.id=i.batch_id WHERE b.order_id=o.id AND i.status='SERVED'))) THEN
 RAISE EXCEPTION 'checkout total or item state mismatch' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM orders o LEFT JOIN payments p ON p.order_id=o.id WHERE (o.status='PAID' AND (p.id IS NULL OR p.amount_vnd<>o.total_vnd)) OR (o.status<>'PAID' AND p.id IS NOT NULL)) THEN
 RAISE EXCEPTION 'payment and order must agree' USING ERRCODE='23514'; END IF;
 RETURN NULL;
END $$;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['visits','orders','order_batches','order_items','payments'] LOOP
 EXECUTE format('CREATE CONSTRAINT TRIGGER business_integrity AFTER INSERT OR UPDATE OR DELETE ON %I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_business_integrity()',t);
 END LOOP;
END $$;

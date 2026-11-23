START TRANSACTION;
ALTER TABLE receipts ADD status character varying(30) NOT NULL DEFAULT 'Draft';

CREATE TABLE receipt_status_changes (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    receipt_id uuid NOT NULL,
    from_status character varying(30) NOT NULL,
    to_status character varying(30) NOT NULL,
    changed_by_user_id integer NOT NULL,
    changed_at timestamp with time zone NOT NULL DEFAULT (now()),
    reason character varying(1000),
    CONSTRAINT pk_receipt_status_changes PRIMARY KEY (id),
    CONSTRAINT ck_receipt_status_changes_from_status_valid CHECK (from_status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled')),
    CONSTRAINT ck_receipt_status_changes_reason_not_empty CHECK (length(reason) > 0),
    CONSTRAINT ck_receipt_status_changes_reason_required CHECK (to_status NOT IN ('Draft', 'Cancelled') OR reason IS NOT NULL),
    CONSTRAINT ck_receipt_status_changes_status_differs CHECK (from_status <> to_status),
    CONSTRAINT ck_receipt_status_changes_to_status_valid CHECK (to_status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled')),
    CONSTRAINT fk_receipt_status_changes_receipts_receipt_id FOREIGN KEY (receipt_id) REFERENCES receipts (id) ON DELETE RESTRICT,
    CONSTRAINT fk_receipt_status_changes_users_changed_by_user_id FOREIGN KEY (changed_by_user_id) REFERENCES users (id) ON DELETE RESTRICT
);

ALTER TABLE receipts ADD CONSTRAINT ck_receipts_status_valid CHECK (status IN ('Draft', 'PendingSupplyApproval', 'PendingFinanceApproval', 'Approved', 'Issued', 'Cancelled'));

CREATE INDEX ix_receipt_status_changes_changed_by_user_id ON receipt_status_changes (changed_by_user_id);

CREATE INDEX ix_receipt_status_changes_receipt_id_changed_at ON receipt_status_changes (receipt_id, changed_at);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261123052442_AddReceiptStatus', '10.0.13');

COMMIT;


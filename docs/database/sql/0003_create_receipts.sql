START TRANSACTION;
CREATE TABLE receipts (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    donor_id uuid NOT NULL,
    total_amount numeric(12,2) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_receipts PRIMARY KEY (id),
    CONSTRAINT ck_receipts_total_amount_not_negative CHECK (total_amount >= 0),
    CONSTRAINT fk_receipts_donors_donor_id FOREIGN KEY (donor_id) REFERENCES donors (id) ON DELETE RESTRICT
);

CREATE TABLE receipt_items (
    receipt_id uuid NOT NULL,
    food_item_id uuid NOT NULL,
    unit_price numeric(12,2) NOT NULL,
    quality_percent numeric(5,2) NOT NULL,
    CONSTRAINT pk_receipt_items PRIMARY KEY (receipt_id, food_item_id),
    CONSTRAINT ck_receipt_items_quality_percent_range CHECK (quality_percent BETWEEN 0 AND 100),
    CONSTRAINT ck_receipt_items_unit_price_positive CHECK (unit_price > 0),
    CONSTRAINT fk_receipt_items_food_items_food_item_id FOREIGN KEY (food_item_id) REFERENCES food_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_receipt_items_receipts_receipt_id FOREIGN KEY (receipt_id) REFERENCES receipts (id) ON DELETE CASCADE
);

CREATE INDEX ix_receipt_items_food_item_id ON receipt_items (food_item_id);

CREATE INDEX ix_receipts_donor_id ON receipts (donor_id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261106021712_CreateReceipts', '10.0.13');

COMMIT;


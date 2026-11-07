START TRANSACTION;
CREATE TABLE donors (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    name character varying(255) NOT NULL,
    address text,
    postal_code character varying(20),
    contact_person character varying(255),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_donors PRIMARY KEY (id),
    CONSTRAINT ck_donors_name_not_empty CHECK (length(name) > 0)
);

CREATE TABLE food_items (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    donor_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    quantity numeric(12,2) NOT NULL,
    unit character varying(20) NOT NULL,
    donation_date date NOT NULL,
    quality character varying(1) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_food_items PRIMARY KEY (id),
    CONSTRAINT ck_food_items_name_not_empty CHECK (length(name) > 0),
    CONSTRAINT ck_food_items_quality CHECK (quality IN ('A', 'B', 'C')),
    CONSTRAINT ck_food_items_quantity_positive CHECK (quantity > 0),
    CONSTRAINT ck_food_items_unit_not_empty CHECK (length(unit) > 0),
    CONSTRAINT fk_food_items_donors_donor_id FOREIGN KEY (donor_id) REFERENCES donors (id) ON DELETE RESTRICT
);

CREATE INDEX ix_food_items_donor_id ON food_items (donor_id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261105163138_CreateDonorsAndFoodItems', '10.0.13');

COMMIT;


START TRANSACTION;
ALTER TABLE catalog_items ADD is_active boolean NOT NULL DEFAULT TRUE;

CREATE TABLE bc_item_mapping_changes (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    business_central_code character varying(50) NOT NULL,
    action character varying(20) NOT NULL,
    old_catalog_item_id uuid,
    new_catalog_item_id uuid,
    changed_by_user_id integer NOT NULL,
    changed_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_bc_item_mapping_changes PRIMARY KEY (id),
    CONSTRAINT fk_bc_item_mapping_changes_catalog_items_new_catalog_item_id FOREIGN KEY (new_catalog_item_id) REFERENCES catalog_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bc_item_mapping_changes_catalog_items_old_catalog_item_id FOREIGN KEY (old_catalog_item_id) REFERENCES catalog_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bc_item_mapping_changes_users_changed_by_user_id FOREIGN KEY (changed_by_user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE bc_item_mappings (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    business_central_code character varying(50) NOT NULL,
    catalog_item_id uuid NOT NULL,
    confirmed_by_user_id integer NOT NULL,
    confirmed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_bc_item_mappings PRIMARY KEY (id),
    CONSTRAINT ck_bc_item_mappings_business_central_code_not_empty CHECK (length(business_central_code) > 0),
    CONSTRAINT fk_bc_item_mappings_catalog_items_catalog_item_id FOREIGN KEY (catalog_item_id) REFERENCES catalog_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bc_item_mappings_users_confirmed_by_user_id FOREIGN KEY (confirmed_by_user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_bc_item_mapping_changes_business_central_code ON bc_item_mapping_changes (business_central_code);

CREATE INDEX ix_bc_item_mapping_changes_changed_by_user_id ON bc_item_mapping_changes (changed_by_user_id);

CREATE INDEX ix_bc_item_mapping_changes_new_catalog_item_id ON bc_item_mapping_changes (new_catalog_item_id);

CREATE INDEX ix_bc_item_mapping_changes_old_catalog_item_id ON bc_item_mapping_changes (old_catalog_item_id);

CREATE UNIQUE INDEX ix_bc_item_mappings_business_central_code ON bc_item_mappings (business_central_code);

CREATE INDEX ix_bc_item_mappings_catalog_item_id ON bc_item_mappings (catalog_item_id);

CREATE INDEX ix_bc_item_mappings_confirmed_by_user_id ON bc_item_mappings (confirmed_by_user_id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261124214039_AddBcItemMappings', '10.0.13');

COMMIT;


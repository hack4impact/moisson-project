START TRANSACTION;
CREATE TABLE catalog_items (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    name character varying(255) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_catalog_items PRIMARY KEY (id),
    CONSTRAINT ck_catalog_items_name_not_empty CHECK (length(name) > 0)
);

CREATE TABLE prices (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    catalog_item_id uuid NOT NULL,
    unit_price numeric(12,4) NOT NULL,
    effective_date date NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_prices PRIMARY KEY (id),
    CONSTRAINT ck_prices_unit_price_positive CHECK (unit_price > 0),
    CONSTRAINT fk_prices_catalog_items_catalog_item_id FOREIGN KEY (catalog_item_id) REFERENCES catalog_items (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ix_prices_catalog_item_id_effective_date ON prices (catalog_item_id, effective_date);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261110000447_CreateCatalogItemsAndPrices', '10.0.13');

COMMIT;


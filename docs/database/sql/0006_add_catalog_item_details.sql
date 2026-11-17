START TRANSACTION;
ALTER TABLE catalog_items ADD category character varying(100) NOT NULL DEFAULT '';

ALTER TABLE catalog_items ADD business_central_code character varying(50) NOT NULL DEFAULT '';

ALTER TABLE catalog_items ADD unit character varying(20) NOT NULL DEFAULT '';

ALTER TABLE catalog_items ADD quality_class character varying(1) NOT NULL DEFAULT '';

ALTER TABLE catalog_items ADD updated_at timestamp with time zone;

ALTER TABLE catalog_items ALTER COLUMN category DROP DEFAULT;

ALTER TABLE catalog_items ALTER COLUMN business_central_code DROP DEFAULT;

ALTER TABLE catalog_items ALTER COLUMN unit DROP DEFAULT;

ALTER TABLE catalog_items ALTER COLUMN quality_class DROP DEFAULT;

CREATE UNIQUE INDEX ix_catalog_items_business_central_code ON catalog_items (business_central_code);

CREATE INDEX ix_catalog_items_quality_class ON catalog_items (quality_class);

ALTER TABLE catalog_items ADD CONSTRAINT ck_catalog_items_category_not_empty CHECK (length(category) > 0);

ALTER TABLE catalog_items ADD CONSTRAINT ck_catalog_items_business_central_code_not_empty CHECK (length(business_central_code) > 0);

ALTER TABLE catalog_items ADD CONSTRAINT ck_catalog_items_unit_not_empty CHECK (length(unit) > 0);

ALTER TABLE catalog_items ADD CONSTRAINT fk_catalog_items_quality_classes_quality_class FOREIGN KEY (quality_class) REFERENCES quality_classes (code) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261117040905_AddCatalogItemDetails', '10.0.13');

COMMIT;

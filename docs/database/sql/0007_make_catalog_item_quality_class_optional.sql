START TRANSACTION;
ALTER TABLE catalog_items ALTER COLUMN quality_class DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261120031640_MakeCatalogItemQualityClassOptional', '10.0.13');

COMMIT;


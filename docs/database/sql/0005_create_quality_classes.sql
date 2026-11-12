START TRANSACTION;
ALTER TABLE food_items DROP CONSTRAINT ck_food_items_quality;

CREATE TABLE quality_classes (
    code character varying(1) NOT NULL,
    name character varying(100) NOT NULL,
    percentage numeric(5,2) NOT NULL,
    min_percentage numeric(5,2) NOT NULL,
    max_percentage numeric(5,2) NOT NULL,
    CONSTRAINT pk_quality_classes PRIMARY KEY (code),
    CONSTRAINT ck_quality_classes_code_not_empty CHECK (length(code) > 0),
    CONSTRAINT ck_quality_classes_name_not_empty CHECK (length(name) > 0),
    CONSTRAINT ck_quality_classes_percentage_in_range CHECK (percentage BETWEEN min_percentage AND max_percentage),
    CONSTRAINT ck_quality_classes_range_valid CHECK (min_percentage >= 0 AND min_percentage <= max_percentage AND max_percentage <= 100)
);

INSERT INTO quality_classes (code, max_percentage, min_percentage, name, percentage)
VALUES ('A', 100.0, 100.0, 'Grocery quality', 100.0);
INSERT INTO quality_classes (code, max_percentage, min_percentage, name, percentage)
VALUES ('B', 75.0, 50.0, 'Imperfect', 50.0);
INSERT INTO quality_classes (code, max_percentage, min_percentage, name, percentage)
VALUES ('C', 0.0, 0.0, 'Near expiry', 0.0);

CREATE INDEX ix_food_items_quality ON food_items (quality);

CREATE UNIQUE INDEX ix_quality_classes_name ON quality_classes (name);

ALTER TABLE food_items ADD CONSTRAINT fk_food_items_quality_classes_quality FOREIGN KEY (quality) REFERENCES quality_classes (code) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261112002641_CreateQualityClasses', '10.0.13');

COMMIT;


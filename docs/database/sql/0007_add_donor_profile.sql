START TRANSACTION;
ALTER TABLE donors ADD business_central_code character varying(50);

ALTER TABLE donors ADD city character varying(100);

ALTER TABLE donors ADD email character varying(254);

ALTER TABLE donors ADD notes text;

ALTER TABLE donors ADD phone character varying(30);

ALTER TABLE donors ADD province character varying(50);

ALTER TABLE donors ADD type character varying(20) NOT NULL DEFAULT 'organization';

ALTER TABLE donors ALTER COLUMN type DROP DEFAULT;

ALTER TABLE donors ADD updated_at timestamp with time zone;

CREATE UNIQUE INDEX ix_donors_business_central_code ON donors (business_central_code);

ALTER TABLE donors ADD CONSTRAINT ck_donors_business_central_code_not_empty CHECK (length(business_central_code) > 0);

ALTER TABLE donors ADD CONSTRAINT ck_donors_type CHECK (type IN ('individual', 'organization'));

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261118185010_AddDonorProfile', '10.0.13');

COMMIT;


START TRANSACTION;
ALTER TABLE receipts ADD receipt_number character varying(10);

CREATE TABLE receipt_number_counters (
    series character varying(10) NOT NULL,
    last_number integer NOT NULL,
    CONSTRAINT pk_receipt_number_counters PRIMARY KEY (series),
    CONSTRAINT ck_receipt_number_counters_last_number_in_range CHECK (last_number BETWEEN 0 AND 999999)
);

INSERT INTO receipt_number_counters (series, last_number)
VALUES ('DEN', 0);

UPDATE receipts AS r
SET receipt_number = 'DEN_' || lpad(numbered.position::text, 6, '0')
FROM (
    SELECT id, row_number() OVER (ORDER BY created_at, id) AS position
    FROM receipts
    WHERE status = 'Issued'
) AS numbered
WHERE r.id = numbered.id;

UPDATE receipt_number_counters
SET last_number = (SELECT count(*) FROM receipts WHERE receipt_number IS NOT NULL)
WHERE series = 'DEN';

CREATE UNIQUE INDEX ix_receipts_receipt_number ON receipts (receipt_number);

ALTER TABLE receipts ADD CONSTRAINT ck_receipts_issued_has_receipt_number CHECK (status <> 'Issued' OR receipt_number IS NOT NULL);

ALTER TABLE receipts ADD CONSTRAINT ck_receipts_receipt_number_format CHECK (receipt_number ~ '^DEN_[0-9]{6}$');

ALTER TABLE receipts ADD CONSTRAINT ck_receipts_receipt_number_only_once_issued CHECK (status IN ('Issued', 'Cancelled') OR receipt_number IS NULL);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261123233317_AddReceiptNumbers', '10.0.13');

COMMIT;


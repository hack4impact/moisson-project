START TRANSACTION;
ALTER TABLE users ADD created_by_id integer;

CREATE INDEX ix_users_created_by_id ON users (created_by_id);

ALTER TABLE users ADD CONSTRAINT fk_users_users_created_by_id FOREIGN KEY (created_by_id) REFERENCES users (id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261125020527_AddUserCreatedBy', '10.0.13');

COMMIT;


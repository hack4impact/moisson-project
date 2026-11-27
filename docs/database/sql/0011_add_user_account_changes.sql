START TRANSACTION;
CREATE TABLE user_account_changes (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id integer NOT NULL,
    action character varying(20) NOT NULL,
    old_email character varying(256),
    new_email character varying(256),
    old_display_name character varying(200),
    new_display_name character varying(200),
    old_role_id integer,
    new_role_id integer,
    old_is_active boolean,
    new_is_active boolean,
    changed_by_user_id integer NOT NULL,
    changed_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_user_account_changes PRIMARY KEY (id),
    CONSTRAINT fk_user_account_changes_roles_new_role_id FOREIGN KEY (new_role_id) REFERENCES roles (id) ON DELETE RESTRICT,
    CONSTRAINT fk_user_account_changes_roles_old_role_id FOREIGN KEY (old_role_id) REFERENCES roles (id) ON DELETE RESTRICT,
    CONSTRAINT fk_user_account_changes_users_changed_by_user_id FOREIGN KEY (changed_by_user_id) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_user_account_changes_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_user_account_changes_changed_by_user_id ON user_account_changes (changed_by_user_id);

CREATE INDEX ix_user_account_changes_new_role_id ON user_account_changes (new_role_id);

CREATE INDEX ix_user_account_changes_old_role_id ON user_account_changes (old_role_id);

CREATE INDEX ix_user_account_changes_user_id ON user_account_changes (user_id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261127164801_AddUserAccountChanges', '10.0.13');

COMMIT;


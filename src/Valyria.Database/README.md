# Valyria Database Migrations

This project contains Flyway database migration scripts for the Valyria application's MySQL database.

## Overview

Migrations run from the GitHub Actions deploy job ([.github/workflows/ci-cd.yml](../../.github/workflows/ci-cd.yml)): once the tests pass on `main`, Flyway migrates production and then `aspire deploy` ships the apps. They are deliberately **not** an AppHost resource, because local `aspire run` points at the production database and would migrate it from whatever branch is checked out.

Shared Flyway settings live in [flyway.toml](flyway.toml). They disable `flyway clean` and refuse to auto-baseline.

Production was migrated by hand through V11 and then baselined at version 11, so Flyway only runs V12 and later there. On a pull request, CI also runs every migration through Flyway on an empty MySQL database.

## Project Structure

```
Valyria.Database/
├── migrations/          # SQL migration scripts
│   ├── V1__placeholder.sql
│   └── V2__example.sql
└── README.md
```

## Migration Naming Convention

Flyway requires strict naming conventions for migration files:

### Versioned Migrations
Format: `V{version}__{description}.sql`

Examples:
- `V1__initial_schema.sql`
- `V2__add_transactions_table.sql`
- `V2.1__add_user_email_index.sql`
- `V3__add_audit_triggers.sql`

Rules:
- Start with `V` (uppercase)
- Version number (integer or decimal: `1`, `2`, `2.1`, `2.2`)
- Double underscore `__`
- Description with underscores (not hyphens): `create_users_table`
- `.sql` extension

### Repeatable Migrations
Format: `R__{description}.sql`

Examples:
- `R__create_views.sql`
- `R__update_stored_procedures.sql`

These run whenever their content changes, useful for views, stored procedures, and functions.

## Configuration

### CI (production)

The deploy job reads the connection from GitHub `production` environment secrets, which Flyway picks up as environment variables:

| Secret | Example |
|---|---|
| `FLYWAY_URL` | `jdbc:mysql://cn-valyria-prod.mysql.database.azure.com:3306/vep_db?useSsl=true` |
| `FLYWAY_USER` | the admin login you'd use in MySQL Workbench (it needs DDL rights and must be able to create objects with `DEFINER=base_admin`) |
| `FLYWAY_PASSWORD` | |

`vep_db` must be the database in the URL: Flyway keeps its `flyway_schema_history` table there, and some migrations have no `USE` statement.

The Flyway image connects through the **MariaDB** JDBC driver, not MySQL Connector/J, so Connector/J options such as `sslMode=REQUIRED` are silently ignored. Without TLS, Azure rejects the connection with an "insecure transport" error. `useSsl=true` turns TLS on and verifies the server certificate (including hostname) against Java's built-in trust store, which already has Azure's DigiCert Global Root G2. To pin that root explicitly instead, mount the PEM into the container and add `&serverSslCert=/flyway/certs/DigiCertGlobalRootG2.crt.pem`.

### Running Flyway by hand

Run Flyway from Docker with this directory as the working directory so it picks up `flyway.toml`:

```bash
# From repository root
docker run --rm \
  -v "$PWD/src/Valyria.Database:/flyway/project:ro" \
  -e FLYWAY_URL="jdbc:mysql://cn-valyria-prod.mysql.database.azure.com:3306/vep_db?useSsl=true" \
  -e FLYWAY_USER=<admin user> -e FLYWAY_PASSWORD=<password> \
  flyway/flyway:11.20.3 -workingDirectory=/flyway/project info
```

Swap `info` for `migrate`, `validate` or `baseline` as needed.

### One-time production baseline

Production predates Flyway. Until it is baselined, `migrate` refuses to run ("Found non-empty schema(s) `vep_db` but no schema history table"), so nothing can be replayed by accident. To baseline it, take a backup and then mark V11 as the starting point:

```bash
az mysql flexible-server backup create --resource-group <rg> --name cn-valyria-prod --backup-name pre-flyway-baseline

# the docker command above, ending with:
  ... -workingDirectory=/flyway/project baseline -baselineVersion=11 "-baselineDescription=Applied manually before Flyway"
```

Afterwards `info` lists V1 through V11 as "Below Baseline" / "Ignored (Baseline)". The next migration, V12, is the first one Flyway applies.

## Migration Best Practices

### 1. Idempotency
Migrations should be safe to run multiple times:

```sql
-- Good
CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE
);

-- Good
ALTER TABLE users
ADD COLUMN IF NOT EXISTS created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- Bad
CREATE TABLE users (...);  -- Fails if table exists
```

### 2. Backwards Compatibility
Design migrations to support rolling deployments:

```sql
-- Phase 1: Add new nullable column
ALTER TABLE users ADD COLUMN new_email VARCHAR(255) NULL;

-- Phase 2: Backfill data (in repeatable migration or separate script)
UPDATE users SET new_email = email WHERE new_email IS NULL;

-- Phase 3: Make NOT NULL (in next version)
ALTER TABLE users MODIFY COLUMN new_email VARCHAR(255) NOT NULL;

-- Phase 4: Drop old column (in future version)
ALTER TABLE users DROP COLUMN email;
```

### 3. Performance Considerations

**Indexes:**
```sql
-- Add indexes separately from table creation for large tables
CREATE INDEX idx_users_email ON users(email);

-- For large tables, use ALGORITHM=INPLACE to avoid table locks
ALTER TABLE large_table
ADD COLUMN status VARCHAR(50),
ALGORITHM=INPLACE, LOCK=NONE;
```

**Batch Updates:**
```sql
-- For large data migrations, use batching
-- Consider running these outside of automatic migrations for very large tables
UPDATE users SET status = 'active' WHERE status IS NULL LIMIT 10000;
```

### 4. MySQL-Specific Syntax

```sql
-- Use utf8mb4 for full Unicode support (including emoji)
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    bio TEXT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- JSON columns (MySQL 5.7+)
ALTER TABLE users ADD COLUMN preferences JSON;

-- Full-text search
CREATE FULLTEXT INDEX idx_bio_fulltext ON users(bio);
```

### 5. Testing Migrations

Before committing:
1. Test on a copy of production data
2. Verify migration runs successfully
3. Check rollback scenarios
4. Measure performance impact

## Common Operations

### Creating a New Migration

1. Create a new file in `migrations/`:
```bash
# From repository root
touch src/Valyria.Database/migrations/V3__add_audit_log.sql
```

2. Write your SQL:
```sql
-- V3__add_audit_log.sql
CREATE TABLE IF NOT EXISTS audit_log (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(100) NOT NULL,
    changes JSON,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_user_id (user_id),
    INDEX idx_entity (entity_type, entity_id),
    INDEX idx_created_at (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

3. Open a PR (CI runs it through Flyway on an empty database). Merging to `main` applies it to production before the deploy

### Viewing Migration Status

Flyway tracks applied migrations in the `flyway_schema_history` table:

```sql
SELECT * FROM flyway_schema_history ORDER BY installed_rank;
```

Columns:
- `installed_rank`: Order of execution
- `version`: Migration version
- `description`: Migration description
- `script`: Filename
- `checksum`: Script checksum (detects modifications)
- `installed_on`: When migration was applied
- `execution_time`: How long it took
- `success`: Whether it succeeded

## Troubleshooting

### Migration Failed
1. Check the error in Flyway output
2. Fix the migration SQL
3. Update the checksum or repair:
```sql
DELETE FROM flyway_schema_history WHERE version = 'X';
```
4. Re-run the application

### Checksum Mismatch
If you modify an already-applied migration:
```bash
# Option 1: Repair (updates checksums)
flyway repair

# Option 2: Delete and re-run (development only!)
DELETE FROM flyway_schema_history WHERE version = 'X';
```

**Production:** Never modify applied migrations. Create a new migration instead.

## References

- [Flyway Documentation](https://documentation.red-gate.com/flyway)
- [MySQL Flyway Support](https://documentation.red-gate.com/fd/mysql-277579322.html)
- [Aspire PostgreSQL Extensions Documentation](https://aspire.dev/integrations/databases/postgres/postgresql-extensions/)

## Exporting Current Database Schema

To create initial migrations from your existing database:

```bash
# Using mysqldump
mysqldump \
  -h localhost \
  -u root \
  -p \
  --no-data \
  --skip-add-drop-table \
  --skip-comments \
  valyria > src/Valyria.Database/migrations/V1__initial_schema.sql

# For data
mysqldump \
  -h localhost \
  -u root \
  -p \
  --no-create-info \
  --complete-insert \
  valyria > src/Valyria.Database/migrations/V1.1__initial_data.sql
```

Edit the resulting SQL to:
1. Add `IF NOT EXISTS` clauses
2. Remove AUTO_INCREMENT values
3. Format for readability
4. Split into logical migrations

# Valyria Database Migrations

This project contains Flyway database migration scripts for the Valyria application's MySQL database.

## Overview

Flyway is integrated into the Aspire AppHost using the [CommunityToolkit.Aspire.Hosting.Flyway](https://github.com/CommunityToolkit/Aspire) package. Migrations run automatically when the application starts, ensuring the database schema is up-to-date before the API begins serving requests.

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

### AppHost Configuration

In [Valyria.AppHost/AppHost.cs](../Valyria.AppHost/AppHost.cs):

```csharp
var flywayJdbcUrl = builder.AddParameter("FlywayJdbcUrl");
var flyway = builder.AddFlyway("valyria-migrations", "../Valyria.Database/migrations")
    .WithArgs("-url", flywayJdbcUrl, "migrate");

var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WaitFor(flyway);  // API waits for migrations to complete
```

### User Secrets

Add the `FlywayJdbcUrl` parameter to your user secrets:

```bash
dotnet user-secrets set "Parameters:FlywayJdbcUrl" "jdbc:mysql://localhost:3306/valyria?user=root&password=yourpassword" --project src/Valyria.AppHost
```

**JDBC URL Format for MySQL:**
```
jdbc:mysql://<host>:<port>/<database>?user=<username>&password=<password>
```

Example:
```
jdbc:mysql://localhost:3306/valyria_db?user=admin&password=SecureP@ss123
```

Optional parameters:
```
jdbc:mysql://localhost:3306/valyria_db?user=admin&password=SecureP@ss123&useSSL=false&allowPublicKeyRetrieval=true
```

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

3. Run the Aspire application - migrations apply automatically

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

### Manual Migration (Development/Testing)

If needed, you can run Flyway manually:

```bash
# Using Docker
docker run --rm \
  -v $(pwd)/src/Valyria.Database/migrations:/flyway/sql \
  flyway/flyway:latest \
  -url=jdbc:mysql://host.docker.internal:3306/valyria \
  -user=root \
  -password=yourpassword \
  migrate

# Using Flyway CLI (if installed)
flyway \
  -url=jdbc:mysql://localhost:3306/valyria \
  -user=root \
  -password=yourpassword \
  -locations=filesystem:./src/Valyria.Database/migrations \
  migrate
```

Other commands:
- `info` - Show migration status
- `validate` - Validate applied migrations against available ones
- `baseline` - Baseline an existing database
- `repair` - Repair metadata table

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

### Connection Issues
Verify your JDBC URL:
```bash
# Check user secrets
dotnet user-secrets list --project src/Valyria.AppHost

# Test connection
mysql -h localhost -P 3306 -u root -p
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Deploy

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Run Database Migrations
        env:
          FLYWAY_URL: ${{ secrets.DB_JDBC_URL }}
        run: |
          docker run --rm \
            -v ${{ github.workspace }}/src/Valyria.Database/migrations:/flyway/sql \
            flyway/flyway:latest \
            -url=$FLYWAY_URL \
            migrate
```

### Azure DevOps Example

```yaml
- task: Docker@2
  displayName: 'Run Flyway Migrations'
  inputs:
    command: 'run'
    arguments: >
      --rm
      -v $(Build.SourcesDirectory)/src/Valyria.Database/migrations:/flyway/sql
      flyway/flyway:latest
      -url=$(FlywayJdbcUrl)
      migrate
```

## References

- [Flyway Documentation](https://documentation.red-gate.com/flyway)
- [MySQL Flyway Support](https://documentation.red-gate.com/fd/mysql-277579322.html)
- [CommunityToolkit.Aspire.Hosting.Flyway](https://github.com/CommunityToolkit/Aspire)
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

# Flyway Migration Scripts

This directory contains versioned SQL migration scripts for the Valyria database.

## Naming Convention

Migration files must follow this pattern:
- **Versioned migrations**: `V{version}__{description}.sql`
  - Example: `V1__initial_schema.sql`
  - Example: `V2__add_transactions_table.sql`
  - Example: `V2.1__add_user_email_index.sql`

- **Repeatable migrations**: `R__{description}.sql`
  - Example: `R__create_views.sql`
  - These run whenever their content changes

## Version Numbering

- Use semantic versioning: `V1`, `V2`, `V3`, etc.
- For minor changes between versions: `V2.1`, `V2.2`, etc.
- Always use double underscore `__` to separate version from description
- Use underscores in description: `add_user_table` not `add-user-table`

## Migration Best Practices

1. **Idempotency**: Migrations should be safe to run multiple times
   - Use `IF NOT EXISTS` for CREATE statements
   - Use `IF EXISTS` for DROP statements

2. **Backwards Compatibility**: Design migrations to allow rolling back deployments
   - Add columns with defaults or as nullable
   - Don't immediately remove old columns

3. **Performance**: Be mindful of table locking
   - Add indexes in separate migrations
   - Use `ALGORITHM=INPLACE` when possible for ALTER TABLE

4. **Testing**: Always test migrations on a copy of production data

## Example Migration

```sql
-- V1__initial_schema.sql
CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
```

## Running Migrations

Migrations are automatically applied when the Aspire application starts.

For manual migration runs:
```bash
flyway -url=jdbc:mysql://localhost:3306/valyria -user=root -password=secret migrate
```

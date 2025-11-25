# Secrets Management

This project uses environment variables to manage sensitive configuration values like database passwords and JWT secrets. This approach keeps secrets out of source control and allows different values for different environments.

## Required Secrets

The following secrets must be configured:

- `ConnectionStrings__VepDb`: MySQL database connection string
- `JwtSecret`: Secret key for JWT token signing (minimum 32 characters recommended)

## Setup Instructions

### Option 1: Running with dotnet CLI (Development)

The [launchSettings.json](src/Valyria.Api/Properties/launchSettings.json) file contains default development values for running the API directly with `dotnet run`.

To customize these values locally without committing them:

1. Copy the environment variables section from `launchSettings.json`
2. Modify the values as needed
3. The file is already configured for local development

**To run the API directly:**
```bash
cd src/Valyria.Api
dotnet run
```

### Option 2: Running with Aspire (Development & Production)

When running through Aspire, secrets are injected from the AppHost configuration.

#### Using Environment Variables

Set the required environment variables before running the Aspire AppHost:

**Linux/macOS:**
```bash
export ConnectionStrings__VepDb="server=localhost;database=vep_db;user=myuser;password=mypassword"
export JwtSecret="your_jwt_secret_key_here_minimum_32_characters"

cd src/Valyria.AppHost
dotnet run
```

**Windows (PowerShell):**
```powershell
$env:ConnectionStrings__VepDb="server=localhost;database=vep_db;user=myuser;password=mypassword"
$env:JwtSecret="your_jwt_secret_key_here_minimum_32_characters"

cd src/Valyria.AppHost
dotnet run
```

#### Using .NET User Secrets (Recommended for Development)

For a more permanent local development setup, use .NET User Secrets:

```bash
cd src/Valyria.AppHost

# Initialize user secrets (if not already done)
dotnet user-secrets init

# Set the secrets
dotnet user-secrets set "ConnectionStrings:VepDb" "server=localhost;database=vep_db;user=myuser;password=mypassword"
dotnet user-secrets set "JwtSecret" "your_jwt_secret_key_here_minimum_32_characters"

# Run the AppHost
dotnet run
```

To view your configured secrets:
```bash
dotnet user-secrets list
```

#### Using .env File (Alternative)

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and fill in your actual values:
   ```
   ConnectionStrings__VepDb=server=localhost;database=vep_db;user=myuser;password=mypassword
   JwtSecret=your_actual_jwt_secret_key_minimum_32_characters
   ```

3. Load the environment variables before running:
   ```bash
   # Linux/macOS
   export $(cat .env | xargs)
   cd src/Valyria.AppHost
   dotnet run
   ```

**Note:** The `.env` file is excluded from git via `.gitignore` to prevent accidental commits of secrets.

## Production Deployment

For production deployments:

1. **Azure App Service / Container Apps**: Configure environment variables in the Azure Portal under Configuration > Application Settings

2. **Docker/Kubernetes**: Pass environment variables through container configuration or use secrets management tools

3. **CI/CD Pipelines**: Use secure variable storage in your CI/CD platform (GitHub Secrets, Azure DevOps Variable Groups, etc.)

## Generating a Secure JWT Secret

To generate a secure random JWT secret:

```bash
# Using OpenSSL (64 character hex string)
openssl rand -hex 32

# Using PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

## Security Best Practices

- ✅ Never commit real secrets to source control
- ✅ Use different secrets for each environment (dev, staging, production)
- ✅ Rotate secrets regularly
- ✅ Use strong, randomly generated values for JWT secrets
- ✅ Limit access to production secrets to only necessary personnel
- ❌ Don't share secrets via email, Slack, or other unsecured channels
- ❌ Don't hardcode secrets in application code

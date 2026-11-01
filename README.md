# Valyria Exchange Program

A modern, cloud-native distributed application for managing the Valyrian Exchange Program (VEP) - an aid exchange system for tracking transactions between nations.

## Architecture Overview

This is a **.NET Aspire** distributed application that combines a REST API backend with a modern Vue.js frontend, orchestrated through Microsoft's cloud-native framework.

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Orchestration** | .NET Aspire | .NET 10 |
| **Backend API** | ASP.NET Core | .NET 9 |
| **Core Libraries** | .NET | .NET 8 |
| **Frontend** | Nuxt | 3.15.4 |
| **UI Framework** | Vue | 3.5.13 |
| **Component Library** | Nuxt UI | 3.3.7 |
| **Styling** | Tailwind CSS | via Nuxt UI |
| **State Management** | Pinia | 2.3.0 |
| **Language (Frontend)** | TypeScript | 5.7.2 |
| **Data Access** | Dapper (micro-ORM) | 2.0.123 |
| **Database** | MySQL | 8.0+ |
| **Authentication** | JWT Bearer | ASP.NET Identity |
| **API Documentation** | Swagger/OpenAPI | 6.5.0 |
| **Mapping** | AutoMapper | 12.0.0 |
| **Testing (UI)** | Vitest | 2.1.8 |

## Project Structure

```
Valyria.Aspire/
├── src/
│   ├── Valyria.Api/              # ASP.NET Core Web API (.NET 9)
│   │   ├── Controllers/          # REST API endpoints
│   │   ├── Auth/                 # JWT authentication logic
│   │   ├── Extensions/           # DI configuration
│   │   └── MappingProfile.cs     # AutoMapper configuration
│   │
│   ├── Valyria.Core/             # Domain layer (.NET 8)
│   │   ├── Entities/             # Domain models
│   │   ├── DTOs/                 # Data transfer objects
│   │   ├── Contracts/            # API request/response models
│   │   ├── Enums/                # Domain enumerations
│   │   └── Interfaces/           # Repository interfaces
│   │
│   ├── Valyria.Infrastructure/   # Data access layer (.NET 8)
│   │   ├── Repositories/         # Dapper-based repositories
│   │   ├── Exceptions/           # Custom exceptions
│   │   └── Extensions/           # DI configuration
│   │
│   ├── Valyria.ServiceDefaults/  # Aspire integration (.NET 9)
│   │   └── Extensions.cs         # OpenTelemetry, health checks
│   │
│   ├── Valyria.AppHost/          # Aspire orchestrator (.NET 10)
│   │   └── AppHost.cs            # Service configuration
│   │
│   └── Valyria.Ui/               # Nuxt 3 frontend
│       ├── pages/                # Route pages
│       ├── components/           # Vue components
│       ├── stores/               # Pinia state stores
│       ├── middleware/           # Auth middleware
│       ├── composables/          # Reusable composition functions
│       ├── infrastructure/       # Utilities
│       └── Dockerfile            # Container configuration
│
├── discord-bot/                  # Val Town Discord bot (JavaScript/Deno, not part of Aspire)
│
├── .env.example                  # Environment variables template
└── Valyria.Aspire.sln           # Solution file
```

## Application Flow

```
┌──────────────────────────────────────────┐
│   Valyria.AppHost (Orchestrator)         │
│   • Service discovery                    │
│   • Environment configuration            │
│   • Health monitoring                    │
└────────┬────────────────────────┬────────┘
         │                        │
    ┌────▼─────────┐        ┌────▼──────────┐
    │ Valyria.Api  │        │  Valyria.UI   │
    │ (REST API)   │◄───────│  (Nuxt SPA)   │
    │              │  HTTP  │               │
    │ Port: 5000/  │        │  Port: 3000   │
    │       5001   │        │               │
    └────┬─────────┘        └───────────────┘
         │
         │ Dapper ORM
         │
    ┌────▼─────────┐
    │  MySQL DB    │
    │  (vep_db)    │
    └──────────────┘
```

### Layer Architecture

1. **Presentation**: Nuxt UI components, pages, and middleware
2. **API**: ASP.NET Core controllers with JWT authentication
3. **Service**: Repository pattern with dependency injection
4. **Data**: Dapper ORM + MySQL database
5. **Cross-cutting**: Aspire service defaults (telemetry, health checks, service discovery)

## Discord Bot

[`discord-bot/`](discord-bot/README.md) is a small Discord bot hosted on [Val Town](https://www.val.town), separate from the Aspire app. Members run `/uniquecode` to privately get the unique code for the VEP account(s) linked to their Discord ID. The bot has no database access; it calls the API's `/api/discord` endpoint with the shared `DiscordBotApiKey`. CI deploys it to Val Town on every merge to `main`; see its [README](discord-bot/README.md) for setup and configuration.

## API Endpoints

### Authentication (`/api/user`)
- `POST /api/user/login` - User authentication (returns JWT token)

### Accounts (`/api/accounts`)
- `GET /api/accounts` - List all accounts
- `GET /api/accounts/{id}` - Get account details
- `POST /api/accounts` - Create new account
- `PUT /api/accounts` - Update account
- `DELETE /api/accounts/{id}` - Delete account
- `GET /api/accounts/prospect` - Find prospective accounts
- `GET /api/accounts/topScorers` - Get top performing accounts

### Discord bot (`/api/discord`)
Authenticated with the `X-Api-Key` header (the `DiscordBotApiKey` setting), not a JWT. See [discord-bot/README.md](discord-bot/README.md).
- `GET /api/discord/users/{discordId}/accounts` - Accounts (with unique codes) linked to a Discord user ID

### Deals (`/api/deals`)
- `GET /api/deals/{id}` - Get deal details
- `POST /api/deals` - Create a deal between two slots (admin)
- `PUT /api/deals/{id}/cancel` - Cancel a deal (admin)
- `GET /api/deals/account/{accountId}` - Get an account's active deals and notifications
- `GET /api/deals/account/{accountId}/history` - Get every deal an account has been part of
- `GET /api/deals/{id}/activity?accountId=` - Get aid activity for a deal, from one participant's view
- `GET /api/deals/admin/active` - Search all active deals (admin)
- `GET /api/deals/admin/aid-activity` - Search all aid, with the deal each one matched (admin)
- `GET /api/deals/admin/wait-queue` - List accounts waiting for slots (admin)
- `GET /api/deals/admin/flagged` - List unresolved deal flags (admin)
- `PUT /api/deals/admin/flags/{id}/resolve` - Resolve a deal flag (admin)

### Slots (`/api/slots`)
- `GET /api/slots/account/{accountId}` - Get an account's slots (admin)
- `GET /api/slots/available/{accountId}` - Get an account's slots that aren't in a deal (admin)
- `PUT /api/slots/{id}` - Update a slot's cycle values (admin)

### Admin (`/api/admin`)
- Administrative operations and dashboard

API documentation is available via Swagger at `/swagger` when running.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for AppHost)
- [Node.js 20+](https://nodejs.org/) (LTS recommended)
- [Docker](https://www.docker.com/) (the local MySQL database, Flyway and the Azure Storage emulator run as containers)
- [.NET Aspire Workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)

### Install .NET Aspire Workload

```bash
dotnet workload update
dotnet workload install aspire
```

## Environment Setup

### 1. Database Setup

Nothing to set up: a local run creates its own MySQL database, with meaningful data dated to today. On every `aspire run`:

1. **`mysql`** starts MySQL 8.0 with a persistent data volume (`valyria-local-mysql`) and production's `sql_mode` (production isn't strict, so over-long CN values are truncated rather than failing the import)
2. **`db-migrations`** runs the Flyway migrations in [src/Valyria.Database/migrations](src/Valyria.Database/migrations), with the same image and settings as CI
3. **`valyria-batch-dataloader`** imports today's real CN files from cybernations.net (a few MB), the same way production does
4. **`db-seed`** ([Valyria.Seeding](src/Valyria.Seeding/DevDataSeeder.cs)) fills an empty `vep_db` with about 35 accounts on real nations, with deals in every state (in flight on real aid offers, awaiting send, 1-day warning, extended, final warning, blocked, pending start, cancelled), a team event, admins of each kind, and accounts on hold. The real procedures then work out deal states and flags and match the open slots. Its log lists every seeded account

Every seeded account logs in with its ruler name (or nation ID) and the unique code `LOCAL`. Seeded accounts have no Discord ID, so the data loader never pings anyone. To test the pings or the Discord bot, put your own ID on the seeded Super Admin:

```bash
cd src/Valyria.AppHost
dotnet user-secrets set "Seed:DevDiscordId" "<your Discord user ID>"
```

The data is kept between runs, and the data loader keeps the CN data current, but the seeded deals age like real ones (warnings turn into cancellations after a few days). Use the **Reset & reseed** command on `db-seed` in the dashboard to delete all VEP data and seed it again with dates from today. To start completely fresh, stop the app and delete the `valyria-local-mysql` Docker volume.

**Offline, or CN is down:** set `CnFileSource=Local` (for example `dotnet user-secrets set "CnFileSource" "Local"` in the AppHost) to import the saved set in [src/Valyria.Database/local/cn-files](src/Valyria.Database/local/cn-files) instead. The aid dates are moved forward so the newest aid is dated today.

**Production:** use the `https-prod` launch profile, or set `UseProductionDb=true`, to point the API and data loader at the production database instead (this needs `VepDbConnectionString` and `CnDbConnectionString`, below). Against production, migrations never run, and the data loader only runs when you start it from the dashboard.

### 2. Environment Variables

The application requires two environment variables:

#### Required Variables

1. **`VepDbConnectionString`** and **`CnDbConnectionString`** - production MySQL connection strings, only needed with `UseProductionDb` (see above). Get them from LWW or Devo

2. **`JwtSecret`** - Secret key for JWT token signing (minimum 32 characters)
   ```bash
   # Generate a secure key:
   openssl rand -hex 32
   ```

3. **`DiscordBotApiKey`** - API key the [Discord bot](discord-bot/README.md) sends to `/api/discord`. Production uses the same value as the bot's `VEP_API_KEY` on Val Town; locally, any random value works unless you're testing the bot
   ```bash
   openssl rand -base64 32
   ```

#### Configuration Methods

Choose ONE of the following methods:

##### Option A: .NET User Secrets (Recommended for Development)

```bash
# Navigate to the AppHost project
cd src/Valyria.AppHost

# Set the database connection string
dotnet user-secrets set "VepDbConnectionString" "server=localhost;database=vep_db;user=vep_user;password=your_password"

# Set the JWT secret (generate a random key first)
dotnet user-secrets set "JwtSecret" "$(openssl rand -hex 32)"

# Set the Discord bot's API key
dotnet user-secrets set "Parameters:DiscordBotApiKey" "$(openssl rand -base64 32)"
```

##### Option B: Environment Variables

```bash
# Linux/macOS
export VepDbConnectionString="server=localhost;database=vep_db;user=vep_user;password=your_password"
export JwtSecret="your_jwt_secret_key_here_minimum_32_characters"

# Windows (PowerShell)
$env:VepDbConnectionString="server=localhost;database=vep_db;user=vep_user;password=your_password"
$env:JwtSecret="your_jwt_secret_key_here_minimum_32_characters"
```

##### Option C: .env File

```bash
# Copy the example file
cp .env.example .env

# Edit .env with your values
nano .env
```

The `.env` file should contain:
```bash
ConnectionStrings__VepDb=server=localhost;database=vep_db;user=vep_user;password=your_password
JwtSecret=your_jwt_secret_key_here_minimum_32_characters
```

### 3. Install Frontend Dependencies

```bash
cd src/Valyria.Ui
npm install --legacy-peer-deps
```

## Running the Application

### Method 1: Using .NET Aspire (Recommended)

This method runs both the API and UI together with the Aspire dashboard for monitoring.

```bash
# From the root directory
cd src/Valyria.AppHost
dotnet run
```

The Aspire dashboard will start automatically:
- **Aspire Dashboard**: https://localhost:17024 (or http://localhost:15044)
- **API**: Accessible through Aspire (typically http://localhost:5000)
- **UI**: Accessible through Aspire (typically http://localhost:3000)
- **API Swagger**: http://localhost:5000/swagger

The dashboard provides:
- Real-time service health monitoring
- Distributed tracing with OpenTelemetry
- Logs aggregation
- Metrics visualization
- Service dependency mapping

### Method 2: Running Services Individually

#### Run API Only

```bash
cd src/Valyria.Api
dotnet run
```

- API: http://localhost:5000 (HTTP) or https://localhost:5001 (HTTPS)
- Swagger: http://localhost:5000/swagger

#### Run UI Only

```bash
cd src/Valyria.Ui
npm run dev
```

- UI: http://localhost:3000

**Note**: When running individually, ensure the API is started first and the UI is configured with the correct API URL via the `NUXT_PUBLIC_API_URL` environment variable.

## Debugging

### Debugging in Visual Studio 2022

1. Open [Valyria.Aspire.sln](Valyria.Aspire.sln)
2. Set [Valyria.AppHost](src/Valyria.AppHost/Valyria.AppHost.csproj) as the startup project
3. Press F5 to start debugging
4. The Aspire dashboard will open automatically
5. Set breakpoints in any project (API, Infrastructure, etc.)

### Debugging in VS Code

1. Install the [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
2. Open the workspace folder
3. Use the `.NET Aspire` launch configuration
4. Press F5 to start debugging

### Debugging the Frontend

```bash
cd src/Valyria.Ui

# Run with Nuxt DevTools
npm run dev

# Run tests in watch mode
npm run test

# Run tests with UI
npm run test:ui
```

Use browser DevTools for frontend debugging. The Vue DevTools extension is recommended.

### Debugging Individual Services

To debug just the API without Aspire:

```bash
cd src/Valyria.Api
dotnet run --launch-profile http
```

Set environment variables before running:
```bash
export VepDbConnectionString="server=localhost;database=vep_db;user=vep_user;password=your_password"
export JwtSecret="your_jwt_secret_key"
dotnet run
```

## Testing

### Backend Testing

```bash
# Run all tests in the solution
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

### Frontend Testing

```bash
cd src/Valyria.Ui

# Run unit tests
npm run test

# Run tests with UI
npm run test:ui

# Run tests in watch mode
npm run test -- --watch
```

### Integration Testing

With Aspire running, you can test the full stack:

1. Start the application via AppHost
2. Navigate to the Aspire dashboard
3. Use the dashboard to view traces, logs, and metrics
4. Test API endpoints through Swagger UI
5. Test the UI at the configured port

## Building for Production

### Build All Projects

```bash
# Build the entire solution
dotnet build --configuration Release
```

### Build API Docker Image

```bash
cd src/Valyria.Api
dotnet publish -c Release
```

### Build UI Docker Image

```bash
cd src/Valyria.Ui

# Build the Docker image
docker build -t valyria-ui:latest .

# Run the container
docker run -p 3000:3000 -e NUXT_PUBLIC_API_URL=http://localhost:5000 valyria-ui:latest
```

### Deploy with Aspire

Aspire supports deployment to:
- **Azure Container Apps** (configured via `AddAzureContainerAppEnvironment`)
- **Docker Compose**
- **Kubernetes**

```bash
# Generate deployment manifests
cd src/Valyria.AppHost
dotnet run --publisher manifest --output-path ../deploy
```

This generates deployment configurations for your target environment.

## Database Migrations

Currently, the application uses Dapper for data access. Database schema changes should be managed through:

1. Manual SQL scripts
2. Migration tools like [FluentMigrator](https://fluentmigrator.github.io/) or [DbUp](https://dbup.readthedocs.io/)

Example migration script location: `src/Valyria.Infrastructure/Migrations/`

## Security Considerations

- **JWT Tokens**: Ensure `JwtSecret` is strong (32+ characters) and kept secure
- **Discord bot API key**: `DiscordBotApiKey` only opens `/api/discord`; keep it random (`openssl rand -base64 32`) and rotate it in GitHub and Val Town together
- **HTTPS**: In production, enable HTTPS redirection (currently disabled in development)
- **CORS**: Configure CORS policy in [Program.cs](src/Valyria.Api/Program.cs#L85-L93) for production
- **Database**: Use strong passwords and restrict database access
- **Secrets**: Never commit `.env` files or user secrets to version control

## Troubleshooting

### Common Issues

#### "JwtSecret is not configured"

Ensure you've set the `JwtSecret` using one of the configuration methods above.

```bash
cd src/Valyria.AppHost
dotnet user-secrets set "JwtSecret" "$(openssl rand -hex 32)"
```

#### "Unable to connect to database"

For a local run, check that Docker is running and the `mysql` and `db-migrations` resources are healthy in the dashboard. With `UseProductionDb`, verify the connection string is correct:

```bash
mysql -u vep_user -p -e "SELECT 1;"
```

#### "Port already in use"

If ports 5000, 5001, or 3000 are in use:

```bash
# Find process using port
lsof -i :5000  # macOS/Linux
netstat -ano | findstr :5000  # Windows

# Kill the process or change ports in launchSettings.json
```

#### "npm install fails"

The project requires the `--legacy-peer-deps` flag:

```bash
cd src/Valyria.Ui
npm install --legacy-peer-deps
```

#### UI can't connect to API

Check that `NUXT_PUBLIC_API_URL` is set correctly. When running through Aspire, this is configured automatically. When running standalone:

```bash
export NUXT_PUBLIC_API_URL=http://localhost:5000
npm run dev
```

## Project Architecture Decisions

### Why .NET Aspire?

- **Service Orchestration**: Simplifies running multiple services together
- **Observability**: Built-in distributed tracing, metrics, and logging
- **Service Discovery**: Automatic endpoint configuration
- **Cloud-Native**: First-class support for Azure Container Apps and Kubernetes
- **Developer Experience**: Unified dashboard for monitoring all services

### Why Dapper?

- **Performance**: Minimal overhead compared to full ORMs
- **Simplicity**: Direct SQL control for complex queries
- **Flexibility**: Easy to optimize specific queries
- **MySQL Support**: Excellent compatibility with MySQL

### Why Nuxt 3?

- **SSR/SSG Support**: Better SEO and performance
- **Auto-imports**: Reduced boilerplate
- **File-based Routing**: Intuitive page structure
- **TypeScript**: Full type safety
- **Vite**: Fast HMR and build times

## Contributing

1. Create a feature branch from `main`
2. Make your changes
3. Test thoroughly (both unit and integration tests)
4. Submit a pull request with a clear description

## License

[Specify your license here]

## Support

For issues and questions:
- Check the [Aspire documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- Review [Nuxt documentation](https://nuxt.com/docs)
- Open an issue in this repository

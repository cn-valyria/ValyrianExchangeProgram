# Tests

| Project | What it covers | Needs |
|---|---|---|
| `Valyria.UnitTests` | Controllers, auth (token provider, JWT events, filters), the API pipeline booted in memory, the data loader worker and Mapster mappings, the CN file parser and the Discord webhook | Nothing |
| `Valyria.IntegrationTests` | Every Dapper repository, run against MySQL 8 built from the real Flyway migrations in `src/Valyria.Database/migrations` | Docker |

## Running

```bash
dotnet test                                   # everything
dotnet test tests/Valyria.UnitTests           # no Docker needed
dotnet test tests/Valyria.IntegrationTests    # starts a mysql:8.0 container
```

The integration tests start a single container for the run (about 15 seconds the first time, while the image downloads and migrations apply), and wipe its data before each test. They run one at a time because they share that database.

## Coverage

```bash
dotnet tool restore
dotnet test --settings coverage.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:TestResults/CoverageReport -reporttypes:"TextSummary;Html"
```

Open `TestResults/CoverageReport/index.html`, or read `TestResults/CoverageReport/Summary.txt`. Remove `TestResults` between runs, or the report merges old results in.

`coverage.runsettings` measures `Valyria.Api`, `Valyria.Core`, `Valyria.Infrastructure` and `Valyria.Batch.DataLoader`. It leaves out the AppHost, ServiceDefaults (Aspire template code) and the data loader's `Program.cs`.

`CnFileGrabber` is untested: it downloads from cybernations.net with its own `HttpClient`, so testing it would mean injecting the client first.

## Skipped tests

Two integration tests are skipped because they describe bugs. Each skip reason names the line at fault. Remove the `Skip` once the bug is fixed.

# AsyncAwaitFnbOnPrem

## Tooling prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com) (runtime too, if running projects manually)
- Docker Desktop or equivalent Docker Engine (Testcontainers, RabbitMQ + SQL Server depend on it)
- [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet) (installed globally to run migrations if needed)

## Configuration
1. Copy `.env` or set environment variables if you need to override defaults (see `appsettings.json` in each project). Defaults use:
   - SQL Server: `Server=localhost,14333;Database=AsyncAwaitFnb;User Id=sa;Password=Your_password123;TrustServerCertificate=True`
   - RabbitMQ: `localhost`, `guest` credentials, `fnb.events` exchange.
2. `Order.Api`, `Payment.Consumer`, and `Kitchen.Consumer` load `appsettings.json`; you can override via environment variables (e.g., `RabbitMQ__Host`).
3. Ensure `scripts/dev-up.*` is adjusted if you change ports or credentials.

## Environment setup
1. Start the required infrastructure:
   ```bash
   ./scripts/dev-up.sh     # or scripts/dev-up.ps1
   ```
2. Wait for Docker to report `rabbitmq` and `sqlserver` as healthy (check `docker compose ps` or visit RabbitMQ on `http://localhost:15672`).
3. (Optional) Use `dotnet ef database update --project src/Order.Infrastructure` to preseed schemas—`Order.Api` already migrates at startup in Development.

## Building the solution
1. Restore & build everything:
   ```bash
   dotnet build AsyncAwaitFnbOnPrem.sln
   ```
2. Run all tests (integration suite needs Docker):
   ```bash
   dotnet test AsyncAwaitFnbOnPrem.sln
   ```

## Running services
1. Launch `Order.Api`:
   ```bash
   cd src/Order.Api
   dotnet run
   ```
2. Launch consumers (prefer separate terminals):
   ```bash
   cd src/Payment.Consumer && dotnet run
   cd src/Kitchen.Consumer && dotnet run
   ```
3. Monitor logs; API has OpenAPI UI on `/swagger` when running in Development.

## Notes
- Use `ConnectionStrings:SqlServer` and `RabbitMQ` sections for overrides (environment variables support `:` notation).
- Integration tests depend on Docker; ensure Docker named pipe/daemon is accessible before running them.

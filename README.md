# AsyncAwaitFnbOnPrem

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com) (repo pins SDK in `global.json`)
- Docker Desktop (or compatible Docker Engine)
- Optional for manual migrations: [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet)

Default local infrastructure values:
- SQL Server: `Server=localhost,14333;Database=AsyncAwaitFnb;User Id=sa;Password=Your_password123;TrustServerCertificate=True`
- RabbitMQ: host `localhost`, user `guest`, password `guest`, exchange `fnb.events`

## First-Time Setup (Install + Verify)
1. Clone the repository and open a terminal in the repo root.
2. Confirm tool versions:
   ```bash
   dotnet --version
   docker --version
   ```
3. Start infrastructure containers:
   ```bash
   # Linux/macOS
   ./scripts/dev-up.sh

   # Windows PowerShell
   ./scripts/dev-up.ps1
   ```
4. Wait until both services are healthy:
   ```bash
   docker compose ps
   ```
5. Restore and build:
   ```bash
   dotnet restore AsyncAwaitFnbOnPrem.sln
   dotnet build AsyncAwaitFnbOnPrem.sln
   ```
6. Optional validation tests:
   ```bash
   dotnet test AsyncAwaitFnbOnPrem.sln
   ```

## Next Runs (After Initial Setup)
1. Start infrastructure:
   ```bash
   ./scripts/dev-up.sh      # or ./scripts/dev-up.ps1
   ```
2. Run services in separate terminals:
   ```bash
   dotnet run --project src/Order.Api/Order.Api.csproj
   dotnet run --project src/Payment.Consumer/Payment.Consumer.csproj
   dotnet run --project src/Kitchen.Consumer/Kitchen.Consumer.csproj
   ```
3. Open API Swagger:
   - `http://localhost:5000/swagger`
4. When finished, stop infrastructure:
   ```bash
   ./scripts/dev-down.sh    # or ./scripts/dev-down.ps1
   ```

## Configuration Overrides
- Services read `appsettings.json` + environment variables.
- Common overrides:
  - `ConnectionStrings__SqlServer`
  - `RabbitMQ__Host`
  - `RabbitMQ__Username`
  - `RabbitMQ__Password`

# PartnerAPI

A minimal .NET 10 Partner API focused on CRUD operations for partner records.

## Projects
- `src/Partner.Api`: ASP.NET Core minimal API with in-memory repository.
- `tests/Partner.Api.Tests`: xUnit unit tests for repository behavior.

## Run locally
```bash
dotnet restore PartnerAPI.sln
dotnet run --project src/Partner.Api/Partner.Api.csproj
```

Swagger UI is available at `http://localhost:5000/swagger` by default.

## Test
```bash
dotnet test PartnerAPI.sln
```

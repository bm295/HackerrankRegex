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

## Keycloak JWT integration
- Protected endpoints: all `/partners` routes now require a valid Bearer token.
- Public endpoint: `/health`.
- Configure Keycloak in `src/Partner.Api/appsettings.json` under `Keycloak`:
  - `Authority`: realm issuer URL, e.g. `http://localhost:8080/realms/partner-realm`
  - `Realm`: realm name
  - `ClientId`: API client/audience (e.g. `partner-api`)
  - `RequireHttpsMetadata`: set `false` for local HTTP development

Example token request for client credentials:
```bash
curl -X POST \
  "http://localhost:8080/realms/partner-realm/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=partner-api&client_secret=<secret>"
```

## Test
```bash
dotnet test PartnerAPI.sln
```

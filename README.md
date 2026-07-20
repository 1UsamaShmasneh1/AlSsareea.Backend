# AlSsareea Backend

Backend foundation for **AlSsareea (عالسريع)**, a multilingual delivery platform. The solution is a modular monolith designed to preserve clear module boundaries and allow selected modules to be extracted later if operational needs justify it.

## Status

Foundation only. The solution currently provides shared domain/application building blocks, an Identity module skeleton, a minimal HTTP API, localization setup, health checks, Problem Details, OpenAPI, correlation IDs, and automated tests. Authentication, persistence, payments, maps, and delivery workflows have not been implemented.

## Requirements

- .NET SDK `10.0.302` or a newer compatible .NET 10 feature band
- No external services or database are required

## Restore, build, run, and test

Run these commands from this directory:

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/AlSsareea.Api
dotnet test --no-build
dotnet format --verify-no-changes
```

For local HTTPS, trust the ASP.NET Core development certificate if your machine has not already done so:

```powershell
dotnet dev-certs https --trust
```

## Current endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Application health probe |
| `GET` | `/api/system/info` | Non-sensitive service metadata |
| `GET` | `/openapi/v1.json` | OpenAPI document in Development only |

Future versioned business endpoints will use the `/api/v1` base path. The unversioned system endpoints are operational endpoints rather than business contracts.

## Solution structure

- `src/AlSsareea.Api`: HTTP composition root and minimal endpoints.
- `src/BuildingBlocks`: framework-neutral domain and application abstractions, contracts, and shared infrastructure implementations.
- `src/Modules/Identity`: Identity domain and layer skeleton; it does not yet authenticate users.
- `tests`: unit, integration, and architecture tests.
- `docs`: architecture notes and Architecture Decision Records.

## Contribution rules

Keep domain code independent of application, infrastructure, and ASP.NET Core. Application code must not depend on infrastructure. Modules may communicate only through public contracts and must never reference another module's infrastructure. Use UTC timestamps, central package versions, stable packages, and add no secrets. Run restore, build, tests, and format verification before handing off a change.

This project remains in its founding stage. Database persistence, authentication, payment processing, and map integrations are intentionally deferred.

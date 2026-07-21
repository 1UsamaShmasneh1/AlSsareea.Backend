# AlSsareea Backend

Backend foundation for **AlSsareea (عالسريع)**, a multilingual delivery platform. The solution is a modular monolith with explicit module boundaries and independently owned persistence.

## Status

Phase 3 adds production-oriented authentication and authorization to the durable Phase 2 Identity model: PBKDF2 password hashing, JWT access tokens, rotating refresh tokens with replay response, durable sessions and devices, lockout, role/permission policies, OTP primitives, scoped idempotency, audit records, rate limiting, and security headers. See [the authentication architecture](docs/architecture/authentication.md).

## Requirements

- .NET SDK `10.0.302` or a newer compatible .NET 10 feature band
- Docker Desktop or Docker Engine
- Docker Compose

Restore the repository-local EF CLI tool and packages once:

```powershell
dotnet tool restore
dotnet restore
```

## PostgreSQL/PostGIS

Compose uses development-only credentials. Override `POSTGRES_PASSWORD` when desired; never reuse the default outside local development.

```powershell
docker compose up -d
docker compose ps
```

Stop without deleting the named volume:

```powershell
docker compose down
```

`docker compose down -v` also deletes all local database data and must be used with care.

## Connection string

Identity reads `ConnectionStrings:IdentityDatabase`. `appsettings.Development.json` contains a local-only value matching Compose. Override it with `ConnectionStrings__IdentityDatabase` or user secrets:

```powershell
dotnet user-secrets init --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:IdentityDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
```

No production connection string is stored in the repository.

## Authentication configuration

Production must provide the following through environment variables or a secret manager. Startup validation rejects missing/weak JWT and OTP secrets and unsafe lifetimes or hashing parameters.

```powershell
$env:Authentication__Jwt__Issuer = "https://identity.example.com"
$env:Authentication__Jwt__Audience = "alssareea-clients"
$env:Authentication__Jwt__SigningKey = "<at-least-32-random-bytes>"
$env:Authentication__Otp__Pepper = "<at-least-32-random-bytes>"
```

The checked-in Development values are conspicuous local placeholders, not production credentials. Replace them with user secrets for shared development environments. `Authentication:Otp:DevelopmentProviderEnabled` must be false in Production; startup fails otherwise.

## Migrations

Run from the repository root. The design-time factory uses `ConnectionStrings__IdentityDatabase` when present and otherwise uses the documented local-development fallback.

```powershell
$identityProject = ".\src\Modules\Identity\AlSsareea.Modules.Identity.Infrastructure\AlSsareea.Modules.Identity.Infrastructure.csproj"

dotnet ef migrations add <MigrationName> --project $identityProject --context IdentityDbContext --output-dir Persistence\Migrations
dotnet ef database update --project $identityProject --context IdentityDbContext
dotnet ef migrations remove --project $identityProject --context IdentityDbContext
dotnet ef migrations list --project $identityProject --context IdentityDbContext
dotnet ef migrations has-pending-model-changes --project $identityProject --context IdentityDbContext
```

Only remove a migration that has not been applied. Migrations are never applied automatically when the API starts.

## Restore, build, run, and test

Run these commands from this directory:

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/AlSsareea.Api
dotnet test --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

Integration tests require Docker. Testcontainers starts an isolated PostGIS container and does not use the local Compose database.

For local HTTPS, trust the ASP.NET Core development certificate if your machine has not already done so:

```powershell
dotnet dev-certs https --trust
```

## Current endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Application health probe |
| `GET` | `/health/live` | Process liveness; independent of PostgreSQL |
| `GET` | `/health/ready` | Readiness, including Identity PostgreSQL connectivity |
| `GET` | `/api/system/info` | Non-sensitive service metadata |
| `POST` | `/api/v1/auth/login` | Password login and token issuance |
| `POST` | `/api/v1/auth/refresh` | Atomic refresh-token rotation |
| `POST` | `/api/v1/auth/logout` | Revoke the current session |
| `POST` | `/api/v1/auth/logout-all` | Revoke all sessions and rotate the security stamp |
| `GET` | `/api/v1/auth/me` | Current authenticated identity |
| `GET` | `/api/v1/auth/sessions` | Current user's sessions; requires `identity.sessions.read` |
| `DELETE` | `/api/v1/auth/sessions/{sessionId}` | Revoke an owned session; requires `identity.sessions.revoke` |
| `POST` | `/api/v1/auth/otp/challenges` | Create a development/test OTP challenge |
| `POST` | `/api/v1/auth/otp/challenges/{challengeId}/verify` | Atomically consume an OTP |
| `GET` | `/openapi/v1.json` | OpenAPI document in Development only |

Future versioned business endpoints will use the `/api/v1` base path. The unversioned system endpoints are operational endpoints rather than business contracts.

## Solution structure

- `src/AlSsareea.Api`: HTTP composition root and minimal endpoints.
- `src/BuildingBlocks`: framework-neutral domain and application abstractions, contracts, and shared infrastructure implementations.
- `src/Modules/Identity`: Identity domain, authentication application contracts, and persistence/security implementation.
- `tests`: unit, integration, and architecture tests.
- `docs`: architecture notes and Architecture Decision Records.

## Contribution rules

Keep domain code independent of application, infrastructure, and ASP.NET Core. Application code must not depend on infrastructure. Modules may communicate only through public contracts and must never reference another module's infrastructure. Use UTC timestamps, central package versions, stable packages, and add no secrets. Run restore, build, tests, and format verification before handing off a change.

## Data strategy

- Start with one PostgreSQL database.
- Give each module its own schema, `DbContext`, migrations, and migration-history table.
- Keep migrations inside the owning module's Infrastructure project.
- Do not access another module's schema or Infrastructure directly.
- Do not introduce a system-wide `DbContext` or generic repository.
- Do not use EF Core InMemory or SQLite for persistence integration tests.
- Do not run migrations automatically in production.

Identity owns schema `identity`, its own migration history, and thirteen tables documented in `docs/architecture/identity-domain.md`. PostGIS is enabled for future geographic modules, but Identity has no spatial entity.

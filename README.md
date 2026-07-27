# AlSsareea Backend

Backend foundation for **AlSsareea (عالسريع)**, a multilingual delivery platform. The solution is a modular monolith with explicit module boundaries and independently owned persistence.

## Status

Phase 9B adds the Promotions module on top of the completed Identity, authentication,
authorization, Customers, and Maps phases. The solution now includes merchant and branch
lifecycles, scoped employees and ownership, relational schedules and overrides, PostGIS
branch locations, Maps-owned service-area assignments, and merchant-owned localized
catalogs and products, plus validated local image storage, variants, lifecycle management,
contract-based Catalog media references, and deterministic scoped pricing policies with
integer-minor-unit calculations and immutable calculation snapshots. See
[the Customers architecture](docs/architecture/customers.md) and
[the Maps architecture](docs/architecture/maps.md), and
[the Merchants architecture](docs/architecture/merchants.md), and
[the Catalog module](docs/modules/catalog.md), and
[the Media module](docs/modules/media.md), and
[the Pricing module](docs/modules/pricing.md), and
[the Promotions architecture](docs/architecture/promotions.md).

## Requirements

- .NET SDK `10.0.302` or a newer compatible .NET 10 feature band
- Docker Desktop or Docker Engine
- Docker Compose
- PostgreSQL with PostGIS when module persistence is used

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

Identity, Customers, Maps, Merchants, Catalog, Media, Pricing, and Promotions own separate contexts and migration histories while normally
using the same PostgreSQL database. Configure `ConnectionStrings:IdentityDatabase`,
`ConnectionStrings:CustomersDatabase`, `ConnectionStrings:MapsDatabase`,
`ConnectionStrings:MerchantsDatabase`, `ConnectionStrings:CatalogDatabase`, and
`ConnectionStrings:MediaDatabase`, `ConnectionStrings:PricingDatabase`, and
`ConnectionStrings:PromotionsDatabase`.
`appsettings.Development.json` contains local-only values matching Compose.

```powershell
dotnet user-secrets init --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:IdentityDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:CustomersDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:MapsDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:CatalogDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:MediaDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:PricingDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:MerchantsDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
dotnet user-secrets set "ConnectionStrings:PromotionsDatabase" "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=<development-password>" --project src/AlSsareea.Api
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
$customersProject = ".\src\Modules\Customers\AlSsareea.Modules.Customers.Infrastructure\AlSsareea.Modules.Customers.Infrastructure.csproj"
$mapsProject = ".\src\Modules\Maps\AlSsareea.Modules.Maps.Infrastructure\AlSsareea.Modules.Maps.Infrastructure.csproj"
$catalogProject = ".\src\Modules\Catalog\AlSsareea.Modules.Catalog.Infrastructure\AlSsareea.Modules.Catalog.Infrastructure.csproj"
$mediaProject = ".\src\Modules\Media\AlSsareea.Modules.Media.Infrastructure\AlSsareea.Modules.Media.Infrastructure.csproj"
$pricingProject = ".\src\Modules\Pricing\AlSsareea.Modules.Pricing.Infrastructure\AlSsareea.Modules.Pricing.Infrastructure.csproj"
$promotionsProject = ".\src\Modules\Promotions\AlSsareea.Modules.Promotions.Infrastructure\AlSsareea.Modules.Promotions.Infrastructure.csproj"

dotnet ef migrations add <MigrationName> --project $identityProject --context IdentityDbContext --output-dir Persistence\Migrations
dotnet ef database update --project $identityProject --context IdentityDbContext
dotnet ef migrations remove --project $identityProject --context IdentityDbContext
dotnet ef migrations list --project $identityProject --context IdentityDbContext
dotnet ef migrations has-pending-model-changes --project $identityProject --context IdentityDbContext
dotnet ef database update --project $customersProject --context CustomersDbContext
dotnet ef migrations has-pending-model-changes --project $customersProject --context CustomersDbContext
dotnet ef database update --project $mapsProject --context MapsDbContext
dotnet ef migrations has-pending-model-changes --project $mapsProject --context MapsDbContext
dotnet ef database update --project $catalogProject --context CatalogDbContext
dotnet ef migrations has-pending-model-changes --project $catalogProject --context CatalogDbContext
dotnet ef database update --project $mediaProject --context MediaDbContext
dotnet ef migrations has-pending-model-changes --project $mediaProject --context MediaDbContext
dotnet ef database update --project $pricingProject --context PricingDbContext
dotnet ef migrations has-pending-model-changes --project $pricingProject --context PricingDbContext
dotnet ef database update --project $promotionsProject --context PromotionsDbContext
dotnet ef migrations has-pending-model-changes --project $promotionsProject --context PromotionsDbContext
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
| `POST` | `/api/v1/customers/me` | Create the authenticated user's customer profile |
| `GET`, `PUT` | `/api/v1/customers/me` | Read or update the owned profile |
| `GET`, `POST` | `/api/v1/customers/me/addresses` | List or add owned addresses |
| `GET`, `PUT`, `DELETE` | `/api/v1/customers/me/addresses/{addressId}` | Read, update, or soft-delete an owned address |
| `PUT` | `/api/v1/customers/me/addresses/{addressId}/default` | Select the owned default address |
| `GET`, `PUT` | `/api/v1/customers/me/preferences` | Read or update owned preferences |
| `GET`, `PUT` | `/api/v1/admin/customers...` | Permission-protected customer administration |
| `POST` | `/api/v1/pricing/estimates` | Resolve a scoped pricing policy and return a deterministic breakdown and snapshot |
| `GET`, `POST` | `/api/v1/pricing/policies` | Permission-protected pricing policy queries and creation |
| `GET`, `PUT` | `/api/v1/pricing/policies/{policyId}` | Read or update a pricing policy |
| `PUT` | `/api/v1/pricing/policies/{policyId}/rules` | Replace a draft policy's rules |
| `POST` | `/api/v1/pricing/policies/{policyId}/{activate|deactivate|archive}` | Transition a pricing policy lifecycle |
| `GET`, `POST` | `/api/v1/promotions` | Permission- and scope-protected promotion administration |
| `GET`, `PUT` | `/api/v1/promotions/{promotionId}` | Read or update a promotion |
| `POST` | `/api/v1/promotions/{promotionId}/activate` | Activate a promotion |
| `POST` | `/api/v1/promotions/evaluate` | Return an explainable promotional evaluation |
| `POST` | `/api/v1/promotions/coupons/validate` | Validate and evaluate one coupon |

Future versioned business endpoints will use the `/api/v1` base path. The unversioned system endpoints are operational endpoints rather than business contracts.

## Solution structure

- `src/AlSsareea.Api`: HTTP composition root and minimal endpoints.
- `src/BuildingBlocks`: framework-neutral domain and application abstractions, contracts, and shared infrastructure implementations.
- `src/Modules/Identity`: Identity domain, authentication application contracts, and persistence/security implementation.
- `src/Modules/Customers`: Customer domain, stable HTTP contracts, application abstractions, and owned persistence.
- `src/Modules/Maps`: Provider-neutral maps contracts, service areas, PostGIS persistence, and a deterministic fake provider.
- `src/Modules/Merchants`: Merchant and branch lifecycles, schedules, service-area assignments, ownership, and merchant-scoped memberships.
- `src/Modules/Catalog`: Merchant-owned localized catalogs, products, options, availability, and immutable product snapshots.
- `src/Modules/Media`: Validated image lifecycle, local storage abstraction, variants, and media lookup contracts.
- `src/Modules/Pricing`: Scoped pricing policy lifecycle, deterministic fee calculation, and calculation snapshots.
- `src/Modules/Promotions`: Promotion lifecycle, coupons, eligibility, evaluation, funding attribution, and owned persistence.
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

Identity owns schema `identity`. Customers owns schema `customers`, a separate migration
history, four tables, and PostGIS Point storage documented in
`docs/architecture/customers.md`. Maps owns schema `maps`, service-area MultiPolygon
boundaries, and its own migration history documented in `docs/architecture/maps.md`.
There are no cross-schema foreign keys.
Promotions owns schema `promotions`, its own migration history, promotion, redemption,
and audit tables, and no cross-schema foreign keys.

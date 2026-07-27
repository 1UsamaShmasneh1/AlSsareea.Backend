# Promotions module

## Responsibility and boundaries

Promotions owns promotional definitions, lifecycle, coupon codes, eligibility, usage limits,
priority and conflict decisions, funding attribution, evaluation, and idempotent redemption
records. It does not own base prices, fees, taxes, carts, checkout, orders, payments,
settlements, or a ledger.

The module consists of Domain, Application, Contracts, and Infrastructure projects.
Domain depends only on BuildingBlocks Domain. Application depends on Domain, Contracts,
BuildingBlocks Application, and Catalog Domain only for the existing strongly typed
`ProductId` and `CategoryId`. Infrastructure owns EF Core and uses the public
`IMerchantCatalogScopeProvider` and `ICatalogPromotionScopeProvider` contracts to authorize
and validate merchant-owned scopes. It never references another module's Infrastructure
or DbContext. Cross-module identifiers remain scalar UUIDs in persistence, with no
cross-schema foreign keys.

Evaluation accepts the existing Pricing `PricingBreakdownDto` and an optional
`PricingSnapshotDto`. Pricing remains the sole source of base price, fee, and tax
calculation; Promotions only calculates promotional adjustments.

## Domain model

`Promotion` is the aggregate root and uses a strongly typed `PromotionId`, GUID
optimistic-concurrency stamp, UTC timestamps, and focused domain events. Localized name
and optional description store explicit Arabic, Hebrew, and English fields, matching the
three cultures supported by the API.

Supported promotion types:

- coupon;
- product discount;
- category discount;
- merchant discount;
- order-threshold discount;
- free delivery.

Benefits are fixed minor-unit amounts, percentage basis points (1–10000) with an optional
minor-unit cap, or free delivery. Money uses `long`; currency is a normalized three-letter
ISO code. Discounts are capped at the eligible amount and cannot create negative totals.

Scope is global, merchant, branch, category, or product. Merchant-owned scopes persist an
explicit merchant ID. Multiple product/category/branch IDs are stored in a PostgreSQL UUID
array with a GIN index. Product and category scope is validated through Catalog's narrow
contract, whose implementation uses Catalog's real strongly typed IDs; branch scope uses
the existing Merchants contract.

## Lifecycle

New promotions begin `Draft`. Draft and suspended promotions may be activated if their
validity has not ended. Only active promotions may be suspended. Archived is terminal.
Evaluation requires both `Active` status and a timestamp inside the half-open validity
period `[start, end)`; an ended active record is rejected as expired without relying on
status alone. Application operations obtain time through `IClock`.

## Coupons, eligibility, and usage

Coupon normalization trims, removes whitespace, uppercases invariantly, and validates a
restricted non-sensitive code alphabet. `normalized_coupon_code` has a filtered unique
PostgreSQL index, so uniqueness is case-insensitive by construction.

Eligibility supports minimum subtotal, an optional customer restriction, first-order
input, merchant/branch/product/category scope, currency, coupon requirement, validity,
and usage availability. It deliberately uses explicit rules rather than an expression
engine. Global, per-customer, budget, and per-order limits are nullable; all-null is the
explicit unlimited representation.

`promotion_redemptions` is an append-only foundation for usage accounting. External
references are globally unique for idempotency. Recording is an explicit administrative
operation; Cart and Order do not consume coupons in this phase.

## Priority, stacking, and evaluation

Eligible candidates are ordered independently of database order:

1. higher priority;
2. larger customer discount;
3. earlier validity start;
4. ascending stable `PromotionId`.

Exclusive and non-stackable promotions reject later candidates. At most one candidate
from a normalized conflict group is selected. The response includes applied and rejected
decisions, stable reason codes, original and final amounts, product/free-delivery
adjustments, conflict decisions, funding split, evaluation timestamp, and snapshots.

Each `PromotionEvaluationSnapshot` carries promotion ID, concurrency-version reference,
type, normalized coupon when present, discount/currency, funding breakdown, applied
scope, machine-readable reason, evaluation time, and optional Pricing policy/version
references. Phase 10 may persist this in Cart; Phase 11 may copy it to an immutable Order
snapshot.

## Funding

Funding is Platform, Merchant, or Shared. Shares are basis points totaling 10000 and must
match the selected source. Evaluation attributes the discount deterministically. No
money movement, settlement, liability, or ledger entry is created.

## Authorization and audit

Dynamic permissions follow the existing lowercase dotted convention:

- `promotions.promotions.view`
- `promotions.promotions.create`
- `promotions.promotions.update`
- `promotions.promotions.activate`
- `promotions.promotions.suspend`
- `promotions.promotions.archive`
- `promotions.promotions.evaluate`
- `promotions.usage.view`
- `promotions.usage.record`

Platform roles may manage all scopes. Merchant users are checked through
`IMerchantCatalogScopeProvider`; request merchant/branch IDs are never trusted. Catalog
targets are checked through `ICatalogPromotionScopeProvider`. Out-of-scope resources are
concealed where the existing API pattern uses 404.

Sensitive changes create append-only `promotion_audit` records containing only promotion,
actor, action, and timestamp. No token, secret, personal payload, or large request body is
stored.

## API

All endpoints are authenticated Minimal APIs under `/api/v1/promotions`:

- `POST /`, `GET /`, `GET /{id}`, `PUT /{id}`;
- `POST /{id}/activate`, `/suspend`, `/archive`;
- `POST /evaluate`;
- `POST /coupons/validate`;
- `POST /redemptions`;
- `GET /{id}/usage`.

Endpoints contain mapping only, require operation-specific permissions, accept
`CancellationToken`, return contracts rather than EF entities, validate route/body
identity in the service, and map stable errors to Problem Details including a `code`.

## Persistence

`PromotionsDbContext` owns schema `promotions` and migration history
`promotions.__ef_migrations_history`.

Tables:

- `promotions`;
- `promotion_redemptions`;
- `promotion_audit`.

Important safeguards include unique internal name and normalized coupon indexes, status,
type, priority, validity and active lookup indexes, a GIN scope-target index, redemption
promotion/customer indexes, an owning-merchant scope index, external-reference uniqueness,
money/validity/funding/usage check constraints, GUID optimistic concurrency, and `RESTRICT`
foreign keys inside the owned schema. Audit and redemption records are append-only.

## Deferred scope and limitations

- Phase 10 — Carts: actual selection/application and Cart snapshot persistence.
- Phase 11 — Orders: immutable Order snapshot and final redemption coordination.
- Phase 22 — Payments: payment effects.
- Phase 23 — Settlements and Financial Ledger: real funding movements and accounting.
- Outbox/integration events: deferred until a real multi-module reliable flow exists.
- Cart-driven usage consumption and reservation remain deferred; the current administrative
  redemption endpoint is the idempotent persistence foundation.
- No background expiry worker; timestamp validity remains authoritative.

## Commands

```powershell
$project = ".\src\Modules\Promotions\AlSsareea.Modules.Promotions.Infrastructure\AlSsareea.Modules.Promotions.Infrastructure.csproj"
dotnet ef migrations add <Name> --project $project --context PromotionsDbContext --output-dir Persistence\Migrations
dotnet ef migrations has-pending-model-changes --project $project --context PromotionsDbContext
dotnet ef database update --project $project --context PromotionsDbContext
dotnet test tests/AlSsareea.UnitTests/AlSsareea.UnitTests.csproj
dotnet test tests/AlSsareea.ArchitectureTests/AlSsareea.ArchitectureTests.csproj
dotnet test tests/AlSsareea.IntegrationTests/AlSsareea.IntegrationTests.csproj
```

Integration tests use the existing PostgreSQL 17/PostGIS Testcontainer, never EF InMemory
or SQLite, and require Docker.

# Pricing module

Pricing is the Phase 9A owner of delivery and order-fee policy. Its Domain,
Application, Contracts, and Infrastructure projects follow the solution's inward
dependency rules. `PricingDbContext` owns PostgreSQL schema `pricing`, its own migration
history, `pricing_policies`, and `pricing_rules`. It neither reads another module's
database nor creates cross-schema foreign keys.

## Policies, scope, and lifecycle

A policy has a currency, UTC effective range, priority, version, optimistic concurrency
stamp, and one of four scopes. Resolution order is deliberately deterministic:

1. merchant branch;
2. merchant;
3. service zone;
4. global.

Within the most-specific matching scope, the higher policy priority wins. Two matching
policies with the same specificity and priority are rejected as
`pricing.ambiguous_policy`; identifiers are never used to hide ambiguous configuration.
An active policy is effective when `EffectiveFromUtc <= calculation time` and its
exclusive `EffectiveUntilUtc`, when present, is later than the calculation time.

Policies start as `Draft`. Only a draft can change its metadata or replace its complete
rule set. A non-empty draft or inactive policy can be activated, an active policy can be
deactivated, and a non-active policy can be archived. Archived policies cannot be
reactivated. Activation performs an overlap check in a serializable transaction. Every
mutation rotates the concurrency stamp; rule replacement and lifecycle transitions also
advance the policy version used in calculation snapshots.

## Rules and calculation

Amounts use signed 64-bit integer minor currency units; floating-point money is not used.
Percentages use integer basis points from 0 through 10,000 and round half up to the nearest
minor unit. Arithmetic is checked, and invalid negative values, inverted ranges, invalid
currencies, and overflow are rejected.

The supported rule types are fixed delivery, distance delivery, zone delivery, service
fee, platform fee, small-order fee, minimum order, and tax. A rule can be disabled, fixed,
or percentage-based and can carry explicit minimum and maximum caps. Percentage bases are
items subtotal, items subtotal plus delivery, or the accumulated pre-tax total. Distance
delivery supports an included distance, an optional maximum distance, and a fee for each
started kilometre above the included distance. Exactly one applicable delivery rule is
selected by rule priority; equal top priorities are invalid.

The calculation order is minimum-order eligibility, delivery, service fee, platform fee,
small-order fee, and tax. The response always separates subtotal, delivery, service,
platform, small-order, tax, discounts, and grand total. Discounts are zero in Phase 9A;
promotions remain a later concern. An unmet minimum produces an ineligible response with
`pricing.minimum_order_not_met` rather than silently changing the order amount.

Every successful calculation includes an immutable value snapshot containing the selected
policy ID and version, effective scope, applied rule IDs, input references and distance,
calculation timestamp, full breakdown, and eligibility result. Downstream modules should
persist the snapshot they receive instead of recalculating historical orders from mutable
policies.

## Authorization and module boundaries

Policy endpoints require `pricing.view` or `pricing.manage`; estimates require
`pricing.calculate`. Rate limits are separated into read, write, and calculate policies.
Merchant and branch access is checked through
`IMerchantCatalogScopeProvider` from Merchants Contracts. Pricing does not reference
Merchants Infrastructure. Service-zone IDs are stable scalar inputs in Phase 9A; no Maps
database access or cross-schema foreign key is introduced.

The estimate contract accepts a trusted items subtotal because Pricing owns fee
calculation, not product prices. Catalog remains authoritative for product base prices;
public clients must not treat a caller-supplied subtotal as authoritative. Future Carts
will validate items and prices through Catalog before calling Pricing. Orders will copy
the returned pricing snapshot, and Payments will consume the approved Order total instead
of independently recalculating it.

## API

All endpoints are under `/api/v1/pricing`:

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/estimates` | Resolve a policy and calculate a complete estimate and snapshot |
| `POST` | `/policies` | Create a draft policy |
| `GET` | `/policies` | List policies with bounded pagination and optional filters |
| `GET` | `/policies/{policyId}` | Read one policy |
| `PUT` | `/policies/{policyId}` | Update draft metadata |
| `PUT` | `/policies/{policyId}/rules` | Atomically replace a draft rule set |
| `POST` | `/policies/{policyId}/activate` | Activate a configured policy |
| `POST` | `/policies/{policyId}/deactivate` | Deactivate an active policy |
| `POST` | `/policies/{policyId}/archive` | Archive a non-active policy |

Validation, scope, concurrency, ambiguity, currency, distance, and not-found failures use
Problem Details with a stable `code` extension from `PricingErrorCodes`.

## Configuration, migrations, and verification

Configure `ConnectionStrings:PricingDatabase`. The checked-in Development value matches
the local Compose database; production must inject its value through configuration or a
secret manager.

```powershell
$pricingProject = ".\src\Modules\Pricing\AlSsareea.Modules.Pricing.Infrastructure\AlSsareea.Modules.Pricing.Infrastructure.csproj"
dotnet ef database update --project $pricingProject --context PricingDbContext
dotnet ef migrations has-pending-model-changes --project $pricingProject --context PricingDbContext
dotnet test tests/AlSsareea.UnitTests/AlSsareea.UnitTests.csproj
dotnet test tests/AlSsareea.ArchitectureTests/AlSsareea.ArchitectureTests.csproj
dotnet test tests/AlSsareea.IntegrationTests/AlSsareea.IntegrationTests.csproj
```

Phase 9A intentionally excludes promotions, coupons, surge pricing, external tax engines,
historical currency conversion, and cross-policy rule composition. Those capabilities
belong in later phases and must retain deterministic snapshots and module boundaries.
Configured tax and statutory-charge rules require legal and accounting review and are not
legal or accounting advice. Activation is request-driven in this phase; there is no
background activation scheduler. No commit or push was performed as part of the Phase 9A
implementation.

# Merchants module

Phase 6 introduces merchant administration and operations without adding Catalog behavior.

## Boundaries and aggregates

- `Merchant` owns legal/profile data, lifecycle, and the current owner identifier.
- `MerchantBranch` is separate because branch location, lifecycle, hours, overrides, and service areas change independently.
- `MerchantEmployee` is separate for merchant-scoped membership lifecycle and optimistic concurrency.
- Merchants references Identity and Maps only through `IIdentityUserLookup` and `IMapsModule`; it never references their Infrastructure assemblies or creates cross-schema foreign keys.

## Lifecycles and ownership

Merchants start `PendingApproval` and may become `Active`, `Suspended`, `Rejected`, or terminally `Closed`. Suspension, rejection, and closing require reasons.

Branches start `Draft` and may become `Active`, `TemporarilyClosed`, `Suspended`, or terminally `Closed`. Activation and reopening require an active merchant. The application maintains a primary branch and PostgreSQL enforces at most one with a partial unique index.

Memberships are `Invited`, `Active`, `Suspended`, or `Removed`, with roles `Owner`, `Manager`, `BranchManager`, and `Employee`. Ownership transfer updates the merchant and both memberships in one transaction. Employee endpoints cannot remove, suspend, or downgrade the active owner.

## Authorization

Endpoints reuse the dynamic `Permission:` policy provider. Application services additionally require an active membership in the route merchant, constrain branch-scoped memberships to their branch, query merchant/branch route pairs together, deny suspended/removed memberships, and conceal cross-scope resources as not found. Merchant activation, suspension, rejection, and closure are platform-only.

Permissions are:

- `merchants.merchants.view`
- `merchants.merchants.create`
- `merchants.merchants.update`
- `merchants.lifecycle.manage`
- `merchants.branches.view`
- `merchants.branches.manage`
- `merchants.business-hours.manage`
- `merchants.service-areas.manage`
- `merchants.employees.view`
- `merchants.employees.manage`

## Hours and availability

Weekly schedules are relational and contain all seven local days. Open days require non-overlapping periods. Overnight periods are deliberately not supported in Phase 6. Branches store IANA-compatible time-zone identifiers.

Availability accepts UTC, converts to local branch time, then applies full-day exceptional closures, date-specific special hours, and finally weekly hours. Historical overrides remain stored; only a current or future override can be cancelled.

## Persistence

`MerchantsDbContext` owns the `merchants` schema and these tables:

- `merchants`
- `merchant_branches`
- `merchant_business_hours`
- `merchant_business_hour_periods`
- `merchant_branch_schedule_overrides`
- `merchant_branch_special_hour_periods`
- `merchant_branch_service_areas`
- `merchant_employees`

Branch coordinates use `geometry(Point,4326)` with a GIST index. Service-area polygons remain in Maps; Merchants stores only service-area IDs. The migration is `20260725102407_AddMerchantsModule`.

## API and deferred scope

Authenticated endpoints under `/api/v1/merchants` cover merchant lifecycle, branches, business hours, overrides, availability, service areas, employees, and ownership. API responses use contracts and failures use Problem Details.

Categories, products, menus, variants, modifiers, catalog pricing, product availability, inventory, and catalog search remain deferred to Phase 7.

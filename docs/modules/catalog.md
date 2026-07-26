# Catalog module

Catalog is the Phase 7 owner of the customer-visible merchandise model. Its four projects
follow the repository's Domain, Application, Contracts, and Infrastructure dependency
direction. `CatalogDbContext` owns PostgreSQL schema `catalog` and its independent
`catalog.__ef_migrations_history` history table.

## Boundary and aggregates

`Catalog` is the merchant-level lifecycle root (`Draft`, `Active`, `Suspended`, `Archived`).
`Category` owns its localized label and optional hierarchy link. `MenuSection` owns localized
display groupings and ordered product links. `Product` is a separate aggregate because it is
independently versioned and will be referenced by future carts and orders. It owns
translations, variants, option groups and options, image references, and local-time
availability schedules.

Catalog stores merchant and branch identifiers as scalar references. It has no cross-schema
foreign keys and never consumes `MerchantsDbContext`; merchant existence, management
authority, and branch scope are obtained through `IMerchantCatalogScopeProvider` in
Merchants Contracts.

## Product behavior

Lifecycle, visibility, and inventory are deliberately independent. Only an active, visible
product in `InStock` or `LowStock` state is purchasable. `OutOfStock` and `Unavailable`
preserve the product while preventing purchase. Commercial or presentation changes advance
`CurrentVersion` and rotate the optimistic concurrency stamp.

A variant represents one sellable form or SKU and contributes a signed price adjustment.
An option represents a selection inside a required or optional, single- or multiple-choice
group. Group minimums and maximums are validated. The authoritative price is:

`base price + selected variant adjustment + selected option adjustments`

The server rejects unavailable or foreign selections, duplicate option identifiers,
selection-limit violations, and a negative total. Client-supplied totals are never accepted.

## Localization, search, and availability

Translations support Arabic (`ar`), Hebrew (`he`), and English (`en`) with the catalog's
default language as fallback. Translation keys are unique per owner and language. Normalized
search text, merchant/status filters, stable sorting, and pagination are executed by
PostgreSQL.

Availability periods use a named time zone, weekday, and local start/end. End times at or
before the start represent an overnight period. Optional branch identifiers narrow a
schedule without introducing a database dependency on Merchants.

## Images, snapshots, permissions, and deferred work

Images are references only (`MediaId` or external reference); a partial unique index permits
one primary reference per product. `IProductSnapshotProvider` captures product version,
localized names, selected variant/options, tax-category reference, and the server-calculated
minor-unit total for future order immutability.

Management endpoints use dynamic `catalog.*` permissions and merchant scope validation.
Public endpoints expose only active, visible catalog data. Media upload and processing are
Phase 8; pricing/promotions are Phase 9; carts are Phase 10; orders are Phase 11. Inventory
quantities/reservations, tax calculation, payments, delivery, and UI remain outside Phase 7.

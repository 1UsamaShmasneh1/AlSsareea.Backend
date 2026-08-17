# Architecture overview

AlSsareea uses a modular monolith. The API is the composition root; modules own their domain, application, infrastructure, and public contracts. Building blocks contain small cross-cutting abstractions that are genuinely shared.

Dependencies point inward: Infrastructure may use Application and Domain; Application may use Domain; Domain remains framework-neutral. Cross-module integrations use Contracts and never another module's Infrastructure assembly. Architecture tests enforce the initial dependency rules.

Business endpoints added later should be grouped under `/api/v1`. Operational system endpoints remain under `/health` and `/api/system`.

The Maps module owns the reusable geospatial foundation: service areas, spatial queries,
provider-neutral geocoding, reverse geocoding, places, and routing contracts. Other
modules consume `AlSsareea.Modules.Maps.Contracts`; they do not reference Maps
Infrastructure or its DbContext. Customer and merchant addresses, delivery, dispatching,
pricing, drivers, and live tracking remain outside Maps.

Phase 4 adds the [Customers module](customers.md), which owns business profiles and commercial state while retaining a scalar-only boundary to Identity.

Phase 6 adds the [Merchants module](merchants.md), which owns merchant and branch
lifecycles, scoped memberships, relational schedules, branch locations, and service-area
assignments. It validates Identity users and Maps service areas through public contracts
without cross-module Infrastructure references or cross-schema foreign keys.

Phase 7 adds the [Catalog module](../modules/catalog.md). Catalog owns merchant catalogs,
categories, menu sections, localized products, variants, selectable options, image
references, availability schedules, inventory presentation state, price composition, and
immutable product snapshots. It asks Merchants for scope through a Contracts interface and
does not read the Merchants schema or reference Merchants Infrastructure.

Phase 8 adds the [Media module](../modules/media.md). Media owns image validation,
local-provider storage, metadata, processing variants, ownership, access level, lifecycle,
and cleanup. Catalog validates media references through `IMediaAssetLookup`; neither module
reads the other's schema, and no cross-schema foreign key is created.

Phase 9A adds the [Pricing module](../modules/pricing.md). Pricing owns scoped and
effective-dated pricing policies, rule lifecycle, deterministic integer-minor-unit fee
calculation, and immutable calculation snapshots. It consumes merchant scope through
Merchants Contracts, treats service-zone identifiers as scalar references, and owns the
`pricing` schema without cross-schema foreign keys.

Phase 9B adds the [Promotions module](promotions.md), which owns promotion definitions,
coupon normalization, lifecycle, deterministic eligibility and conflict evaluation,
funding attribution, stable snapshots, and append-only usage/audit records. It consumes
Pricing breakdown contracts and validates merchant/catalog scope through public module
contracts without accessing another module's storage.

Phase 10 adds the [Carts module](../modules/carts/README.md). Carts owns pre-order state
and composes Customers, Merchants, Catalog, Pricing, and Promotions only through public
contracts. Its checkout summary is recalculated and is not an order or payment boundary.

Phase 11 adds the [Orders module](../../src/Modules/Orders/README.md). Orders turns a
trusted, freshly validated checkout summary into immutable relational snapshots, owns the
explicit order lifecycle and append-only history, and atomically persists creation
idempotency and integration events in its own outbox. Cart conversion is idempotent and no
distributed transaction or cross-schema foreign key is used.

Phase 13 adds the [Drivers module](../modules/drivers/README.md). Drivers owns the permanent
commercial and operational driver profile in the `drivers` schema. `DriverId` is independent
from Identity `UserId`; Identity, Maps service areas, and Media assets are consumed through
public contracts only. Tracking, dispatching, delivery, and financial ledgers remain separate.

Phase 14 adds the [Tracking module](../modules/tracking/README.md). Tracking owns immutable
GPS history and a concurrency-safe latest projection in PostGIS. Drivers eligibility is consumed
through Contracts, SignalR remains an ephemeral post-commit channel, and customer visibility is
order-context-only behind a deny-by-default contract pending Delivery in Phase 15.

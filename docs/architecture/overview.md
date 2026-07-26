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

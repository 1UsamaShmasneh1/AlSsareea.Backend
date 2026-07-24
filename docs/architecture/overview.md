# Architecture overview

AlSsareea uses a modular monolith. The API is the composition root; modules own their domain, application, infrastructure, and public contracts. Building blocks contain small cross-cutting abstractions that are genuinely shared.

Dependencies point inward: Infrastructure may use Application and Domain; Application may use Domain; Domain remains framework-neutral. Cross-module integrations use Contracts and never another module's Infrastructure assembly. Architecture tests enforce the initial dependency rules.

Business endpoints added later should be grouped under `/api/v1`. Operational system endpoints remain under `/health` and `/api/system`.

The Maps module owns the reusable geospatial foundation: service areas, spatial queries,
provider-neutral geocoding, reverse geocoding, places, and routing contracts. Other
modules consume `AlSsareea.Modules.Maps.Contracts`; they do not reference Maps
Infrastructure or its DbContext. Customer and merchant addresses, delivery, dispatching,
pricing, drivers, and live tracking remain outside Maps.

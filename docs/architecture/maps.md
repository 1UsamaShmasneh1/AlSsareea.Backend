# Maps and geospatial foundation

## Responsibilities and boundaries

Maps owns geospatial abstractions, service-area boundaries and spatial queries, geocoding,
reverse geocoding, places, and routing provider abstractions. Customers and Merchants will
own their addresses. Delivery and Dispatching may consume Maps contracts but must not
access Maps persistence.

The public Contracts project exposes provider-neutral request and response records,
provider interfaces, and `IMapsModule` for service-area queries. Provider SDK types and
database geometry types must never cross this boundary.

## Coordinate standard

All coordinates use EPSG:4326. Application and module contracts use the unambiguous
`Latitude` then `Longitude` naming. NetTopologySuite and PostGIS points use:

- `X = Longitude`
- `Y = Latitude`
- `SRID = 4326`

`GeoPoint.Create` rejects non-finite numbers and coordinates outside latitude `-90..90`
or longitude `-180..180`. Service-area boundaries must be non-empty, valid
`MultiPolygon` values with SRID 4326. Point coverage uses `ST_Covers`, so a point on a
polygon edge is included.

## Persistence

Maps data is stored in PostgreSQL under the `maps` schema. The initial migration enables
PostGIS, creates `maps.service_areas`, persists the boundary as
`geometry(MultiPolygon,4326)`, and creates a GiST spatial index. `MapsDbContext` remains
internal to Infrastructure. Other modules use Contracts.

Configure the database through the `ConnectionStrings:MapsDatabase` configuration key.
No connection string is stored in source control.

## Providers

`Maps:Provider` selects the provider. `Fake` is the only supported value in this phase.
The fake provider performs no HTTP calls and supplies deterministic geocoding, reverse
geocoding, autocomplete, place details, and Haversine-based route results. Its
`FailAllRequests` option exists to exercise provider failure handling.

To add a provider:

1. Implement the provider-neutral interfaces in Maps Infrastructure.
2. Keep all provider SDK models and mapping code in Infrastructure.
3. Add a provider selection value and register the implementation in Maps Infrastructure.
4. Do not change Domain, Application, or public contracts unless the provider reveals a
   genuinely provider-neutral platform capability.
5. Add contract-mapping, cancellation, failure, and integration tests.

## Testing strategy

Unit tests cover coordinate validation and ordering, service-area behavior and boundary
validation, edge coverage, and deterministic fake-provider behavior. Architecture tests
enforce inward dependencies, neutral contracts, and provider placement. Integration tests
run the real migration against a PostGIS Testcontainer and verify extension installation,
schema/table ownership, geometry persistence, inside/outside/edge queries, and the GiST
index.

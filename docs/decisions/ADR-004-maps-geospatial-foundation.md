# ADR-004: Maps geospatial foundation

- Status: Accepted
- Date: 2026-07-24

## Context

Multiple future modules need coordinates and map-provider capabilities without coupling
domain logic to a commercial provider or sharing persistence.

## Decision

Create Maps as a standard modular-monolith module with Domain, Application, Contracts,
and Infrastructure projects. Use EPSG:4326 and PostGIS `geometry(MultiPolygon,4326)` for
service-area boundaries. Convert points as longitude/X and latitude/Y. Use `ST_Covers`
for edge-inclusive service-area queries and a GiST boundary index.

Expose provider-neutral contracts and keep implementations in Infrastructure. Provide a
deterministic fake provider as the only configured provider in this phase. Keep
`MapsDbContext` internal and require all cross-module use to go through Contracts.

## Consequences

Future provider integrations can be added without changing Domain or Application.
PostgreSQL deployments must provide PostGIS and apply the Maps migrations. Integration
tests require a Docker-compatible runtime to start the PostGIS Testcontainer.

# ADR-008: Tracking location storage and realtime delivery

## Status

Accepted for Phase 14.

## Decision

Tracking exclusively owns driver telemetry, immutable history, and latest projection. Drivers stores no GPS data. PostgreSQL/PostGIS with SRID 4326 is the source of truth; a separate latest table avoids a history scan on every update and uses a conditional upsert to withstand concurrent and out-of-order writes.

Retries and offline batches use a DriverId-scoped monotonic sequence with a database unique constraint. SignalR publishes small post-commit updates and clients resynchronize from REST after reconnect. SignalR is not durable. Tracking creates no outbox or audit record per GPS point and adds neither Redis nor a message broker.

Customer visibility is an explicit `ITrackingVisibilityProvider` boundary keyed by order context, never arbitrary DriverId. It denies by default until Phase 15 supplies delivery and assignment knowledge. Retention is configurable, while automated deletion waits for an approved jobs mechanism.

## Consequences

The ingestion path has one history insert and at most one conditional latest upsert. PostgreSQL remains authoritative and operations can query bounded history. Independent simultaneous device streams are not supported until a stable stream identifier is introduced. Realtime delivery may be missed, so consumers must use REST resynchronization.

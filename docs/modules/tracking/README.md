# Tracking module

Tracking owns high-frequency driver GPS telemetry. Drivers continues to own driver profiles and operational eligibility; Tracking owns immutable location history, one latest-location projection per driver, ingestion metadata, and realtime hints.

## Ingestion and validation

Authenticated drivers submit only their own telemetry through `POST /api/v1/tracking/location` or a bounded offline batch through `POST /api/v1/tracking/locations/batch`. The API never accepts a driver or user identifier in either body. `IDriverOperationalSnapshotProvider` resolves the current Identity user to the Drivers-owned identifier and eligibility snapshot.

Coordinates must be finite and within WGS84 bounds. Recorded timestamps must be UTC, no more than 30 seconds in the future, and within the 24-hour offline synchronization window. Accuracy must be positive; readings worse than 250 metres are retained as history but cannot become latest. Speed is optional and non-negative; heading is optional in `[0, 360)`. A configurable Haversine-based guard subtracts both readings' accuracy radii before comparing implied speed with the default 75 m/s limit. Implausible readings remain in history and do not poison latest.

Defaults under `Tracking` are: live staleness 300 seconds, maximum batch size 200, history query range 24 hours, page size 100, and recommended intervals of 120 seconds offline, 30 seconds idle/online, and 10 seconds busy.

## Sequence, duplicates, offline sync, and concurrency

Phase 14 scopes sequence numbers by `DriverId` because there is no stable Tracking device/session contract. `(driver_id, sequence_number)` is unique, making retries naturally idempotent without high-volume idempotency rows. This assumes one monotonically increasing logical stream per driver; a future multi-device contract must add an explicit stream identifier before clients may emit independent sequences.

Accepted readings are inserted into `tracking.driver_locations`. Latest is maintained separately in `tracking.driver_latest_locations` by a PostgreSQL conditional upsert. Sequence is authoritative; recorded time breaks equal-sequence ties. Older offline readings can be retained without moving latest backward. History insertion and conditional latest promotion share one transaction. There is no generic repository, history scan, audit row, outbox row, Redis write, or broker message per ping.

## Persistence and retention

`TrackingDbContext` owns schema `tracking` and `tracking.__ef_migrations_history`. Positions use PostGIS `geometry(Point,4326)`. Both tables have GiST position indexes; history also has `(driver_id, recorded_at_utc DESC)`, timestamp, and unique sequence indexes. There are no cross-schema foreign keys or cascade paths.

The configured retention horizon is 30 days. Automated deletion is intentionally deferred because the repository has no approved background-jobs foundation.

## API, permissions, and rate limits

- `POST /api/v1/tracking/location` — `tracking.locations.update.self`
- `POST /api/v1/tracking/locations/batch` — `tracking.locations.update.self`
- `GET /api/v1/tracking/me/latest` — driver self resynchronization
- `GET /api/v1/tracking/drivers/{driverId}/latest` — `tracking.locations.read`
- `GET /api/v1/tracking/drivers/{driverId}/history` — `tracking.locations.read.history`, bounded UTC range and pagination
- `GET /api/v1/tracking/orders/{orderId}/latest` — authenticated order context only

Ingestion uses the existing fixed-window limiter, partitioned by authenticated subject plus device and network metadata. The default is 240 permits per minute. Batch cardinality is independently capped at 200.

## Realtime and privacy

The authenticated hub is `/hubs/tracking`. Clients can call only `SubscribeSelf`, `SubscribeOperations`, or `SubscribeOrder`; there is no arbitrary group-name method. Operations needs `tracking.realtime.operations`; self is resolved through Drivers; order subscriptions pass through `ITrackingVisibilityProvider`. Payloads omit received time, sequence, batch metadata, concurrency stamps, and history.

Broadcast occurs only after the database transaction commits. SignalR is an ephemeral hint, never the source of truth. After reconnect, clients fetch REST latest and then resubscribe.

Customer visibility never accepts a driver identifier. The default provider still denies all access
when Tracking is composed alone. In the API composition root, Delivery replaces it with an
order-context provider that requires the authenticated customer owner, an assigned driver, and a
visible delivery state (`PickedUp`, `InTransit`, or `ArrivedAtDropOff`). Terminal, pre-pickup, and
unassigned deliveries are not visible. Tracking still reads no Delivery tables directly.

## Testing and migrations

Domain/application tests cover validation, timestamps, staleness, offline windows, duplicates, movement plausibility, latest promotion, batch limits, eligibility, and adaptive intervals. Architecture tests enforce boundaries. Integration tests use the pinned PostGIS Testcontainers image for migrations, schema isolation, spatial storage, constraints, idempotency, concurrency, pagination, and authorization metadata.

```powershell
dotnet ef migrations has-pending-model-changes --project src/Modules/Tracking/AlSsareea.Modules.Tracking.Infrastructure/AlSsareea.Modules.Tracking.Infrastructure.csproj --startup-project src/Modules/Tracking/AlSsareea.Modules.Tracking.Infrastructure/AlSsareea.Modules.Tracking.Infrastructure.csproj --context TrackingDbContext
```

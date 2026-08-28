# Dispatching module

Dispatching owns driver selection, candidate decision snapshots, offer lifecycle, retry history,
and emergency manual assignment. Delivery remains the owner of pickup, transit, proof, completion,
and failure execution. Dispatching stores scalar identifiers only and never reads another schema.

## Selection policy

Drivers supplies discovery and authoritative operational eligibility through
`IDriverDispatchCandidateProvider`; inactive, unapproved, suspended, offline/unavailable,
out-of-zone, vehicle-mismatched, and capacity-exhausted drivers are hard-filtered before scoring.
Tracking supplies the latest location through `IDispatchLocationProvider`; locations older than
`MaximumLocationStalenessSeconds` or less accurate than the configured threshold are excluded.
Maps `IRoutingProvider` supplies distance and ETA.

The initial deterministic score is a weighted normalized sum: distance 35%, ETA 30%, remaining
capacity 20%, time since last assignment 10%, and optional merchant preparation alignment 5%.
Ties resolve by distance and then `DriverId`, making identical inputs stable. Drivers currently has
no authoritative last-assignment projection, so that input remains null and receives the neutral
never-assigned fairness value. Preparation timing is optional and contributes zero until supplied
from a trusted caller/source.

## Offers and concurrency

Phase 16 implements sequential offers only. At most one pending offer exists; decline or expiry
advances to the next ranked candidate, and a candidate is not re-offered in the same attempt.
The aggregate rejects acceptance at or after `ExpiresAtUtc` using `IClock`, independently of the
15-second expiration worker. The request and offer use GUID optimistic-concurrency tokens. A unique
dispatch-per-delivery index, one transaction for state/idempotency/audit/outbox, and Delivery's
idempotent assignment contract ensure one logical winner.

Batch offers and zone broadcast were not added because no existing product-plan contract requires
them; the sequential path is the supported default. Retry is bounded by `MaximumAttempts`. Manual
assignment may bypass ranking but still requires an active, approved, unsuspended, online/available,
zone- and vehicle-compatible driver with remaining capacity, a dedicated permission, actor, reason,
and immutable history.

## Persistence and API

`DispatchingDbContext` owns schema `dispatching`, `dispatching.__ef_migrations_history`, requests,
candidates, offers, history, idempotency, audit, and outbox tables. Candidate/history/audit/outbox/
idempotency rows are append-only. Foreign keys are internal to the schema; cross-module identifiers
are scalar values.

Routes live under `/api/v1/dispatching`. Start/read/retry/cancel/manual operations use dedicated
permissions. Offer accept and decline rely on authenticated-driver ownership resolution and return
not found for another driver's offer, preventing IDOR disclosure. All mutations require an
`Idempotency-Key`; failures use the API's Problem Details shape.

## Migration check

```powershell
dotnet ef migrations has-pending-model-changes --project src/Modules/Dispatching/AlSsareea.Modules.Dispatching.Infrastructure/AlSsareea.Modules.Dispatching.Infrastructure.csproj --context DispatchingDbContext
```

Excluded scope includes Notifications, push/SMS/email providers, Redis, brokers, payments,
settlements, UI applications, GPS ingestion/storage, route optimization, multi-order batching,
and machine-learning ranking.

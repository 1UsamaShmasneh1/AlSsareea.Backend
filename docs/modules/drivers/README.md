# Drivers module

Phase 13 introduces the permanent commercial and operational driver profile. The module owns driver activation, vehicles, document metadata and review, service-area assignments, availability, shifts, violations, suspensions, capacity, audit records, idempotency records, and integration outbox messages.

## Architecture and ownership

`Driver` is the only aggregate root. `Vehicle`, `DriverDocument`, `DriverZoneAssignment`, `DriverShift`, `DriverViolation`, and `DriverSuspension` are lifecycle-controlled child entities and do not have repositories. `DriverId` is independent from the Identity `UserId`; the database has no cross-schema foreign keys or joins. Identity, Media, and Maps are accessed only through their public contracts.

The module has Domain, Application, Contracts, and Infrastructure projects. Its `DriversDbContext` owns the `drivers` schema and `drivers.__ef_migrations_history`. Deletes use `NoAction`; historical records are retained. The module uses archived status rather than a global soft-delete filter.

## Lifecycles

- Activation: `NotSubmitted -> PendingReview -> Approved/Rejected`; an approved profile can then become operationally active.
- Availability: `Offline -> Online -> OnBreak/Busy -> Online/Offline`. `DriverEligibilityPolicy` is the single online-eligibility authority. It requires an operationally active, approved driver, exactly one active primary vehicle, an active service area, approved unexpired documents, and no active suspension. Document expiry is exclusive (`ExpiresAtUtc > now`), so a document expiring exactly at `now` is expired.
- Vehicle document requirements are selected explicitly by `VehicleType`. The current business rule intentionally requires driving licence, vehicle registration, and vehicle insurance for Bicycle, Motorcycle, Car, Van, and Truck; the per-type policy is the extension point for a future approved rule change.
- Vehicle: pending verification, active, inactive, rejected, expired, or retired. PostgreSQL enforces unique current plates and one active primary vehicle per driver.
- Document: pending review, approved, rejected, expired, or replaced. Replacing a current document preserves the old record.
- Shift: scheduled, started, completed, cancelled, or missed. Phase 13 uses explicit shifts and does not introduce a second recurring working-hours model.
- Violations and suspensions are historical. Active suspension is derived from `StartsAtUtc <= now`, `EndsAtUtc is null || EndsAtUtc > now`, and `LiftedAtUtc is null`; stored status is not the eligibility source. A future suspension does not force the driver offline early, and a time-expired suspension stops blocking online availability without a worker updating status.

## Persistence and integration

Migration `InitializeDriversModule` creates `drivers`, `vehicles`, `driver_documents`, `driver_zone_assignments`, `driver_shifts`, `driver_violations`, `driver_suspensions`, `audit_records`, `idempotency_records`, and `outbox_messages`. Constraints cover capacity/load, dates, enum domains, document review requirements, and uniqueness. Optimistic concurrency uses `ConcurrencyStamp` on mutable profile, vehicle, document, and shift data.

Audit, idempotency completion, domain mutation, and outbox insertion share one unit of work. Idempotency scope is actor + operation + hashed key, request fingerprints use canonical JSON and SHA-256, and successful response JSON/status are retained for exact replay; no headers, tokens, secrets, or document bytes are stored. Concurrent duplicates resolve through the unique scope after rollback. Business changes always produce a safe integration event, while repeated online/offline no-ops persist replay metadata only and do not create audit/outbox records or change concurrency stamps. Outbox IDs use event IDs and PostgreSQL requires every payload to be a JSON object, matching Orders. Documents store Media asset IDs, never file bytes or storage paths. `IDriverEligibilityProvider` and `IDriverOperationalSnapshotProvider` export bounded operational snapshots.

## API, permissions, and security

Endpoints are under `/api/v1/drivers`. Self-service endpoints derive `UserId` from the authenticated principal. Shift scheduling/cancellation and administrative reads use `drivers.shifts.manage` / `drivers.shifts.read`; owned reads and start/complete operations use `drivers.shifts.read.self` / `drivers.shifts.manage.self` under `/drivers/me/shifts`. Self routes accept no owner identifier and conceal another driver's shift as not found. Other administrative review, activation, vehicle verification, document review, zone assignment, violations, and suspensions each require dedicated dynamic permissions. Mutating endpoints require an `Idempotency-Key`, use Problem Details, and return conflicts for stale concurrency stamps.

PII is minimized: Identity remains the source of authentication and contact information, and Media remains the source of document files. The module does not store passwords, tokens, full identity-document numbers, or file contents.

## Testing and limitations

Unit tests exercise IDs, lifecycles, eligibility, capacity, vehicles, documents, zones, shifts, violations, and suspensions. Integration tests apply real migrations to PostgreSQL/PostGIS through Testcontainers and validate schema ownership, model synchronization, and uniqueness. Architecture tests enforce dependency direction and prevent API access to `DriversDbContext`.

Phase 13 does not implement tracking, GPS ingestion, live locations, SignalR location broadcasting, dispatching, driver offers, assignments, delivery workflow, pickup, proof of delivery, earnings calculation, settlements, payments, notification delivery, or a driver application. Automated shift scheduling and recurring working-hours policies remain future work.

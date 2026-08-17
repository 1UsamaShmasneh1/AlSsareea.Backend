# Delivery module

Delivery owns the operational execution of a ready order from creation and driver assignment
through pickup, transit, drop-off, completion, failure, or cancellation. It does not own order
commercial state, driver profiles or dispatching, media bytes, customer profiles, GPS telemetry,
payments, settlements, or financial ledgers.

## Aggregate and lifecycle

`Delivery` is the aggregate root and uses a strongly typed `DeliveryId`. Exactly one delivery may
reference an order. Creation copies trusted order/customer data into immutable pickup and drop-off
snapshots. The normal lifecycle is:

`Created -> Assigned -> HeadingToPickup -> ArrivedAtPickup -> PickedUp -> InTransit -> ArrivedAtDropOff -> Delivered`

Invalid skips and repeated transitions are rejected. `Failed` and `Cancelled` are terminal.
Assignment and each status change update a GUID concurrency token and append an immutable status
history row with its UTC timestamp. Failure uses a controlled reason plus bounded optional notes.

## Proof policy and PIN safety

Proof requirements are fixed when the delivery is created and may require any combination of PIN,
photo, signature, and recipient name. Completion succeeds only when every configured proof is
present. Photo and signature proofs store only a validated, ready Media asset identifier. Recipient
names are trimmed and bounded.

PINs contain six cryptographically random digits and are returned only once from the privileged
creation operation. The database stores a PBKDF2-SHA256 hash and random salt, never the plaintext
PIN or submitted candidates. Comparison is constant-time. Failed attempts are counted and the PIN
locks after five failures; the API never logs or echoes the candidate.

## Persistence and reliability

`DeliveryDbContext` owns schema `delivery` and `delivery.__ef_migrations_history`. It owns
`deliveries`, `delivery_status_history`, `delivery_proofs`, operation-idempotency, audit, and outbox
tables. Status history, proofs, audit, and outbox rows are append-only. Foreign keys stay within the
schema, use restrictive deletion, and never point at another module. Mutations atomically persist
the aggregate, idempotency record, audit fact, and integration event. Expected concurrency conflicts
produce an explicit conflict result.

## Module integrations

- Orders supplies an eligible immutable order snapshot through `IDeliveryOrderSnapshotProvider`.
- Customers maps the scalar `CustomerId` to its Identity `UserId` for ownership checks.
- Drivers validates assignment eligibility and resolves an authenticated driver to `DriverId`.
- Media validates proof asset references; Delivery stores identifiers only.
- Tracking remains the only owner of locations and SignalR. Delivery implements its visibility
  contract without exposing a driver identifier to callers.

There is no cross-module Infrastructure reference, cross-schema foreign key, distributed
transaction, dispatch algorithm, delivery-location table, or second realtime hub. Delivery emits
created, assigned, status-changed, completed, and failed integration events into its own outbox.

## API and authorization

Routes are under `/api/v1/deliveries`. Management creates and assigns deliveries. A driver may read
or operate only the delivery resolved from their authenticated Identity user. A customer may read
only a delivery whose copied customer user identifier matches their subject. Cross-tenant misses
return not found. Mutating routes require `Idempotency-Key` and a current concurrency stamp.

Permissions are `delivery.deliveries.manage`, `delivery.deliveries.read.own`,
`delivery.deliveries.read.self`, `delivery.deliveries.operate.self`, and
`delivery.deliveries.read.all`.

## Migration check

```powershell
dotnet ef migrations has-pending-model-changes --project src/Modules/Delivery/AlSsareea.Modules.Delivery.Infrastructure/AlSsareea.Modules.Delivery.Infrastructure.csproj --startup-project src/Modules/Delivery/AlSsareea.Modules.Delivery.Infrastructure/AlSsareea.Modules.Delivery.Infrastructure.csproj --context DeliveryDbContext
```

# Orders module

Orders is the durable source of truth for an order after checkout. It owns immutable business snapshots, lifecycle state, status history, creation idempotency, optimistic concurrency, and an orders-owned transactional outbox. It never reads another module's schema or DbContext.

## Architecture and contracts

The module has Domain, Application, Contracts, and Infrastructure projects. Application consumes only narrow contracts:

- `IOrderCheckoutProvider` asks Carts to perform final ownership, expiry, merchant/branch, Catalog, Pricing, and Promotions validation and return a trusted checkout snapshot.
- `IOrderCustomerSnapshotProvider` resolves the authenticated customer's selected address.
- `IOrderMerchantSnapshotProvider` resolves the operational merchant/branch execution snapshot.

Carts marks the cart converted idempotently after the Orders transaction commits. This is deliberately not a distributed transaction. A unique `source_cart_id`, the creation-idempotency constraint, and an idempotent cart conversion make retries safe if that final call fails.

## Creation and snapshots

The client supplies only cart/address references, order type, optional UTC schedule, notes, and an optional expected cart version. Customer identity comes from the authenticated user. Prices, product text, options, promotions, customer/address data, and merchant/branch data come from backend contracts and are copied into Orders. Changes in source modules do not mutate an existing order.

New orders start in `PendingPayment`. This preserves the future separation between order state and payment state; Phase 11 does not authorize or capture payments.

The public order number is the uppercase 32-character GUID representation of `OrderId`. It is immutable, contains no timestamp or personal data, is concurrency-safe, and has a unique database index.

## Lifecycle

| Current | Allowed next states |
|---|---|
| Draft | PendingPayment, Submitted |
| PendingPayment | PaymentAuthorized, Cancelled, Failed |
| PaymentAuthorized | Submitted, Cancelled, Failed |
| Submitted | AcceptedByMerchant, RejectedByMerchant, Cancelled |
| AcceptedByMerchant | Preparing, Cancelled |
| Preparing | ReadyForPickup, Cancelled |
| ReadyForPickup | SearchingForDriver, DriverAssigned, Cancelled |
| SearchingForDriver | DriverAssigned, Cancelled, Failed |
| DriverAssigned | DriverArrivingToPickup, SearchingForDriver, Cancelled |
| DriverArrivingToPickup | PickedUp, SearchingForDriver, Cancelled |
| PickedUp | OnTheWay |
| OnTheWay | Arrived |
| Arrived | Delivered, Failed |
| Delivered | RefundPending |
| RefundPending | Refunded, Failed |

Every transition is a domain method, changes `ConcurrencyStamp`, and appends immutable history. Customer cancellation is exposed only before pickup; delivered, refunded, already-cancelled, picked-up, and in-transit orders cannot be cancelled. Payment-authorized cancellation emits a future-facing event contract but performs no financial action.

## Persistence

- DbContext: `OrdersDbContext`
- Connection string: `OrdersDatabase` (falls back to `CartsDatabase` for the current single-database deployment)
- Schema: `orders`
- Migration history: `orders.__ef_migrations_history`
- Migration: `InitializeOrdersModule`
- Tables: `orders`, `order_items`, `order_item_options`, `order_status_history`, `order_creation_idempotency`, `outbox_messages`

Money uses `bigint`, enums use `smallint`, identifiers use `uuid`, and timestamps use UTC `timestamp with time zone`. The model has no cross-schema foreign keys and no cascade deletes. Lists use no-tracking projections and deterministic pagination; detail reads use bounded split queries.

Creation idempotency stores SHA-256 hashes of the key and canonical request. `(customer_id, operation, key_hash)` is unique. Same key/payload returns the original order; a different payload returns 409. Order, initial history, idempotency, and outbox are saved by one `SaveChanges` transaction.

Outbox rows contain a stable contract name, JSON payload, event identifier, occurrence/creation timestamps, and processing metadata. Phase 11 persists messages atomically but intentionally has no broker or empty background publisher.

## HTTP API

| Method | Route | Permission | Result |
|---|---|---|---|
| POST | `/api/v1/orders` | `orders.orders.create` | 201, 400, 409, 422 |
| GET | `/api/v1/orders/{orderId}` | `orders.orders.read_own` | 200, 404 |
| GET | `/api/v1/orders/by-number/{orderNumber}` | `orders.orders.read_own` | 200, 404 |
| GET | `/api/v1/orders?page=1&pageSize=20` | `orders.orders.read_own` | 200, 400 |
| GET | `/api/v1/orders/{orderId}/timeline` | `orders.orders.read_own` | 200, 404 |
| POST | `/api/v1/orders/{orderId}/cancel` | `orders.orders.cancel_own` | 200, 403, 404, 409 |

All customer reads are filtered by authenticated customer identity. DTOs exclude outbox payloads, hashes, persistence types, and unnecessary PII.

## Migration and testing

Use the Infrastructure project as both migration and startup project because it owns the design-time factory and EF Design package:

```powershell
dotnet ef database update --project .\src\Modules\Orders\AlSsareea.Modules.Orders.Infrastructure\AlSsareea.Modules.Orders.Infrastructure.csproj --startup-project .\src\Modules\Orders\AlSsareea.Modules.Orders.Infrastructure\AlSsareea.Modules.Orders.Infrastructure.csproj --context OrdersDbContext
dotnet ef migrations has-pending-model-changes --project .\src\Modules\Orders\AlSsareea.Modules.Orders.Infrastructure\AlSsareea.Modules.Orders.Infrastructure.csproj --startup-project .\src\Modules\Orders\AlSsareea.Modules.Orders.Infrastructure\AlSsareea.Modules.Orders.Infrastructure.csproj --context OrdersDbContext
```

Integration tests use the pinned PostGIS Testcontainer and validate migration shape, snapshots, isolation, idempotency, outbox atomicity, and optimistic concurrency.

## Security and limitations

Order creation never trusts client customer IDs, prices, status, or audit timestamps. Idempotency keys are constrained and stored only as hashes. Address/customer data is minimized, and outbox payloads contain identifiers and stable operational fields rather than full snapshots.

Not implemented: payment provider/authorization/capture/void, refund processing, merchant live operations, SignalR, dispatching, driver assignment, tracking, delivery aggregate, notifications, settlement, background publishing, and scheduled-order execution.

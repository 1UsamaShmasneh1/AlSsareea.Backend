# ADR-007: Orders snapshots, checkout orchestration, idempotency, and outbox

- Status: Accepted
- Date: 2026-08-03

## Context

An order must remain auditable after mutable catalog, pricing, promotion, address, and merchant data changes. Checkout crosses module boundaries, but each module owns a DbContext and schema. Payments and message-broker publishing are later phases.

## Decision

Orders is a four-layer module and the authoritative lifecycle aggregate. Creation copies backend-validated checkout, customer/address, and merchant/branch data into relational snapshots and starts at `PendingPayment`.

Orders depends only on public Carts, Customers, and Merchants contracts. Carts orchestrates final Catalog/Pricing/Promotions validation. Orders commits its aggregate, history, hashed idempotency record, and integration-event outbox atomically. Cart conversion follows through an idempotent contract; unique source-cart and idempotency constraints make retry safe without a distributed transaction.

`ConcurrencyStamp` is an EF optimistic concurrency token. The public order number uses the full GUID representation and a unique index. All Orders tables and migration history live in `orders`; foreign keys never cross schemas and all delete behavior is `NO ACTION`.

No publisher runs until a real consumer exists. Payment states are represented in the order lifecycle, but payment provider state and financial operations remain separate.

## Consequences

- Historical order presentation is independent of source-module mutations.
- Duplicate and concurrent create requests converge on one order.
- A cart-conversion call may need retry after the Orders commit; duplicate order creation remains impossible.
- Outbox storage exists before transport publishing, avoiding an empty worker or premature broker choice.
- Snapshot contract evolution must be additive/versioned.

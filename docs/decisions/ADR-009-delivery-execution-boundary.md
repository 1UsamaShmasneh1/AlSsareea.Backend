# ADR-009: Delivery owns execution state and tracking visibility

- Status: Accepted
- Date: 2026-08-17

## Context

Orders owns the commercial order lifecycle, Drivers owns driver eligibility, Media owns assets,
and Tracking owns GPS data and realtime transport. Phase 15 needs a delivery execution workflow,
proof policy, durable history, and customer tracking authorization without collapsing those module
boundaries.

## Decision

Create Delivery as an independently persisted module. It copies trusted order/contact/location
snapshots, stores scalar cross-module identifiers, and owns assignment and execution state. It uses
public contracts to validate Orders, Customers, Drivers, and Media data. Each mutation stores its
idempotency, audit, domain change, and integration event in one Delivery transaction.

Tracking continues to own location data and SignalR. Its deny-by-default visibility abstraction is
implemented by Delivery at composition time. Visibility requires the customer owner, an assigned
driver, and one of the explicit post-pickup non-terminal states.

## Consequences

No distributed transaction or cross-schema foreign key is introduced. Other modules consume
Delivery facts asynchronously from its outbox and must tolerate retries. Copied snapshots are
intentionally immutable. Delivery does not perform dispatch optimization, process payments, store
media bytes, or duplicate Tracking data. If Delivery is absent, Tracking remains deny-by-default.

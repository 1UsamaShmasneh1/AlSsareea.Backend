# ADR-010: Dispatching owns selection; Delivery owns execution

- Status: Accepted
- Date: 2026-08-28

## Context

Drivers owns operational eligibility, Tracking owns live location, Maps owns routing, and Delivery
owns the post-assignment workflow. Driver discovery and competing offers need durable decisions and
concurrency guarantees without merging selection and execution into one state machine.

## Decision

Create Dispatching as an independently persisted module. It discovers candidates through Drivers
Contracts, obtains fresh locations through Tracking Contracts, computes distance and ETA through
Maps Contracts, and records deterministic scores and sequential offers. A successful single-winner
decision is passed through Delivery's idempotent assignment contract. Manual assignment is an
audited, permission-protected selection override, not a Delivery lifecycle override.

## Consequences

There are no cross-schema foreign keys, cross-module DbContext access, or Infrastructure-to-
Infrastructure references. Selection and execution may be retried independently using stable
identifiers and outboxes. The current boundary deliberately excludes batch/broadcast dispatch,
notifications, location storage, and execution transitions.

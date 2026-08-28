# ADR-011: In-process outbox processing for Notifications

## Status

Accepted for Phase 17.

## Context

Orders, Drivers, Delivery and Dispatching persisted integration events transactionally with processing metadata, but the repository had no general event dispatcher and those outboxes were not delivered. Notifications is the first module that requires operational consumption, durable deduplication and retry inside the modular monolith.

## Decision

Add small BuildingBlocks contracts for an integration-event source, consumer and dispatcher. Source Infrastructure implementations own reads and processing-metadata updates for their outbox. Bounded hosted workers dispatch locally to Notifications, and Notifications records a durable Inbox identity before source completion. Delivery work is stored and claimed separately with PostgreSQL conditional updates. Processing is at least once; consumer effects are idempotent.

No distributed broker, queue framework, Redis or distributed lock is introduced. Contracts and serialized integration-event envelopes remain the boundary, so a broker adapter can replace the in-process source/dispatcher later.

## Consequences

The monolith gains graceful, cancellable background processing without new infrastructure. Database uniqueness and conditional claims prevent duplicate durable effects and sends. Source outbox processing currently has one completion marker, so future independent consumers should introduce per-consumer checkpoints or move to a broker before sharing one event with multiple separately deployed consumers.

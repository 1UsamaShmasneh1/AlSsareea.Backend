# ADR-001: Modular monolith

- Status: Accepted
- Date: 2026-07-20

## Context

AlSsareea needs strong domain boundaries but is still at the foundation stage. Starting with independently deployed services would add network, deployment, observability, and data-consistency complexity before the traffic and team boundaries are known.

## Decision

Build a modular monolith. Each module owns separate Domain, Application, Infrastructure, and Contracts projects. Dependencies point inward, and modules never access another module's Infrastructure. Cross-module communication must use explicit contracts.

## Consequences

The platform has one deployment initially while retaining testable boundaries. A module may be extracted when it has a stable contract and data ownership, a distinct scaling or availability requirement, and the operational value exceeds distributed-system cost.

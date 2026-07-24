# ADR-004: Persistence per module

## Context

AlSsareea is a modular monolith whose modules must remain independently evolvable. A shared system-wide data model or `DbContext` would couple module internals and make later extraction difficult.

## Decision

Use one PostgreSQL database initially, with an independent schema, `DbContext`, migrations, and migration-history table for each module. Migrations live in the owning module's Infrastructure project. Module Infrastructure may not query another module's schema directly, and the solution will not introduce a general application `DbContext` or generic repository.

Identity starts with schema `identity`, `IdentityDbContext`, and `identity.__ef_migrations_history`.

## Consequences

- Module ownership and database boundaries remain explicit.
- Each module can evolve and validate its model independently.
- Cross-module reads require contracts rather than direct table access.
- Operationally, one database is simpler now while schemas remain separable into distinct databases later.
- Transactions spanning modules require an explicit future design and are not hidden behind a shared context.

## Alternatives considered

- **One shared `DbContext` and public schema:** rejected because it erodes module boundaries.
- **A database per module now:** deferred because it adds deployment, connection, and cross-module consistency overhead before scale requires it.
- **Generic repository:** rejected because it obscures EF capabilities and does not establish a useful business abstraction.

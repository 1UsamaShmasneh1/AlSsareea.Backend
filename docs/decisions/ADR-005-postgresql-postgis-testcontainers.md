# ADR-005: PostgreSQL, PostGIS, and Testcontainers

## Context

The delivery platform will require relational persistence and geographic capabilities. Persistence tests must exercise the same provider semantics used in production.

## Decision

Use PostgreSQL through Npgsql and EF Core. Enable PostGIS at the database level and register NetTopologySuite support from the start, without adding spatial fields to Identity. Use `postgis/postgis:17-3.5` pinned to digest `sha256:404171ea9058c801f405af25d63b3b8e5c9e50f2759e49390dbcc3c7ee533f4d` for local Compose and Testcontainers. Integration tests start a real, isolated PostGIS container and apply migrations explicitly.

## Consequences

- PostgreSQL types, schemas, migrations, naming, and timestamp behavior are tested realistically.
- PostGIS is ready for later geographic modules without contaminating the Identity model.
- Integration tests require Docker locally and on CI runners and take longer than in-memory tests.
- The container image version must be reviewed and updated deliberately.

## Alternatives considered

- **EF Core InMemory:** rejected because it is not a relational PostgreSQL implementation.
- **SQLite:** rejected because its types, schemas, SQL dialect, extensions, and concurrency behavior differ from PostgreSQL.
- **Mocks of `DbContext`:** rejected because they cannot validate migrations or provider behavior.
- **A manually managed shared test database:** rejected because it weakens isolation and reproducibility compared with Testcontainers.

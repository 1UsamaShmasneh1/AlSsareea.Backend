# Identity module

Phase 2 implements the persistent Identity model only. `User`, `Role`, and `Permission` are aggregate roots. `Device`, `LoginSession`, `RefreshToken`, `PasswordHistory`, and `LoginHistory` are lifecycle or historical entities owned by Identity; `UserRole` and `RolePermission` are explicit association entities.

The Domain project is framework-neutral. Application exposes only repositories for the three aggregate roots. Infrastructure owns `IdentityDbContext`, PostgreSQL mappings, repositories, and migrations. Contracts contains the module's public integration boundary. The API is only the composition root and does not access the context from endpoints.

## Persistence

- Connection string: `IdentityDatabase`
- Schema: `identity`
- Migration history: `identity.__ef_migrations_history`
- Current migration: `InitializeIdentityDomain`
- Delete policy: every foreign key is `RESTRICT`; users use soft deletion
- Concurrency: GUID concurrency stamps on User, Role, Permission, and Device
- History: login and password history are append-only

Run migrations and integration tests from the repository root using the commands in the root README. Integration tests require Docker and use an isolated PostgreSQL 17/PostGIS Testcontainer.

## Security decisions

Only hashes are represented for passwords, refresh tokens, and attempted login identifiers. Password and refresh-token hash value objects redact `ToString()`. Email and phone uniqueness includes soft-deleted users so identifiers are never silently reused. No default user, password, role, permission, or secret is seeded.

## Not implemented

Login, registration, JWT issuance, refresh-token rotation flow, logout API, OTP, MFA, authorization policies, password reset/change endpoints, email verification, social login, external messaging, administrative APIs, and customer/driver/merchant onboarding are deliberately deferred.

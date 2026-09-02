# Identity module

Identity owns local and external authentication. `User`, `Role`, and `Permission` are aggregate roots. `Device`, `LoginSession`, `RefreshToken`, `ExternalIdentity`, `PasswordHistory`, and `LoginHistory` are lifecycle or historical entities owned by Identity; `UserRole` and `RolePermission` are explicit association entities.

The Domain project is framework-neutral. Application exposes only repositories for the three aggregate roots. Infrastructure owns `IdentityDbContext`, PostgreSQL mappings, repositories, and migrations. Contracts contains the module's public integration boundary. The API is only the composition root and does not access the context from endpoints.

## Persistence

- Connection string: `IdentityDatabase`
- Schema: `identity`
- Migration history: `identity.__ef_migrations_history`
- Current migration: `AddCustomerRegistrationAndExternalIdentities`
- Delete policy: every foreign key is `RESTRICT`; users use soft deletion
- Concurrency: GUID concurrency stamps on User, Role, Permission, and Device
- History: login and password history are append-only

Run migrations and integration tests from the repository root using the commands in the root README. Integration tests require Docker and use an isolated PostgreSQL 17/PostGIS Testcontainer.

## Security decisions

Only hashes are represented for passwords, refresh tokens, and attempted login identifiers. Password and refresh-token hash value objects redact `ToString()`. Email and phone uniqueness includes soft-deleted users so identifiers are never silently reused. External identities are uniquely keyed by provider and validated provider subject; email is never used to silently link accounts. External-only customers have no password hash and cannot use password login. No default user, password, role, permission, or secret is seeded.

## Customer authentication

`POST /api/v1/auth/register/customer` creates only an active Customer identity, device, session, and rotating refresh-token family. `POST /api/v1/auth/external/google` independently validates the Google credential and either reuses its provider-subject link or creates a new external-only Customer. A verified-email collision with an unlinked local account returns `auth.external_link_required`; it never auto-links or creates a duplicate user. Both anonymous operations are rate limited.

Password reset/change, account linking after re-authentication, MFA, production OTP delivery, other social providers, and administrative user-management APIs remain out of scope.

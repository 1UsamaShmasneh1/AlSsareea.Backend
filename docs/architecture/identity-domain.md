# Identity domain and persistence — Phase 2

## Domain model

`User`, `Role`, and `Permission` are aggregate roots and the only types with repositories. A user owns role assignments, devices, sessions, refresh tokens, and password history. A role owns permission assignments. Login history is an append-only module record and may omit the user when an attempted identifier cannot be resolved.

```text
User 1 ── * UserRole * ── 1 Role
Role 1 ── * RolePermission * ── 1 Permission
User 1 ── * Device
User 1 ── * LoginSession
User 1 ── * RefreshToken
User 1 ── * PasswordHistory
User 0..1 ── * LoginHistory
Device 0..1 ── * LoginSession / RefreshToken / LoginHistory
LoginSession 1 ── * RefreshToken
```

All IDs are application-generated GUID value types. Email preserves display casing and stores an invariant lowercase normalized value. Phone numbers use E.164. Password hashes require a versioned representation; refresh-token and attempted-identifier hashes use 64-character SHA-256 hexadecimal representations. Secret hash value objects never reveal values through `ToString()`.

## Tables, constraints, and indexes

All tables are in the `identity` schema and use snake_case names.

| Table | Purpose | Important constraints/indexes |
|---|---|---|
| `users` | Identity account and audit state | unique normalized email/phone, contact-required and enum checks, status/type/time indexes, soft-delete filter |
| `roles` | Named role aggregate | unique normalized name, active index, concurrency stamp |
| `permissions` | Stable technical permission | unique technical name, dotted-lowercase check, module/active indexes, concurrency stamp |
| `user_roles` | Explicit role assignment | composite PK `(user_id, role_id)`, role index |
| `role_permissions` | Explicit permission assignment | composite PK `(role_id, permission_id)`, permission index |
| `devices` | Client-provided device identity | unique `(user_id, device_identifier)`, platform check, user/last-seen/revoked indexes |
| `login_sessions` | Durable session lifecycle | state/date checks and user/device/token/state/activity/expiry indexes |
| `refresh_tokens` | Hashed refresh-token lifecycle | unique token hash, expiry/self-replacement checks, ownership/lifecycle indexes |
| `password_history` | Prior versioned password hashes | user and `(user_id, became_active_utc)` indexes, date check, append-only |
| `login_history` | Hashed login-attempt audit | success/failure consistency checks, identifier/result/time and user-time indexes, append-only |

GUIDs map to `uuid`, enums to `smallint`, UTC timestamps to `timestamp with time zone`, and IP addresses to `inet`. All constraint and index names are explicit. Every foreign key uses `RESTRICT`; there is no cascade deletion.

## Soft deletion and concurrency

Only User is soft-deleted. The global query filter excludes deleted users, while administrative persistence code can explicitly use `IgnoreQueryFilters()`. Unique email and phone indexes are unfiltered, so soft-deleted accounts retain identifier ownership.

User, Role, Permission, and Device use independent `concurrency_stamp` tokens. Domain mutations rotate the stamp, allowing EF Core to raise `DbUpdateConcurrencyException` instead of silently overwriting a concurrent update. `security_stamp` is separate and rotates for security-sensitive User changes.

## Migration and verification

`InitializeIdentityDomain` creates only the Identity schema and its ten tables. Migrations are never applied by API startup. Testcontainers tests apply migrations to an empty PostgreSQL/PostGIS database, validate the model snapshot, constraints, uniqueness, filter bypass, lifecycle persistence, append-only behavior, mappings, delete policy, and an optimistic concurrency conflict.

## Known limitations

This phase supplies storage and domain lifecycle operations, not operational authentication. Login, registration, JWT, refresh-token rotation, OTP, MFA, authorization policies, password reset, external messaging, and onboarding remain unimplemented. Domain events are recorded in-memory only; no external side effects or integration-event publication is configured.

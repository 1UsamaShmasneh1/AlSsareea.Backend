# Notifications module

## Responsibility and boundaries

Notifications owns templates, localized rendered notifications, recipient channel preferences, in-app inbox state, push-token registrations, delivery/attempt history, retries, throttling, Inbox deduplication, audit, and its own Outbox. Identity continues to own authentication devices, sessions, and credentials. User, customer, merchant, driver, order, delivery, and dispatch identifiers are external scalar identifiers; the `notifications` schema has no cross-schema foreign keys and the module never uses another module's `DbContext`.

The module has Domain, Application, Contracts, and Infrastructure projects. Source modules expose recipient lookups through stable Contracts and expose their transactional outboxes through the BuildingBlocks `IIntegrationEventSource` boundary. Notifications depends only on Contracts, not other module Infrastructure.

## Domain model and persistence

`Notification` is the consistency boundary for rendered content and its `NotificationDelivery`/append-only `NotificationAttempt` children. `DeviceToken` owns registration, refresh, deactivation, invalid-token handling, ownership and optimistic concurrency. `NotificationTemplate` and `NotificationPreference` are separate consistency boundaries. Durable inbox, audit, and outbox records are append-only history.

`NotificationsDbContext` uses schema `notifications` and migration history `notifications.__ef_migrations_history`. Migration `20260828121329_InitializeNotificationsModule` creates templates, notifications, deliveries, attempts, device tokens, preferences, inbox, audit and outbox tables, with channel/status/attempt checks, uniqueness for template localization, token hashes, preferences and source-event deliveries, worker indexes, optimistic concurrency stamps, and only intra-schema foreign keys.

## Channels and providers

Channels are Push, SMS, Email, InApp and WhatsApp. In-App is fully local and reaches `Delivered`. Push routing uses production-capable FCM HTTP v1 and APNs HTTP/2 adapters inside Notifications.Infrastructure. Both providers are disabled by default and return `NotConfigured` without a network call. SMS and Email are provider-neutral boundaries and return `NotConfigured` until an approved vendor is configured. WhatsApp is represented but deliberately unavailable until an official provider is approved. A provider `Accepted` result means the provider accepted the request; it is not proof that a person or device finally received it, and this phase does not implement delivery-receipt webhooks.

FCM uses a service-account RS256 assertion to obtain a cached OAuth access token for the Firebase Messaging scope, then posts a token-targeted notification to `https://fcm.googleapis.com/v1/projects/{ProjectId}/messages:send`. Configuration is under `Notifications:Providers:Fcm`: `Enabled`, `ProjectId`, `ClientEmail`, `PrivateKey`, `TokenUri`, and `TimeoutSeconds`. APNs uses a cached ES256 provider JWT and an HTTP/2 request to the production or sandbox `/3/device/{token}` endpoint. Its configuration under `Notifications:Providers:Apns` is `Enabled`, `TeamId`, `KeyId`, `BundleId`, `PrivateKey`, `UseSandbox`, and `TimeoutSeconds`.

Private keys and service-account values must come from the deployment secret provider or environment variables, for example `Notifications__Providers__Fcm__PrivateKey` and `Notifications__Providers__Apns__PrivateKey`; no credential is stored in this repository or the notifications database. Enabling a provider with incomplete settings fails startup validation. Provider HTTP logging is disabled and replaced with structured delivery-id/status/classification logging that excludes device tokens, private keys, OAuth tokens, and APNs JWTs.

FCM `UNREGISTERED` and APNs `BadDeviceToken`, `DeviceTokenNotForTopic`, and `Unregistered` responses map to `InvalidToken`. HTTP 429 maps to `RateLimited`, 5xx responses map to `Transient`, authentication failures are distinct permanent failures, and other malformed/permanent 4xx responses map to `Permanent`. Invalid-token results deactivate the encrypted registration and create a safe audit record; already inactive registrations are rejected locally without another provider request.

Device tokens are notification routing data, not authentication devices. Tokens are encrypted with ASP.NET Data Protection at rest, indexed by SHA-256 hash, and only masked values appear in responses/audit. Full tokens and provider secrets are never logged. Invalid-token provider results deactivate the registration.

## Templates and localization

Templates use stable keys, channel and language (`ar`, `he`, `en`). Rendering supports only explicit `{{parameter}}` substitution, rejects missing/unclosed parameters, and executes no expressions or scripts. A valid requested language is selected first and deterministically falls back to Arabic. Seeded customer-order, merchant-order, delivery-status and driver-offer templates cover Push and In-App in all three languages.

## Event processing and idempotency

The existing Orders, Delivery and Dispatching outboxes were persistence-only. Phase 17 adds a bounded hosted processor and dispatcher. Source outbox rows are read through `IIntegrationEventSource`; the consumer handles `OrderCreatedIntegrationEvent` for customer and merchant recipients, `DeliveryStatusChangedIntegrationEvent` for customers, and `DispatchOfferCreatedIntegrationEvent` for drivers. Recipient resolution occurs through Customers, Merchants, Drivers and Delivery Contracts.

Processing is at least once. Each source-event/recipient/category identity is deterministically derived and stored as the primary key of `notification_inbox_messages`. Replays return success without recreating notifications or deliveries. A unique `(source_event_id,user_id,channel)` index provides a second database guard. Source outbox rows are marked processed only after the consumer succeeds; failures increment bounded attempt metadata and retain a safe error code. No Kafka, RabbitMQ, Redis or external broker is required inside the modular monolith. The Contracts-based source/consumer boundary can later be adapted to a broker.

## Delivery, retry and throttling

The delivery worker atomically claims only Queued/RetryScheduled rows using a conditional PostgreSQL update. Processing claims have a bounded lease; an interrupted process releases stale claims back to RetryScheduled on the next pass so they are not stranded. It uses bounded batches, cancellation, a periodic timer, isolated batch exceptions and graceful shutdown. Transient/rate-limited results use exponential backoff bounded by configuration and maximum attempts; permanent, invalid-token and not-configured failures do not retry. A per-recipient, PostgreSQL-backed one-minute attempt count prevents notification storms independently of HTTP API rate limiting. Parallel claims cannot both send the same delivery.

Configuration is under `Notifications:Processing`: polling interval, batch size, per-recipient delivery limit, maximum backoff and processing-lease duration. Options are bounded and validated during startup. No external connection occurs at startup.

## API and security

Authenticated self-service endpoints derive ownership exclusively from the current JWT user:

- `GET /api/v1/notifications?page=&pageSize=`
- `POST /api/v1/notifications/{notificationId}/read`
- `POST /api/v1/notifications/read-all`
- `POST /api/v1/notifications/devices`
- `DELETE /api/v1/notifications/devices/{deviceTokenId}`
- `GET /api/v1/notifications/preferences`
- `PUT /api/v1/notifications/preferences`

Read/read-all are idempotent. Device registration is idempotent by token hash and ownership. Preferences are unique per user/category/channel. Endpoints never accept another user's identifier, never expose provider errors, and use the API Problem Details convention and separate read/write rate-limit policies. No administration portal or template-management API is included in Phase 17.

`notification_audit` remains append-only and records the operational transitions `consume_event`, `register_device`, `unregister_device`, `invalidate_device`, `update_preferences`, `mark_read`, `mark_all_read`, `delivery_accepted`, `delivery_retry_scheduled`, and `delivery_failed`. Read-all produces one aggregate record with the affected count. Delivery audit details contain only safe provider/error classifications; `NotificationAttempt` remains the detailed per-attempt history.

## Testing and limitations

Unit tests cover invariants, three-language rendering, missing parameters, preference suppression, read idempotency, retry/permanent/max-attempt behavior, FCM OAuth/HTTP v1 payloads and mappings, APNs JWT/HTTP2 payloads and mappings, cancellation, endpoint selection, credential reuse, and safe diagnostics. Architecture tests enforce dependency direction and prevent provider leakage/DbContext use in endpoints. PostgreSQL Testcontainers tests apply the real migration, inspect tables/checks/indexes/isolation, verify no pending model changes, persist delivery history, replay an event without duplicate notification or delivery, exercise runtime audit paths, reject audit mutation/deletion, and verify invalid-token deactivation plus suppression of later provider calls.

Production credentials and live provider connectivity are deployment concerns and were not used by automated tests. Vendor-specific SMS/Email adapters, WhatsApp, delivery-receipt webhooks and multi-node data-protection key storage remain future work. The in-process processor is intentionally for the modular monolith; an external broker remains a future extraction option. Customer/Driver applications in Phases 18/19 are required for physical-device end-to-end validation.

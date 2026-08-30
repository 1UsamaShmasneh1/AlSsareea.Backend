# Customer App backend contracts

Phase 18A adds only customer-safe mobile contracts. Existing Merchant management routes and permissions are unchanged.

## Merchant discovery

Anonymous endpoints use `merchants-read` (120 requests/minute):

- `GET /api/v1/customer/merchants/?page=1&pageSize=20&query=&openNow=`
- `GET /api/v1/customer/merchants/{merchantId}`

Only active merchants having an active or temporarily closed visible branch are returned. Ordering is display name then ID, page size is capped at 100, and `query` matches display names case-insensitively. `openNow` uses existing branch lifecycle, timezone, weekly-hours, and schedule-override rules.

Responses contain display name, public description, open state, and safe branch name/address/coordinates. Owner IDs, employees, registration/tax data, private contacts, lifecycle reasons, audit fields, and concurrency stamps are excluded. Details provide the existing per-merchant Catalog path.

No global product search was added. Discovery search selects a merchant, then existing `/api/v1/merchants/{merchantId}/catalog/search` performs product search under Catalog ownership.

## Customer Maps

All endpoints require authentication and use `maps-read` (60 requests/minute):

- `POST /api/v1/maps/geocode` — `{ query, countryCode? }` to provider-neutral `GeocodingResult[]`.
- `POST /api/v1/maps/reverse-geocode` — `{ latitude, longitude }` to `ReverseGeocodingResult`.
- `POST /api/v1/maps/delivery-eligibility` — coordinates to `{ eligible, serviceAreaId, reasonCode }`, using active Maps-owned service areas and existing boundary semantics.

Problem Details codes are `maps.invalid_request`, `maps.location_not_found`, and `maps.provider_unavailable`. Customers continues to own saved addresses. Fake remains the only configured provider.

## Tracking realtime

- Hub: authenticated `/hubs/tracking`.
- Client methods: `SubscribeSelf()`, `SubscribeOperations()`, and `SubscribeOrder(Guid orderId)`.
- Server event: `LocationUpdated`.
- Customer payload: `{ latitude, longitude, recordedAtUtc, accuracyMeters, speedMetersPerSecond, headingDegrees }`.

Order visibility is resolved server-side through Delivery: the user owns the order, a driver is assigned, and status is PickedUp, InTransit, or ArrivedAtDropOff. Denial returns `tracking_scope_denied`. Tracking publishes accepted latest locations to Delivery-resolved order groups. On reconnect, fetch `/api/v1/tracking/orders/{orderId}/latest` and resubscribe.

## Notifications

Existing authenticated contracts are sufficient:

- `POST /api/v1/notifications/devices` with `{ token, platform, provider }`; platform Android=1, iOS=2, Web=3; provider FCM=1, APNs=2.
- `DELETE /api/v1/notifications/devices/{deviceTokenId}` is ownership-checked and idempotent.
- `GET /api/v1/notifications/?page=1&pageSize=20`.
- `POST /api/v1/notifications/{notificationId}/read`; `POST /api/v1/notifications/read-all`.
- `GET` and `PUT /api/v1/notifications/preferences`.

Registration derives ownership from JWT, upserts by token hash, is idempotent, and reactivates an existing token. For token rotation, register the new token and unregister the prior returned device-token ID. Invalid provider tokens are deactivated automatically; responses contain masks only.

Payments remain Phase 22, support Phase 24, native MAUI work Phase 18B, and a production Maps provider remains deferred.

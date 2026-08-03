# Carts security

Customer identity comes from the authenticated token and is mapped through Customers; request bodies cannot choose a customer. Every load conceals another customer's cart as not found. Product prices, discounts, fees, and totals are absent from mutation requests and calculated server-side.

Writes scope idempotency by customer and operation, compare request hashes, enforce input limits, and reject stale GUID concurrency stamps with `carts.concurrency_conflict`.


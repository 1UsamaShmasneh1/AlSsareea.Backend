# Carts module

Carts owns the pre-order cart lifecycle, line configurations, coupon reference, expiration, concurrency, idempotency, and calculated checkout summary. It uses public Customers, Merchants, Catalog, Pricing, and Promotions contracts and never reads another module's schema.

Equivalent product/variant/option/note configurations merge by summing quantity. Different notes or configurations remain separate. The client never submits authoritative prices or discounts. Routes are authenticated under `/api/carts`; ownership is derived from the current user, and writes require `Idempotency-Key` plus a current concurrency stamp.

Phase 10 excludes order creation, payments, inventory reservation, delivery creation, promotion redemption, guest merge, distributed cache, background cleanup, and real-time synchronization.


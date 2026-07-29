# Checkout summary

`GET /api/carts/{cartId}/checkout-summary` and `POST /api/carts/{cartId}/reprice` regenerate the authoritative pre-order view. Each request revalidates merchant context and every Catalog line, calculates Pricing, then evaluates Promotions.

Unavailable or changed lines remain visible with machine-readable blocking codes. Empty, expired, unavailable, currency-inconsistent, or unpriceable carts are not checkout-ready. Summaries are calculated views rather than orders.

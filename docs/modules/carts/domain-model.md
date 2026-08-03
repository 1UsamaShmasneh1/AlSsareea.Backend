# Cart domain model

`Cart` is an aggregate root with `Active`, `Expired`, `Cleared`, and future-compatible `Converted` states. It owns `CartItem` entities and normalized `CartItemOption` references. Only active, unexpired carts mutate. Reads and writes call expiration logic using `IClock`.

Each line stores Catalog references and the last observed catalog version, never an authoritative price. Quantity is 1–99, notes are limited to 500 characters, and coupon codes are trimmed and upper-cased.


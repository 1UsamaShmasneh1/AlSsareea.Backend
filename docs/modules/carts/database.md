# Carts database

`CartsDbContext` owns PostgreSQL schema `carts` and tables `carts`, `cart_items`, `cart_item_options`, and `cart_idempotency_records`. Foreign keys exist only inside the schema.

Migration `20260729191929_AddCartsModule` creates constraints, indexes, and a partial `NULLS NOT DISTINCT` unique index on active `(customer_id, merchant_id, branch_id)` contexts. Concurrency uses `concurrency_stamp`. Idempotency keys and payloads are stored as SHA-256 hashes and expire after 24 hours.

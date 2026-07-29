# Carts testing

Domain tests cover lifecycle, expiration, quantity limits, deterministic option ordering, equivalent-line merge, separate configurations, notes, and coupon normalization. Architecture tests enforce layer direction and forbid other module Infrastructure dependencies. PostgreSQL integration coverage verifies migration-created module tables.

The integration suite requires a running Docker engine for the pinned PostgreSQL/PostGIS Testcontainer.

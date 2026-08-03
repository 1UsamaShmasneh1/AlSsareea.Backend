# Carts integrations

Customers resolves the authenticated user to an allowed customer. Merchants verifies active merchant and operational branch context. Catalog validates product configuration and catalog version. Pricing calculates fees and totals from backend-derived line values. Promotions evaluates automatic promotions and the entered coupon without redeeming it.

All integrations are public contracts; Carts has no cross-module EF navigation, join, foreign key, or `DbContext` access.

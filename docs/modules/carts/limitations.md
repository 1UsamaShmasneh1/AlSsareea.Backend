# Carts limitations

Orders, checkout transactions, payments, inventory reservation, promotion redemption, and delivery jobs are intentionally absent. The repository has no guest identity/cart model, so login merge is deferred. Expiration is enforced during reads and writes; no cleanup worker is included. There is no distributed cache or real-time cart synchronization.


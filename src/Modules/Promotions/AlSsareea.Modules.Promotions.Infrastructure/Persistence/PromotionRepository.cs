using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Promotions.Infrastructure.Persistence;

internal sealed class PromotionRepository(PromotionsDbContext db) : IPromotionRepository
{
    public Task<Promotion?> GetAsync(PromotionId id, CancellationToken cancellationToken = default) =>
        db.Promotions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default) =>
        await db.Promotions.AddAsync(promotion, cancellationToken);
}

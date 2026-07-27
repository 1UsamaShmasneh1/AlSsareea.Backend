using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Pricing.Infrastructure.Persistence;

internal sealed class PricingPolicyRepository(PricingDbContext db) : IPricingPolicyRepository
{
    public Task<PricingPolicy?> GetAsync(PricingPolicyId id, bool tracked = true, CancellationToken cancellationToken = default)
    {
        IQueryable<PricingPolicy> query = db.Policies.Include(x => x.Rules);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken = default) =>
        db.Policies.AddAsync(policy, cancellationToken).AsTask();
}

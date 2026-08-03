using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class MerchantOrderOperationsScopeProvider(MerchantsDbContext db) : IMerchantOrderOperationsScopeProvider
{
    public async Task<IReadOnlyList<MerchantOrderOperationsScope>> GetScopesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return [];

        var owned = await db.Merchants.AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .Select(x => new { MerchantId = x.Id.Value, IsActive = x.Status == MerchantStatus.Active })
            .ToArrayAsync(cancellationToken);

        var memberships = await db.Employees.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == MerchantMembershipStatus.Active)
            .Join(db.Merchants.AsNoTracking(), employee => employee.MerchantId, merchant => merchant.Id,
                (employee, merchant) => new
                {
                    MerchantId = merchant.Id.Value,
                    IsActive = merchant.Status == MerchantStatus.Active,
                    RestrictedBranchId = employee.BranchId.HasValue ? (Guid?)employee.BranchId.Value.Value : null,
                })
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, MerchantOrderOperationsScope> scopes = owned.ToDictionary(
            x => x.MerchantId,
            x => new MerchantOrderOperationsScope(x.MerchantId, x.IsActive, null, true));

        foreach (var membership in memberships)
        {
            scopes.TryAdd(membership.MerchantId, new MerchantOrderOperationsScope(
                membership.MerchantId,
                membership.IsActive,
                membership.RestrictedBranchId,
                false));
        }

        return scopes.Values.OrderBy(x => x.MerchantId).ToArray();
    }

    public async Task<MerchantOrderOperationsScope?> GetScopeAsync(Guid merchantId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (merchantId == Guid.Empty || userId == Guid.Empty) return null;
        MerchantId id = new(merchantId);
        Merchant? merchant = await db.Merchants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (merchant is null) return null;
        if (merchant.OwnerUserId == userId) return new(merchantId, merchant.Status == MerchantStatus.Active, null, true);

        MerchantEmployee? membership = await db.Employees.AsNoTracking().SingleOrDefaultAsync(
            x => x.MerchantId == id && x.UserId == userId && x.Status == MerchantMembershipStatus.Active,
            cancellationToken);
        return membership is null
            ? null
            : new(merchantId, merchant.Status == MerchantStatus.Active, membership.BranchId?.Value, false);
    }

    public Task<bool> IsOperationalBranchAsync(Guid merchantId, Guid branchId, CancellationToken cancellationToken = default) =>
        db.Branches.AsNoTracking().AnyAsync(x =>
            x.MerchantId == new MerchantId(merchantId) &&
            x.Id == new MerchantBranchId(branchId) &&
            x.Status == MerchantBranchStatus.Active,
            cancellationToken);

    public Task<bool> IsBranchInMerchantAsync(Guid merchantId, Guid branchId, CancellationToken cancellationToken = default) =>
        db.Branches.AsNoTracking().AnyAsync(x =>
            x.MerchantId == new MerchantId(merchantId) && x.Id == new MerchantBranchId(branchId),
            cancellationToken);
}

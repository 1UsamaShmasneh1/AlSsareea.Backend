using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class MerchantCatalogScopeProvider(MerchantsDbContext db) : IMerchantCatalogScopeProvider
{
    public async Task<MerchantCatalogScope?> GetScopeAsync(Guid merchantId, Guid userId, bool isPlatformOperator, CancellationToken ct = default)
    {
        MerchantId id = new(merchantId);
        Merchant? merchant = await db.Merchants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (merchant is null) return null;
        if (isPlatformOperator) return new(merchantId, merchant.Status == MerchantStatus.Active, true, null);
        MerchantEmployee? employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.MerchantId == id && x.UserId == userId && x.Status == MerchantMembershipStatus.Active, ct);
        return employee is null ? new(merchantId, merchant.Status == MerchantStatus.Active, false, null) : new(merchantId, merchant.Status == MerchantStatus.Active, true, employee.BranchId?.Value);
    }

    public Task<bool> IsOperationalBranchAsync(Guid merchantId, Guid branchId, CancellationToken ct = default) =>
        db.Branches.AsNoTracking().AnyAsync(x =>
            x.MerchantId == new MerchantId(merchantId) &&
            x.Id == new MerchantBranchId(branchId) &&
            x.Status == MerchantBranchStatus.Active, ct);
}

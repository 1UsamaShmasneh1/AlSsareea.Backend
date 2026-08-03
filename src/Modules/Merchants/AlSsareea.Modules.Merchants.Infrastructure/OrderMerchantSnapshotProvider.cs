using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class OrderMerchantSnapshotProvider(MerchantsDbContext db) : IOrderMerchantSnapshotProvider
{
    public async Task<OrderMerchantSnapshotContract?> GetAsync(Guid merchantId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        Merchant? merchant = await db.Merchants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == new MerchantId(merchantId), cancellationToken);
        if (merchant is null || merchant.Status != MerchantStatus.Active) return null;
        if (!branchId.HasValue) return new(merchantId, null, merchant.DisplayName, null, null, null);
        MerchantBranch? branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == new MerchantBranchId(branchId.Value) && x.MerchantId == new MerchantId(merchantId), cancellationToken);
        if (branch is null || branch.Status != MerchantBranchStatus.Active) return null;
        string address = string.Join(", ", new[] { branch.Address.Street, branch.Address.BuildingNumber, branch.Address.Area, branch.Address.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new(merchantId, branchId, merchant.DisplayName, branch.Name, address, branch.PhoneNumber);
    }
}

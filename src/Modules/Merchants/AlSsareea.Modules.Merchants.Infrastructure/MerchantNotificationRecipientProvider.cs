using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class MerchantNotificationRecipientProvider(MerchantsDbContext db) : IMerchantNotificationRecipientProvider
{
    public async Task<IReadOnlyList<MerchantNotificationRecipient>> GetAsync(Guid merchantId, Guid? branchId, CancellationToken ct = default)
    {
        MerchantId id = new(merchantId); Guid? owner = await db.Merchants.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.OwnerUserId).SingleOrDefaultAsync(ct); if (owner is null) return [];
        Guid[] employees = await db.Employees.AsNoTracking().Where(x => x.MerchantId == id && x.Status == MerchantMembershipStatus.Active && (x.BranchId == null || branchId == null || x.BranchId == new MerchantBranchId(branchId.Value))).Select(x => x.UserId).ToArrayAsync(ct);
        return employees.Append(owner.Value).Distinct().Select(x => new MerchantNotificationRecipient(x, "ar")).ToArray();
    }
}

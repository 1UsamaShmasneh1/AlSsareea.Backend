using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure.Persistence;

internal sealed class MerchantRepository(MerchantsDbContext db) : IMerchantRepository
{
    public Task<Merchant?> GetAsync(MerchantId id, CancellationToken cancellationToken = default) => db.Merchants.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Merchant merchant, CancellationToken cancellationToken = default) => await db.Merchants.AddAsync(merchant, cancellationToken);
}

internal sealed class MerchantBranchRepository(MerchantsDbContext db) : IMerchantBranchRepository
{
    public Task<MerchantBranch?> GetAsync(MerchantId merchantId, MerchantBranchId id, CancellationToken cancellationToken = default) =>
        db.Branches.Include(x => x.BusinessHours).ThenInclude(x => x.Periods)
            .Include(x => x.ScheduleOverrides).ThenInclude(x => x.Periods)
            .Include(x => x.ServiceAreas)
            .SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.Id == id, cancellationToken);
    public async Task AddAsync(MerchantBranch branch, CancellationToken cancellationToken = default) => await db.Branches.AddAsync(branch, cancellationToken);
}

internal sealed class MerchantEmployeeRepository(MerchantsDbContext db) : IMerchantEmployeeRepository
{
    public Task<MerchantEmployee?> GetAsync(MerchantId merchantId, MerchantEmployeeId id, CancellationToken cancellationToken = default) => db.Employees.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.Id == id, cancellationToken);
    public Task<MerchantEmployee?> GetByUserAsync(MerchantId merchantId, Guid userId, CancellationToken cancellationToken = default) => db.Employees.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.UserId == userId && x.Status != MerchantMembershipStatus.Removed, cancellationToken);
    public async Task AddAsync(MerchantEmployee employee, CancellationToken cancellationToken = default) => await db.Employees.AddAsync(employee, cancellationToken);
}

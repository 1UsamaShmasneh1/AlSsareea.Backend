using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class CustomerMerchantQueryService(MerchantsDbContext db, IClock clock) : ICustomerMerchantQueryService
{
    public async Task<MerchantOperationResult<CustomerMerchantListResponse>> DiscoverAsync(int page, int pageSize, string? query, bool? openNow, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) return MerchantOperation.Failure<CustomerMerchantListResponse>(MerchantOperationStatus.Invalid, "invalid_pagination");
        IQueryable<Merchant> merchants = db.Merchants.AsNoTracking().Where(x => x.Status == MerchantStatus.Active);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string value = query.Trim();
            merchants = merchants.Where(x => EF.Functions.ILike(x.DisplayName, $"%{value}%"));
        }

        Merchant[] candidates = await merchants.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).ToArrayAsync(ct);
        var values = new List<CustomerMerchantSummary>(candidates.Length);
        foreach (Merchant merchant in candidates)
        {
            CustomerMerchantBranchSummary[] branches = await VisibleBranchesAsync(merchant.Id, ct);
            if (branches.Length == 0) continue;
            bool isOpen = branches.Any(x => x.IsOpen);
            if (openNow.HasValue && isOpen != openNow.Value) continue;
            values.Add(new(merchant.Id.Value, merchant.DisplayName, merchant.Description, isOpen, branches.FirstOrDefault(x => x.IsPrimary) ?? branches[0]));
        }

        int total = values.Count;
        CustomerMerchantSummary[] items = values.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return MerchantOperation.Success(new CustomerMerchantListResponse(items, page, pageSize, total));
    }

    public async Task<MerchantOperationResult<CustomerMerchantDetails>> GetDetailsAsync(Guid merchantId, CancellationToken ct)
    {
        MerchantId id;
        try { id = new MerchantId(merchantId); }
        catch (Exception exception) when (exception is ArgumentException or AlSsareea.BuildingBlocks.Domain.DomainException)
        { return MerchantOperation.Failure<CustomerMerchantDetails>(MerchantOperationStatus.NotFound, "merchant_not_found"); }

        Merchant? merchant = await db.Merchants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status == MerchantStatus.Active, ct);
        if (merchant is null) return MerchantOperation.Failure<CustomerMerchantDetails>(MerchantOperationStatus.NotFound, "merchant_not_found");
        CustomerMerchantBranchSummary[] branches = await VisibleBranchesAsync(id, ct);
        if (branches.Length == 0) return MerchantOperation.Failure<CustomerMerchantDetails>(MerchantOperationStatus.NotFound, "merchant_not_found");
        return MerchantOperation.Success(new CustomerMerchantDetails(merchant.Id.Value, merchant.DisplayName, merchant.Description, branches.Any(x => x.IsOpen), branches, $"/api/v1/merchants/{merchant.Id.Value}/catalog"));
    }

    private async Task<CustomerMerchantBranchSummary[]> VisibleBranchesAsync(MerchantId merchantId, CancellationToken ct)
    {
        MerchantBranch[] branches = await db.Branches.AsNoTracking()
            .Include(x => x.BusinessHours).ThenInclude(x => x.Periods)
            .Include(x => x.ScheduleOverrides).ThenInclude(x => x.Periods)
            .Where(x => x.MerchantId == merchantId && (x.Status == MerchantBranchStatus.Active || x.Status == MerchantBranchStatus.TemporarilyClosed))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .ToArrayAsync(ct);
        return branches.Select(x => new CustomerMerchantBranchSummary(x.Id.Value, x.Name, x.Address.City, x.Address.Area, x.Address.Street, x.Location.Latitude, x.Location.Longitude, x.IsPrimary, x.GetAvailability(clock.UtcNow).IsOpen)).ToArray();
    }
}

using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Contracts;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Domain;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Merchants.Infrastructure;

internal sealed class MerchantsService(
    MerchantsDbContext db,
    IMerchantRepository merchants,
    IMerchantBranchRepository branches,
    IMerchantEmployeeRepository employees,
    IIdentityUserLookup identityUsers,
    IMapsModule maps,
    IClock clock) : IMerchantsService
{
    public async Task<MerchantOperationResult<MerchantResponse>> CreateMerchantAsync(CreateMerchantRequest request, MerchantActor actor, CancellationToken ct) =>
        await Run(async () =>
        {
            if (!actor.IsPlatformOperator && request.OwnerUserId != actor.UserId) return Forbidden<MerchantResponse>();
            if (!await identityUsers.IsActiveUserAsync(request.OwnerUserId, ct)) return Invalid<MerchantResponse>("owner_user_not_found");
            DateTime now = clock.UtcNow;
            Merchant merchant = Merchant.Create(MerchantId.New(), request.LegalName, request.DisplayName, request.Description, request.RegistrationNumber, request.TaxNumber, request.Email, request.PhoneNumber, request.OwnerUserId, now);
            MerchantEmployee owner = MerchantEmployee.Create(MerchantEmployeeId.New(), merchant.Id, request.OwnerUserId, null, MerchantMembershipRole.Owner, false, now);
            await merchants.AddAsync(merchant, ct); await employees.AddAsync(owner, ct); await db.SaveChangesAsync(ct);
            return MerchantOperation.Created(ToResponse(merchant));
        });

    public async Task<MerchantOperationResult<MerchantResponse>> GetMerchantAsync(Guid merchantId, MerchantActor actor, CancellationToken ct) =>
        await Run(async () =>
        {
            Merchant? merchant = await merchants.GetAsync(new MerchantId(merchantId), ct);
            if (merchant is null || !await CanAccess(merchant.Id, actor, null, ct)) return NotFound<MerchantResponse>();
            return MerchantOperation.Success(ToResponse(merchant));
        });

    public async Task<MerchantOperationResult<MerchantListResponse>> ListMerchantsAsync(int page, int pageSize, string? search, short? status, MerchantActor actor, CancellationToken ct) =>
        await Run(async () =>
        {
            if (page < 1 || pageSize is < 1 or > 100) return Invalid<MerchantListResponse>("invalid_pagination");
            IQueryable<Merchant> query = db.Merchants.AsNoTracking();
            if (!actor.IsPlatformOperator)
            {
                MerchantId[] ids = await db.Employees.AsNoTracking().Where(x => x.UserId == actor.UserId && x.Status == MerchantMembershipStatus.Active).Select(x => x.MerchantId).ToArrayAsync(ct);
                query = query.Where(x => ids.Contains(x.Id));
            }
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.DisplayName.Contains(search) || x.LegalName.Contains(search));
            if (status is not null)
            {
                if (!Enum.IsDefined((MerchantStatus)status.Value)) return Invalid<MerchantListResponse>("invalid_status");
                query = query.Where(x => x.Status == (MerchantStatus)status.Value);
            }
            int total = await query.CountAsync(ct);
            Merchant[] entities = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
            MerchantSummaryResponse[] items = entities.Select(x => new MerchantSummaryResponse(x.Id.Value, x.DisplayName, (short)x.Status, x.OwnerUserId, x.CreatedAtUtc)).ToArray();
            return MerchantOperation.Success(new MerchantListResponse(items, page, pageSize, total));
        });

    public async Task<MerchantOperationResult<MerchantResponse>> UpdateMerchantAsync(Guid merchantId, UpdateMerchantRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithMerchant(merchantId, actor, false, ct: ct, operation: async merchant =>
        {
            if (merchant.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantResponse>();
            merchant.UpdateProfile(request.LegalName, request.DisplayName, request.Description, request.RegistrationNumber, request.TaxNumber, request.Email, request.PhoneNumber, clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(merchant));
        });

    public Task<MerchantOperationResult<MerchantResponse>> ActivateMerchantAsync(Guid merchantId, Guid concurrencyStamp, MerchantActor actor, CancellationToken ct) =>
        Lifecycle(merchantId, concurrencyStamp, actor, ct: ct, transition: merchant => merchant.Activate(clock.UtcNow));
    public Task<MerchantOperationResult<MerchantResponse>> SuspendMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken ct) =>
        Lifecycle(merchantId, request.ConcurrencyStamp, actor, ct: ct, transition: merchant => merchant.Suspend(request.Reason, clock.UtcNow));
    public Task<MerchantOperationResult<MerchantResponse>> RejectMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken ct) =>
        Lifecycle(merchantId, request.ConcurrencyStamp, actor, ct: ct, transition: merchant => merchant.Reject(request.Reason, clock.UtcNow));
    public Task<MerchantOperationResult<MerchantResponse>> CloseMerchantAsync(Guid merchantId, ReasonRequest request, MerchantActor actor, CancellationToken ct) =>
        Lifecycle(merchantId, request.ConcurrencyStamp, actor, ct: ct, transition: merchant => merchant.Close(request.Reason, clock.UtcNow));

    public async Task<MerchantOperationResult<MerchantResponse>> ChangeOwnerAsync(Guid merchantId, ChangeMerchantOwnerRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithMerchant(merchantId, actor, false, ct: ct, operation: async merchant =>
        {
            if (merchant.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantResponse>();
            if (!await identityUsers.IsActiveUserAsync(request.OwnerUserId, ct)) return Invalid<MerchantResponse>("owner_user_not_found");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            MerchantEmployee? oldOwner = await employees.GetByUserAsync(merchant.Id, merchant.OwnerUserId, ct);
            MerchantEmployee? newOwner = await employees.GetByUserAsync(merchant.Id, request.OwnerUserId, ct);
            DateTime now = clock.UtcNow;
            if (oldOwner is null || oldOwner.Status != MerchantMembershipStatus.Active || oldOwner.Role != MerchantMembershipRole.Owner) return Conflict<MerchantResponse>("active_owner_membership_missing");
            oldOwner.ChangeRole(MerchantMembershipRole.Manager, now);
            await db.SaveChangesAsync(ct);
            if (newOwner is null)
            {
                newOwner = MerchantEmployee.Create(MerchantEmployeeId.New(), merchant.Id, request.OwnerUserId, null, MerchantMembershipRole.Owner, false, now);
                await employees.AddAsync(newOwner, ct);
            }
            else
            {
                if (newOwner.Status != MerchantMembershipStatus.Active) return Conflict<MerchantResponse>("new_owner_membership_not_active");
                newOwner.AssignBranch(null, now);
                newOwner.ChangeRole(MerchantMembershipRole.Owner, now);
            }
            merchant.ChangeOwner(request.OwnerUserId, now);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return MerchantOperation.Success(ToResponse(merchant));
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> CreateBranchAsync(Guid merchantId, CreateMerchantBranchRequest request, MerchantActor actor, CancellationToken ct) =>
        await Run(async () =>
        {
            MerchantId id = new(merchantId);
            Merchant? merchant = await merchants.GetAsync(id, ct);
            if (merchant is null || !await CanAccess(id, actor, null, ct)) return NotFound<MerchantBranchResponse>();
            if (request.Code is not null && await db.Branches.AnyAsync(x => x.MerchantId == id && x.Code == request.Code.Trim(), ct)) return Conflict<MerchantBranchResponse>("branch_code_exists");
            bool hasBranches = await db.Branches.AnyAsync(x => x.MerchantId == id && x.Status != MerchantBranchStatus.Closed, ct);
            bool primary = !hasBranches || request.IsPrimary;
            if (primary) await ClearPrimary(id, null, clock.UtcNow, ct);
            MerchantBranch branch = MerchantBranch.Create(MerchantBranchId.New(), id, request.Name, request.Code, request.PhoneNumber, request.Email, Address(request.Address), Coordinate(request.Location), request.TimeZone, primary, clock.UtcNow);
            await branches.AddAsync(branch, ct); await db.SaveChangesAsync(ct);
            return MerchantOperation.Created(ToResponse(branch));
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> GetBranchAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: branch => Task.FromResult(MerchantOperation.Success(ToResponse(branch))));

    public async Task<MerchantOperationResult<IReadOnlyList<MerchantBranchResponse>>> ListBranchesAsync(Guid merchantId, MerchantActor actor, CancellationToken ct) =>
        await Run<IReadOnlyList<MerchantBranchResponse>>(async () =>
        {
            MerchantId id = new(merchantId);
            if (!await db.Merchants.AnyAsync(x => x.Id == id, ct) || !await CanAccess(id, actor, null, ct)) return NotFound<IReadOnlyList<MerchantBranchResponse>>();
            MerchantEmployee? membership = actor.IsPlatformOperator ? null : await employees.GetByUserAsync(id, actor.UserId, ct);
            IQueryable<MerchantBranch> query = db.Branches.AsNoTracking().Where(x => x.MerchantId == id);
            if (membership?.BranchId is not null) query = query.Where(x => x.Id == membership.BranchId.Value);
            MerchantBranch[] entities = await query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name).ToArrayAsync(ct);
            MerchantBranchResponse[] values = entities.Select(ToResponse).ToArray();
            return MerchantOperation.Success<IReadOnlyList<MerchantBranchResponse>>(values);
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> UpdateBranchAsync(Guid merchantId, Guid branchId, UpdateMerchantBranchRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantBranchResponse>();
            if (request.Code is not null && await db.Branches.AnyAsync(x => x.MerchantId == branch.MerchantId && x.Id != branch.Id && x.Code == request.Code.Trim(), ct)) return Conflict<MerchantBranchResponse>("branch_code_exists");
            branch.Update(request.Name, request.Code, request.PhoneNumber, request.Email, Address(request.Address), request.TimeZone, clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(branch));
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> UpdateBranchLocationAsync(Guid merchantId, Guid branchId, UpdateMerchantBranchLocationRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantBranchResponse>();
            branch.ChangeLocation(new GeoCoordinate(request.Latitude, request.Longitude), clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(branch));
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> ChangeBranchStatusAsync(Guid merchantId, Guid branchId, string operation, BranchStatusRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantBranchResponse>();
            Merchant merchant = (await merchants.GetAsync(branch.MerchantId, ct))!;
            DateTime now = clock.UtcNow;
            switch (operation)
            {
                case "activate": branch.Activate(merchant.Status == MerchantStatus.Active, now); break;
                case "temporary-close": branch.TemporarilyClose(request.Reason, now); break;
                case "reopen": branch.Reopen(merchant.Status == MerchantStatus.Active, now); break;
                case "suspend": branch.Suspend(request.Reason ?? string.Empty, now); break;
                case "close":
                    bool wasPrimary = branch.IsPrimary; branch.Close(request.Reason ?? string.Empty, now);
                    if (wasPrimary)
                    {
                        MerchantBranch? replacement = await db.Branches.Where(x => x.MerchantId == branch.MerchantId && x.Id != branch.Id && x.Status != MerchantBranchStatus.Closed).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
                        replacement?.SetPrimary(true, now);
                    }
                    break;
                default: return Invalid<MerchantBranchResponse>("invalid_branch_operation");
            }
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(branch));
        });

    public async Task<MerchantOperationResult<MerchantBranchResponse>> SetPrimaryBranchAsync(Guid merchantId, Guid branchId, SetPrimaryBranchRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantBranchResponse>();
            DateTime now = clock.UtcNow; await ClearPrimary(branch.MerchantId, branch.Id, now, ct); branch.SetPrimary(true, now);
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(branch));
        });

    public async Task<MerchantOperationResult<BusinessHoursResponse>> GetBusinessHoursAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: branch => Task.FromResult(MerchantOperation.Success(ToBusinessHours(branch))));

    public async Task<MerchantOperationResult<BusinessHoursResponse>> ReplaceBusinessHoursAsync(Guid merchantId, Guid branchId, ReplaceBusinessHoursRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<BusinessHoursResponse>();
            branch.ReplaceBusinessHours(request.Days.Select(x => (x.DayOfWeek, x.ClosedAllDay, x.Periods.Select(p => new OpeningPeriod(p.OpensAt, p.ClosesAt)))), clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToBusinessHours(branch));
        });

    public async Task<MerchantOperationResult<ScheduleOverrideResponse>> AddClosureAsync(Guid merchantId, Guid branchId, AddExceptionalClosureRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<ScheduleOverrideResponse>();
            BranchScheduleOverride value = branch.AddClosure(request.StartDate, request.EndDate, request.Reason, clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Created(ToResponse(value));
        });

    public async Task<MerchantOperationResult<ScheduleOverrideResponse>> SetSpecialHoursAsync(Guid merchantId, Guid branchId, SetSpecialHoursRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<ScheduleOverrideResponse>();
            BranchScheduleOverride value = branch.SetSpecialHours(request.Date, request.Periods.Select(x => new OpeningPeriod(x.OpensAt, x.ClosesAt)), request.Reason, clock.UtcNow);
            await db.SaveChangesAsync(ct); return MerchantOperation.Created(ToResponse(value));
        });

    public async Task<MerchantOperationResult<IReadOnlyList<ScheduleOverrideResponse>>> ListScheduleOverridesAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch<IReadOnlyList<ScheduleOverrideResponse>>(merchantId, branchId, actor, ct: ct, operation: branch =>
            Task.FromResult(MerchantOperation.Success<IReadOnlyList<ScheduleOverrideResponse>>(branch.ScheduleOverrides.OrderBy(x => x.StartDate).Select(ToResponse).ToArray())));

    public async Task<MerchantOperationResult<bool>> CancelScheduleOverrideAsync(Guid merchantId, Guid branchId, Guid overrideId, CancelScheduleOverrideRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            if (branch.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<bool>();
            branch.CancelOverride(new ScheduleOverrideId(overrideId), clock.UtcNow); await db.SaveChangesAsync(ct); return MerchantOperation.Success(true);
        });

    public async Task<MerchantOperationResult<BranchAvailabilityResponse>> GetAvailabilityAsync(Guid merchantId, Guid branchId, DateTime atUtc, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: branch =>
        {
            BranchAvailability value = branch.GetAvailability(atUtc);
            return Task.FromResult(MerchantOperation.Success(new BranchAvailabilityResponse(branch.Id.Value, atUtc, value.IsOpen, value.LocalDate, value.Source)));
        });

    public async Task<MerchantOperationResult<BranchServiceAreaResponse>> AssignServiceAreaAsync(Guid merchantId, Guid branchId, Guid serviceAreaId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            ServiceAreaDetails? area = await maps.GetServiceAreaAsync(serviceAreaId, ct);
            if (area is null) return NotFound<BranchServiceAreaResponse>();
            if (!area.IsActive) return Invalid<BranchServiceAreaResponse>("service_area_inactive");
            DateTime now = clock.UtcNow; branch.AssignServiceArea(serviceAreaId, now); await db.SaveChangesAsync(ct);
            return MerchantOperation.Created(new BranchServiceAreaResponse(area.Id, area.Name, area.IsActive, now));
        });

    public async Task<MerchantOperationResult<bool>> RemoveServiceAreaAsync(Guid merchantId, Guid branchId, Guid serviceAreaId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            branch.RemoveServiceArea(serviceAreaId, clock.UtcNow); await db.SaveChangesAsync(ct); return MerchantOperation.Success(true);
        });

    public async Task<MerchantOperationResult<IReadOnlyList<BranchServiceAreaResponse>>> ListServiceAreasAsync(Guid merchantId, Guid branchId, MerchantActor actor, CancellationToken ct) =>
        await WithBranch<IReadOnlyList<BranchServiceAreaResponse>>(merchantId, branchId, actor, ct: ct, operation: async branch =>
        {
            var result = new List<BranchServiceAreaResponse>();
            foreach (BranchServiceArea assignment in branch.ServiceAreas)
            {
                ServiceAreaDetails? area = await maps.GetServiceAreaAsync(assignment.ServiceAreaId, ct);
                if (area is not null) result.Add(new BranchServiceAreaResponse(area.Id, area.Name, area.IsActive, assignment.AssignedAtUtc));
            }
            return MerchantOperation.Success<IReadOnlyList<BranchServiceAreaResponse>>(result);
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> AddEmployeeAsync(Guid merchantId, AddMerchantEmployeeRequest request, MerchantActor actor, CancellationToken ct) =>
        await Run(async () =>
        {
            MerchantId id = new(merchantId);
            if (!await CanManageEmployees(id, actor, ct)) return NotFound<MerchantEmployeeResponse>();
            if (request.Role == (short)MerchantMembershipRole.Owner) return Invalid<MerchantEmployeeResponse>("use_change_owner");
            if (!Enum.IsDefined((MerchantMembershipRole)request.Role)) return Invalid<MerchantEmployeeResponse>("invalid_membership_role");
            if (!await identityUsers.IsActiveUserAsync(request.UserId, ct)) return Invalid<MerchantEmployeeResponse>("user_not_found");
            if (await employees.GetByUserAsync(id, request.UserId, ct) is not null) return Conflict<MerchantEmployeeResponse>("membership_exists");
            MerchantBranchId? branchIdValue = request.BranchId is null ? null : new MerchantBranchId(request.BranchId.Value);
            if (branchIdValue is not null && !await db.Branches.AnyAsync(x => x.MerchantId == id && x.Id == branchIdValue.Value && x.Status != MerchantBranchStatus.Closed, ct)) return Invalid<MerchantEmployeeResponse>("branch_not_found");
            MerchantEmployee value = MerchantEmployee.Create(MerchantEmployeeId.New(), id, request.UserId, branchIdValue, (MerchantMembershipRole)request.Role, request.Invited, clock.UtcNow);
            await employees.AddAsync(value, ct); await db.SaveChangesAsync(ct); return MerchantOperation.Created(ToResponse(value));
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> GetEmployeeAsync(Guid merchantId, Guid employeeId, MerchantActor actor, CancellationToken ct) =>
        await WithEmployee(merchantId, employeeId, actor, false, ct: ct, operation: employee => Task.FromResult(MerchantOperation.Success(ToResponse(employee))));

    public async Task<MerchantOperationResult<IReadOnlyList<MerchantEmployeeResponse>>> ListEmployeesAsync(Guid merchantId, MerchantActor actor, CancellationToken ct) =>
        await Run<IReadOnlyList<MerchantEmployeeResponse>>(async () =>
        {
            MerchantId id = new(merchantId);
            if (!await CanAccess(id, actor, null, ct)) return NotFound<IReadOnlyList<MerchantEmployeeResponse>>();
            MerchantEmployee[] entities = await db.Employees.AsNoTracking().Where(x => x.MerchantId == id).OrderBy(x => x.Role).ThenBy(x => x.CreatedAtUtc).ToArrayAsync(ct);
            MerchantEmployeeResponse[] values = entities.Select(ToResponse).ToArray();
            return MerchantOperation.Success<IReadOnlyList<MerchantEmployeeResponse>>(values);
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> ChangeEmployeeStatusAsync(Guid merchantId, Guid employeeId, string operation, MerchantEmployeeActionRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithEmployee(merchantId, employeeId, actor, true, ct: ct, operation: async employee =>
        {
            if (employee.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantEmployeeResponse>();
            if (employee.UserId == actor.UserId && !actor.IsPlatformOperator) return Forbidden<MerchantEmployeeResponse>("self_membership_change_denied");
            if (employee.Role == MerchantMembershipRole.Owner && operation is "suspend" or "remove") return Conflict<MerchantEmployeeResponse>("active_owner_required");
            switch (operation)
            {
                case "activate": employee.Activate(clock.UtcNow); break;
                case "suspend": employee.Suspend(clock.UtcNow); break;
                case "remove": employee.Remove(clock.UtcNow); break;
                default: return Invalid<MerchantEmployeeResponse>("invalid_employee_operation");
            }
            await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(employee));
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> ChangeEmployeeRoleAsync(Guid merchantId, Guid employeeId, ChangeMerchantEmployeeRoleRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithEmployee(merchantId, employeeId, actor, true, ct: ct, operation: async employee =>
        {
            if (employee.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantEmployeeResponse>();
            if (employee.UserId == actor.UserId && !actor.IsPlatformOperator) return Forbidden<MerchantEmployeeResponse>("self_role_change_denied");
            if (employee.Role == MerchantMembershipRole.Owner || request.Role == (short)MerchantMembershipRole.Owner) return Conflict<MerchantEmployeeResponse>("use_change_owner");
            if (!Enum.IsDefined((MerchantMembershipRole)request.Role)) return Invalid<MerchantEmployeeResponse>("invalid_membership_role");
            employee.ChangeRole((MerchantMembershipRole)request.Role, clock.UtcNow); await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(employee));
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> AssignEmployeeBranchAsync(Guid merchantId, Guid employeeId, AssignMerchantEmployeeBranchRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithEmployee(merchantId, employeeId, actor, true, ct: ct, operation: async employee =>
        {
            if (employee.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantEmployeeResponse>();
            MerchantBranchId branchId = new(request.BranchId);
            if (!await db.Branches.AnyAsync(x => x.MerchantId == employee.MerchantId && x.Id == branchId && x.Status != MerchantBranchStatus.Closed, ct)) return Invalid<MerchantEmployeeResponse>("branch_not_found");
            employee.AssignBranch(branchId, clock.UtcNow); await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(employee));
        });

    public async Task<MerchantOperationResult<MerchantEmployeeResponse>> RemoveEmployeeBranchRestrictionAsync(Guid merchantId, Guid employeeId, MerchantEmployeeActionRequest request, MerchantActor actor, CancellationToken ct) =>
        await WithEmployee(merchantId, employeeId, actor, true, ct: ct, operation: async employee =>
        {
            if (employee.ConcurrencyStamp != request.ConcurrencyStamp) return Conflict<MerchantEmployeeResponse>();
            employee.AssignBranch(null, clock.UtcNow); await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(employee));
        });

    private async Task<MerchantOperationResult<MerchantResponse>> Lifecycle(Guid id, Guid stamp, MerchantActor actor, Action<Merchant> transition, CancellationToken ct) =>
        await WithMerchant(id, actor, true, ct: ct, operation: async merchant =>
        {
            if (merchant.ConcurrencyStamp != stamp) return Conflict<MerchantResponse>();
            transition(merchant); await db.SaveChangesAsync(ct); return MerchantOperation.Success(ToResponse(merchant));
        });

    private async Task<MerchantOperationResult<T>> WithMerchant<T>(Guid id, MerchantActor actor, bool platformOnly, Func<Merchant, Task<MerchantOperationResult<T>>> operation, CancellationToken ct) =>
        await Run(async () =>
        {
            Merchant? merchant = await merchants.GetAsync(new MerchantId(id), ct);
            if (merchant is null || (platformOnly ? !actor.IsPlatformOperator : !await CanAccess(merchant.Id, actor, null, ct))) return NotFound<T>();
            return await operation(merchant);
        });

    private async Task<MerchantOperationResult<T>> WithBranch<T>(Guid merchantId, Guid branchId, MerchantActor actor, Func<MerchantBranch, Task<MerchantOperationResult<T>>> operation, CancellationToken ct) =>
        await Run(async () =>
        {
            MerchantId mid = new(merchantId); MerchantBranchId bid = new(branchId);
            MerchantBranch? branch = await branches.GetAsync(mid, bid, ct);
            if (branch is null || !await CanAccess(mid, actor, bid, ct)) return NotFound<T>();
            return await operation(branch);
        });

    private async Task<MerchantOperationResult<T>> WithEmployee<T>(Guid merchantId, Guid employeeId, MerchantActor actor, bool manage, Func<MerchantEmployee, Task<MerchantOperationResult<T>>> operation, CancellationToken ct) =>
        await Run(async () =>
        {
            MerchantId mid = new(merchantId);
            bool allowed = manage ? await CanManageEmployees(mid, actor, ct) : await CanAccess(mid, actor, null, ct);
            MerchantEmployee? employee = allowed ? await employees.GetAsync(mid, new MerchantEmployeeId(employeeId), ct) : null;
            return employee is null ? NotFound<T>() : await operation(employee);
        });

    private async Task<bool> CanAccess(MerchantId merchantId, MerchantActor actor, MerchantBranchId? branchId, CancellationToken ct)
    {
        if (actor.IsPlatformOperator) return true;
        MerchantEmployee? membership = await employees.GetByUserAsync(merchantId, actor.UserId, ct);
        return membership?.Status == MerchantMembershipStatus.Active && (branchId is null || membership.BranchId is null || membership.BranchId == branchId);
    }

    private async Task<bool> CanManageEmployees(MerchantId merchantId, MerchantActor actor, CancellationToken ct)
    {
        if (actor.IsPlatformOperator) return true;
        MerchantEmployee? membership = await employees.GetByUserAsync(merchantId, actor.UserId, ct);
        return membership is { Status: MerchantMembershipStatus.Active, BranchId: null, Role: MerchantMembershipRole.Owner or MerchantMembershipRole.Manager };
    }

    private async Task ClearPrimary(MerchantId merchantId, MerchantBranchId? except, DateTime now, CancellationToken ct)
    {
        MerchantBranch[] values = await db.Branches.Where(x => x.MerchantId == merchantId && x.IsPrimary && (except == null || x.Id != except.Value)).ToArrayAsync(ct);
        foreach (MerchantBranch branch in values) branch.SetPrimary(false, now);
    }

    private static async Task<MerchantOperationResult<T>> Run<T>(Func<Task<MerchantOperationResult<T>>> operation)
    {
        try { return await operation(); }
        catch (DomainException) { return Invalid<T>("domain_rule_violation"); }
        catch (DbUpdateConcurrencyException) { return Conflict<T>(); }
        catch (DbUpdateException) { return Conflict<T>("persistence_constraint_violation"); }
    }

    private static BranchAddress Address(BranchAddressRequest value) => BranchAddress.Create(value.City, value.Area, value.Street, value.BuildingNumber, value.PostalCode);
    private static GeoCoordinate Coordinate(CoordinateRequest value) => new(value.Latitude, value.Longitude);
    private static MerchantOperationResult<T> NotFound<T>() => MerchantOperation.Failure<T>(MerchantOperationStatus.NotFound, "resource_not_found");
    private static MerchantOperationResult<T> Invalid<T>(string code) => MerchantOperation.Failure<T>(MerchantOperationStatus.Invalid, code);
    private static MerchantOperationResult<T> Conflict<T>(string code = "concurrency_conflict") => MerchantOperation.Failure<T>(MerchantOperationStatus.Conflict, code);
    private static MerchantOperationResult<T> Forbidden<T>(string code = "forbidden") => MerchantOperation.Failure<T>(MerchantOperationStatus.Forbidden, code);

    private static MerchantResponse ToResponse(Merchant x) => new(x.Id.Value, x.LegalName, x.DisplayName, x.Description, x.RegistrationNumber, x.TaxNumber, x.Email, x.PhoneNumber, x.OwnerUserId, (short)x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.ActivatedAtUtc, x.SuspendedAtUtc, x.SuspensionReason, x.RejectedAtUtc, x.RejectionReason, x.ClosedAtUtc, x.ClosingReason, x.ConcurrencyStamp);
    private static MerchantBranchResponse ToResponse(MerchantBranch x) => new(x.Id.Value, x.MerchantId.Value, x.Name, x.Code, x.PhoneNumber, x.Email, new(x.Address.City, x.Address.Area, x.Address.Street, x.Address.BuildingNumber, x.Address.PostalCode), new(x.Location.Latitude, x.Location.Longitude), (short)x.Status, x.IsPrimary, x.TimeZone, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static MerchantEmployeeResponse ToResponse(MerchantEmployee x) => new(x.Id.Value, x.MerchantId.Value, x.UserId, x.BranchId?.Value, (short)x.Role, (short)x.Status, x.JoinedAtUtc, x.SuspendedAtUtc, x.RemovedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static BusinessHoursResponse ToBusinessHours(MerchantBranch x) => new(x.Id.Value, x.BusinessHours.OrderBy(d => d.DayOfWeek).Select(d => new BusinessDayResponse(d.DayOfWeek, d.ClosedAllDay, d.Periods.OrderBy(p => p.OpensAt).Select(p => new OpeningPeriodResponse(p.Id.Value, p.OpensAt, p.ClosesAt)).ToArray())).ToArray());
    private static ScheduleOverrideResponse ToResponse(BranchScheduleOverride x) => new(x.Id.Value, x.StartDate, x.EndDate, x.IsClosed, x.Reason, x.CreatedAtUtc, x.CancelledAtUtc, x.Periods.OrderBy(p => p.OpensAt).Select(p => new OpeningPeriodResponse(p.Id.Value, p.OpensAt, p.ClosesAt)).ToArray());
}

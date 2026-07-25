using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder root = endpoints.MapGroup("/api/v1/merchants").WithTags("Merchants").RequireAuthorization();
        root.MapPost("/", Create).RequireAuthorization(Permission(MerchantPermissions.Create));
        root.MapGet("/", List).RequireAuthorization(Permission(MerchantPermissions.View));
        root.MapGet("/{merchantId:guid}", Get).RequireAuthorization(Permission(MerchantPermissions.View));
        root.MapPut("/{merchantId:guid}", Update).RequireAuthorization(Permission(MerchantPermissions.Update));
        root.MapPost("/{merchantId:guid}/activate", Activate).RequireAuthorization(Permission(MerchantPermissions.LifecycleManage));
        root.MapPost("/{merchantId:guid}/suspend", Suspend).RequireAuthorization(Permission(MerchantPermissions.LifecycleManage));
        root.MapPost("/{merchantId:guid}/reject", Reject).RequireAuthorization(Permission(MerchantPermissions.LifecycleManage));
        root.MapPost("/{merchantId:guid}/close", Close).RequireAuthorization(Permission(MerchantPermissions.LifecycleManage));
        root.MapPost("/{merchantId:guid}/change-owner", ChangeOwner).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));

        RouteGroupBuilder branch = root.MapGroup("/{merchantId:guid}/branches");
        branch.MapPost("/", CreateBranch).RequireAuthorization(Permission(MerchantPermissions.BranchesManage));
        branch.MapGet("/", ListBranches).RequireAuthorization(Permission(MerchantPermissions.BranchesView));
        branch.MapGet("/{branchId:guid}", GetBranch).RequireAuthorization(Permission(MerchantPermissions.BranchesView));
        branch.MapPut("/{branchId:guid}", UpdateBranch).RequireAuthorization(Permission(MerchantPermissions.BranchesManage));
        branch.MapPut("/{branchId:guid}/location", UpdateLocation).RequireAuthorization(Permission(MerchantPermissions.BranchesManage));
        foreach (string operation in new[] { "activate", "temporary-close", "reopen", "suspend", "close" })
            branch.MapPost($"/{{branchId:guid}}/{operation}", (Guid merchantId, Guid branchId, BranchStatusRequest request, ICurrentUser current, IMerchantsService service, CancellationToken ct) =>
                Run(service.ChangeBranchStatusAsync(merchantId, branchId, operation, request, Actor(current), ct))).RequireAuthorization(Permission(MerchantPermissions.BranchesManage));
        branch.MapPost("/{branchId:guid}/set-primary", SetPrimary).RequireAuthorization(Permission(MerchantPermissions.BranchesManage));

        branch.MapGet("/{branchId:guid}/business-hours", GetHours).RequireAuthorization(Permission(MerchantPermissions.BranchesView));
        branch.MapPut("/{branchId:guid}/business-hours", ReplaceHours).RequireAuthorization(Permission(MerchantPermissions.BusinessHoursManage));
        branch.MapGet("/{branchId:guid}/schedule-overrides", ListOverrides).RequireAuthorization(Permission(MerchantPermissions.BranchesView));
        branch.MapPost("/{branchId:guid}/closures", AddClosure).RequireAuthorization(Permission(MerchantPermissions.BusinessHoursManage));
        branch.MapPost("/{branchId:guid}/special-hours", SetSpecialHours).RequireAuthorization(Permission(MerchantPermissions.BusinessHoursManage));
        branch.MapDelete("/{branchId:guid}/schedule-overrides/{overrideId:guid}", CancelOverride).RequireAuthorization(Permission(MerchantPermissions.BusinessHoursManage));
        branch.MapGet("/{branchId:guid}/availability", Availability).RequireAuthorization(Permission(MerchantPermissions.BranchesView));

        branch.MapGet("/{branchId:guid}/service-areas", ListServiceAreas).RequireAuthorization(Permission(MerchantPermissions.BranchesView));
        branch.MapPost("/{branchId:guid}/service-areas/{serviceAreaId:guid}", AssignServiceArea).RequireAuthorization(Permission(MerchantPermissions.ServiceAreasManage));
        branch.MapDelete("/{branchId:guid}/service-areas/{serviceAreaId:guid}", RemoveServiceArea).RequireAuthorization(Permission(MerchantPermissions.ServiceAreasManage));

        RouteGroupBuilder employee = root.MapGroup("/{merchantId:guid}/employees");
        employee.MapGet("/", ListEmployees).RequireAuthorization(Permission(MerchantPermissions.EmployeesView));
        employee.MapGet("/{employeeId:guid}", GetEmployee).RequireAuthorization(Permission(MerchantPermissions.EmployeesView));
        employee.MapPost("/", AddEmployee).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));
        employee.MapPatch("/{employeeId:guid}/role", ChangeRole).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));
        foreach (string operation in new[] { "activate", "suspend", "remove" })
            employee.MapPost($"/{{employeeId:guid}}/{operation}", (Guid merchantId, Guid employeeId, MerchantEmployeeActionRequest request, ICurrentUser current, IMerchantsService service, CancellationToken ct) =>
                Run(service.ChangeEmployeeStatusAsync(merchantId, employeeId, operation, request, Actor(current), ct))).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));
        employee.MapPost("/{employeeId:guid}/assign-branch", AssignEmployeeBranch).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));
        employee.MapPost("/{employeeId:guid}/remove-branch-restriction", RemoveEmployeeBranch).RequireAuthorization(Permission(MerchantPermissions.EmployeesManage));
        return endpoints;
    }

    private static Task<IResult> Create(CreateMerchantRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.CreateMerchantAsync(r, Actor(c), ct));
    private static Task<IResult> List(int page, int pageSize, string? search, short? status, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ListMerchantsAsync(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, search, status, Actor(c), ct));
    private static Task<IResult> Get(Guid merchantId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.GetMerchantAsync(merchantId, Actor(c), ct));
    private static Task<IResult> Update(Guid merchantId, UpdateMerchantRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.UpdateMerchantAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> Activate(Guid merchantId, MerchantEmployeeActionRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ActivateMerchantAsync(merchantId, r.ConcurrencyStamp, Actor(c), ct));
    private static Task<IResult> Suspend(Guid merchantId, ReasonRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.SuspendMerchantAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> Reject(Guid merchantId, ReasonRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.RejectMerchantAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> Close(Guid merchantId, ReasonRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.CloseMerchantAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> ChangeOwner(Guid merchantId, ChangeMerchantOwnerRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ChangeOwnerAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> CreateBranch(Guid merchantId, CreateMerchantBranchRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.CreateBranchAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> ListBranches(Guid merchantId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ListBranchesAsync(merchantId, Actor(c), ct));
    private static Task<IResult> GetBranch(Guid merchantId, Guid branchId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.GetBranchAsync(merchantId, branchId, Actor(c), ct));
    private static Task<IResult> UpdateBranch(Guid merchantId, Guid branchId, UpdateMerchantBranchRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.UpdateBranchAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> UpdateLocation(Guid merchantId, Guid branchId, UpdateMerchantBranchLocationRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.UpdateBranchLocationAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> SetPrimary(Guid merchantId, Guid branchId, SetPrimaryBranchRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.SetPrimaryBranchAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> GetHours(Guid merchantId, Guid branchId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.GetBusinessHoursAsync(merchantId, branchId, Actor(c), ct));
    private static Task<IResult> ReplaceHours(Guid merchantId, Guid branchId, ReplaceBusinessHoursRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ReplaceBusinessHoursAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> ListOverrides(Guid merchantId, Guid branchId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ListScheduleOverridesAsync(merchantId, branchId, Actor(c), ct));
    private static Task<IResult> AddClosure(Guid merchantId, Guid branchId, AddExceptionalClosureRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.AddClosureAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> SetSpecialHours(Guid merchantId, Guid branchId, SetSpecialHoursRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.SetSpecialHoursAsync(merchantId, branchId, r, Actor(c), ct));
    private static Task<IResult> CancelOverride(
        [FromRoute] Guid merchantId,
        [FromRoute] Guid branchId,
        [FromRoute] Guid overrideId,
        [FromQuery] Guid concurrencyStamp,
        [FromServices] ICurrentUser c,
        [FromServices] IMerchantsService s,
        CancellationToken ct) =>
        Run(
            s.CancelScheduleOverrideAsync(
                merchantId,
                branchId,
                overrideId,
                new CancelScheduleOverrideRequest(concurrencyStamp),
                Actor(c),
                ct),
            true);
    private static Task<IResult> Availability(Guid merchantId, Guid branchId, [FromQuery] DateTime atUtc, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.GetAvailabilityAsync(merchantId, branchId, atUtc, Actor(c), ct));
    private static Task<IResult> ListServiceAreas(Guid merchantId, Guid branchId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ListServiceAreasAsync(merchantId, branchId, Actor(c), ct));
    private static Task<IResult> AssignServiceArea(Guid merchantId, Guid branchId, Guid serviceAreaId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.AssignServiceAreaAsync(merchantId, branchId, serviceAreaId, Actor(c), ct));
    private static Task<IResult> RemoveServiceArea(
        [FromRoute] Guid merchantId,
        [FromRoute] Guid branchId,
        [FromRoute] Guid serviceAreaId,
        [FromServices] ICurrentUser c,
        [FromServices] IMerchantsService s,
        CancellationToken ct) =>
        Run(s.RemoveServiceAreaAsync(merchantId, branchId, serviceAreaId, Actor(c), ct), true);
    private static Task<IResult> ListEmployees(Guid merchantId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ListEmployeesAsync(merchantId, Actor(c), ct));
    private static Task<IResult> GetEmployee(Guid merchantId, Guid employeeId, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.GetEmployeeAsync(merchantId, employeeId, Actor(c), ct));
    private static Task<IResult> AddEmployee(Guid merchantId, AddMerchantEmployeeRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.AddEmployeeAsync(merchantId, r, Actor(c), ct));
    private static Task<IResult> ChangeRole(Guid merchantId, Guid employeeId, ChangeMerchantEmployeeRoleRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.ChangeEmployeeRoleAsync(merchantId, employeeId, r, Actor(c), ct));
    private static Task<IResult> AssignEmployeeBranch(Guid merchantId, Guid employeeId, AssignMerchantEmployeeBranchRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.AssignEmployeeBranchAsync(merchantId, employeeId, r, Actor(c), ct));
    private static Task<IResult> RemoveEmployeeBranch(Guid merchantId, Guid employeeId, MerchantEmployeeActionRequest r, ICurrentUser c, IMerchantsService s, CancellationToken ct) => Run(s.RemoveEmployeeBranchRestrictionAsync(merchantId, employeeId, r, Actor(c), ct));

    private static MerchantActor Actor(ICurrentUser current)
    {
        bool platform = current.Roles.Any(x => x.Equals("admin", StringComparison.OrdinalIgnoreCase) || x.Equals("platform-admin", StringComparison.OrdinalIgnoreCase) || x.Equals("operations", StringComparison.OrdinalIgnoreCase));
        return new(current.UserId?.Value ?? Guid.Empty, platform);
    }
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Run<T>(Task<MerchantOperationResult<T>> operation, bool noContent = false)
    {
        MerchantOperationResult<T> result = await operation;
        return result.Status switch
        {
            MerchantOperationStatus.Success when noContent => Results.NoContent(),
            MerchantOperationStatus.Success => Results.Ok(result.Value),
            MerchantOperationStatus.Created => Results.Json(result.Value, statusCode: StatusCodes.Status201Created),
            MerchantOperationStatus.NotFound => Problem(404, result.ErrorCode),
            MerchantOperationStatus.Forbidden => Problem(403, result.ErrorCode),
            MerchantOperationStatus.Conflict => Problem(409, result.ErrorCode),
            _ => Problem(400, result.ErrorCode),
        };
    }
    private static IResult Problem(int status, string? code) => Results.Problem(statusCode: status, title: status switch { 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Invalid request" }, extensions: new Dictionary<string, object?> { ["code"] = code });
}

using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Identity.Application;

namespace AlSsareea.Api.Endpoints;

internal static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/drivers").WithTags("Drivers").RequireAuthorization();
        group.MapPost("/me", Create).RequireAuthorization(Permission(DriverPermissions.ProfileManageSelf)).RequireRateLimiting("drivers-write").WithName("CreateDriver").Produces<DriverProfileResponse>(201);
        group.MapGet("/me", GetMy).RequireAuthorization(Permission(DriverPermissions.ProfileReadSelf)).WithName("GetMyDriver");
        group.MapPut("/me", UpdateMy).RequireAuthorization(Permission(DriverPermissions.ProfileManageSelf)).RequireRateLimiting("drivers-write").WithName("UpdateMyDriver");
        group.MapPost("/me/submit-review", SubmitReview).RequireAuthorization(Permission(DriverPermissions.ProfileManageSelf)).RequireRateLimiting("drivers-write").WithName("SubmitDriverReview");
        group.MapGet("/", List).RequireAuthorization(Permission(DriverPermissions.ProfileRead)).WithName("ListDrivers");
        group.MapGet("/{driverId:guid}", Get).RequireAuthorization(Permission(DriverPermissions.ProfileRead)).WithName("GetDriver");
        MapTransition(group, "approve", DriverPermissions.ReviewManage); MapTransition(group, "reject", DriverPermissions.ReviewManage); MapTransition(group, "activate", DriverPermissions.ActivationManage); MapTransition(group, "deactivate", DriverPermissions.ActivationManage); MapTransition(group, "archive", DriverPermissions.ProfileManage);

        group.MapPost("/me/vehicles", AddVehicle).RequireAuthorization(Permission(DriverPermissions.VehiclesManageSelf)).RequireRateLimiting("drivers-write").WithName("AddDriverVehicle");
        group.MapPost("/me/vehicles/{vehicleId:guid}/set-primary", SetPrimaryVehicle).RequireAuthorization(Permission(DriverPermissions.VehiclesManageSelf)).RequireRateLimiting("drivers-write").WithName("SetPrimaryDriverVehicle");
        group.MapPost("/{driverId:guid}/vehicles/{vehicleId:guid}/approve", (Guid driverId, Guid vehicleId, VehicleReviewRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ReviewVehicleAsync(Actor(user, context), driverId, vehicleId, true, request, Key(context), ct))).RequireAuthorization(Permission(DriverPermissions.VehiclesManage)).WithName("ApproveDriverVehicle");
        group.MapPost("/{driverId:guid}/vehicles/{vehicleId:guid}/reject", (Guid driverId, Guid vehicleId, VehicleReviewRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ReviewVehicleAsync(Actor(user, context), driverId, vehicleId, false, request, Key(context), ct))).RequireAuthorization(Permission(DriverPermissions.VehiclesManage)).WithName("RejectDriverVehicle");

        group.MapPost("/me/documents", SubmitDocument).RequireAuthorization(Permission(DriverPermissions.DocumentsManageSelf)).RequireRateLimiting("drivers-write").WithName("SubmitDriverDocument");
        group.MapPost("/{driverId:guid}/documents/{documentId:guid}/approve", (Guid driverId, Guid documentId, DocumentReviewRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ReviewDocumentAsync(Actor(user, context), driverId, documentId, true, request, Key(context), ct))).RequireAuthorization(Permission(DriverPermissions.DocumentsReview)).WithName("ApproveDriverDocument");
        group.MapPost("/{driverId:guid}/documents/{documentId:guid}/reject", (Guid driverId, Guid documentId, DocumentReviewRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ReviewDocumentAsync(Actor(user, context), driverId, documentId, false, request, Key(context), ct))).RequireAuthorization(Permission(DriverPermissions.DocumentsReview)).WithName("RejectDriverDocument");

        group.MapPost("/{driverId:guid}/zones", AssignZone).RequireAuthorization(Permission(DriverPermissions.ZonesManage)).RequireRateLimiting("drivers-write").WithName("AssignDriverZone");
        group.MapDelete("/{driverId:guid}/zones/{zoneId:guid}", RemoveZone).RequireAuthorization(Permission(DriverPermissions.ZonesManage)).RequireRateLimiting("drivers-write").WithName("RemoveDriverZone");
        group.MapPost("/me/availability/online", (HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Availability("online", c, u, s, ct)).RequireAuthorization(Permission(DriverPermissions.AvailabilityManageSelf)).WithName("DriverGoOnline");
        group.MapPost("/me/availability/offline", (HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Availability("offline", c, u, s, ct)).RequireAuthorization(Permission(DriverPermissions.AvailabilityManageSelf)).WithName("DriverGoOffline");
        group.MapPost("/me/availability/break/start", (HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Availability("break-start", c, u, s, ct)).RequireAuthorization(Permission(DriverPermissions.AvailabilityManageSelf)).WithName("DriverStartBreak");
        group.MapPost("/me/availability/break/end", (HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Availability("break-end", c, u, s, ct)).RequireAuthorization(Permission(DriverPermissions.AvailabilityManageSelf)).WithName("DriverEndBreak");

        group.MapPost("/{driverId:guid}/shifts", CreateShift).RequireAuthorization(Permission(DriverPermissions.ShiftsManage)).WithName("CreateDriverShift");
        group.MapGet("/{driverId:guid}/shifts", ListShifts).RequireAuthorization(Permission(DriverPermissions.ShiftsRead)).WithName("ListDriverShifts");
        group.MapGet("/{driverId:guid}/shifts/{shiftId:guid}", GetShift).RequireAuthorization(Permission(DriverPermissions.ShiftsRead)).WithName("GetDriverShift");
        foreach (string operation in new[] { "start", "complete", "cancel" }) { string value = operation; group.MapPost($"/{{driverId:guid}}/shifts/{{shiftId:guid}}/{value}", (Guid driverId, Guid shiftId, HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Execute(s.ChangeShiftAsync(Actor(u, c), driverId, shiftId, value, Key(c), ct))).RequireAuthorization(Permission(DriverPermissions.ShiftsManage)).WithName($"{char.ToUpperInvariant(value[0])}{value[1..]}DriverShift"); }
        group.MapGet("/me/shifts", ListMyShifts).RequireAuthorization(Permission(DriverPermissions.ShiftsReadSelf)).WithName("ListMyDriverShifts");
        group.MapGet("/me/shifts/{shiftId:guid}", GetMyShift).RequireAuthorization(Permission(DriverPermissions.ShiftsReadSelf)).WithName("GetMyDriverShift");
        foreach (string operation in new[] { "start", "complete" }) { string value = operation; group.MapPost($"/me/shifts/{{shiftId:guid}}/{value}", (Guid shiftId, HttpContext c, ICurrentUser u, IDriverService s, CancellationToken ct) => Execute(s.ChangeMyShiftAsync(Actor(u, c), shiftId, value, Key(c), ct))).RequireAuthorization(Permission(DriverPermissions.ShiftsManageSelf)).WithName($"{char.ToUpperInvariant(value[0])}{value[1..]}MyDriverShift"); }
        group.MapPost("/{driverId:guid}/violations", RecordViolation).RequireAuthorization(Permission(DriverPermissions.ViolationsManage)).WithName("RecordDriverViolation");
        group.MapPost("/{driverId:guid}/violations/{violationId:guid}/resolve", ResolveViolation).RequireAuthorization(Permission(DriverPermissions.ViolationsManage)).WithName("ResolveDriverViolation");
        group.MapPost("/{driverId:guid}/suspensions", Suspend).RequireAuthorization(Permission(DriverPermissions.SuspensionsManage)).WithName("SuspendDriver");
        group.MapPost("/{driverId:guid}/suspensions/{suspensionId:guid}/lift", LiftSuspension).RequireAuthorization(Permission(DriverPermissions.SuspensionsManage)).WithName("LiftDriverSuspension");
        return endpoints;
    }

    private static void MapTransition(RouteGroupBuilder group, string operation, string permission) => group.MapPost($"/{{driverId:guid}}/{operation}", (Guid driverId, DriverTransitionRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.TransitionAsync(Actor(user, context), driverId, operation, request.ConcurrencyStamp, null, Key(context), ct))).RequireAuthorization(Permission(permission)).RequireRateLimiting("drivers-write").WithName($"Driver{char.ToUpperInvariant(operation[0])}{operation[1..]}");
    private static Task<IResult> Create(CreateDriverRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.CreateAsync(Actor(user, context), request, Key(context), ct));
    private static Task<IResult> GetMy(HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.GetMyAsync(Actor(user, context), ct));
    private static Task<IResult> Get(Guid driverId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.GetAsync(Actor(user, context), driverId, ct));
    private static Task<IResult> List([AsParameters] DriverQuery query, IDriverService service, CancellationToken ct) => Execute(service.ListAsync(query, ct));
    private static Task<IResult> UpdateMy(UpdateDriverProfileRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.UpdateProfileAsync(Actor(user, context), request, Key(context), ct));
    private static async Task<IResult> SubmitReview(HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) { DriverActor actor = Actor(user, context); DriverOperationResult<DriverProfileResponse> profile = await service.GetMyAsync(actor, ct); return profile.Value is null ? await Execute(Task.FromResult(profile)) : await Execute(service.TransitionAsync(actor, profile.Value.Id, "submit-review", profile.Value.ConcurrencyStamp, null, Key(context), ct)); }
    private static Task<IResult> AddVehicle(AddVehicleRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.AddVehicleAsync(Actor(user, context), request, Key(context), ct));
    private static Task<IResult> SetPrimaryVehicle(Guid vehicleId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.SetPrimaryVehicleAsync(Actor(user, context), vehicleId, Key(context), ct));
    private static Task<IResult> SubmitDocument(SubmitDriverDocumentRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.SubmitDocumentAsync(Actor(user, context), request, Key(context), ct));
    private static Task<IResult> AssignZone(Guid driverId, AssignDriverZoneRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.AssignZoneAsync(Actor(user, context), driverId, request, Key(context), ct));
    private static Task<IResult> RemoveZone(Guid driverId, Guid zoneId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.RemoveZoneAsync(Actor(user, context), driverId, zoneId, Key(context), ct));
    private static Task<IResult> Availability(string operation, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ChangeAvailabilityAsync(Actor(user, context), operation, Key(context), ct));
    private static Task<IResult> CreateShift(Guid driverId, CreateDriverShiftRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.CreateShiftAsync(Actor(user, context), driverId, request, Key(context), ct));
    private static Task<IResult> ListShifts(Guid driverId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ListShiftsAsync(Actor(user, context), driverId, ct));
    private static Task<IResult> GetShift(Guid driverId, Guid shiftId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.GetShiftAsync(Actor(user, context), driverId, shiftId, ct));
    private static Task<IResult> ListMyShifts(HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ListMyShiftsAsync(Actor(user, context), ct));
    private static Task<IResult> GetMyShift(Guid shiftId, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.GetMyShiftAsync(Actor(user, context), shiftId, ct));
    private static Task<IResult> RecordViolation(Guid driverId, RecordDriverViolationRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.RecordViolationAsync(Actor(user, context), driverId, request, Key(context), ct));
    private static Task<IResult> ResolveViolation(Guid driverId, Guid violationId, ResolveDriverViolationRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.ResolveViolationAsync(Actor(user, context), driverId, violationId, request, Key(context), ct));
    private static Task<IResult> Suspend(Guid driverId, SuspendDriverRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.SuspendAsync(Actor(user, context), driverId, request, Key(context), ct));
    private static Task<IResult> LiftSuspension(Guid driverId, Guid suspensionId, LiftDriverSuspensionRequest request, HttpContext context, ICurrentUser user, IDriverService service, CancellationToken ct) => Execute(service.LiftSuspensionAsync(Actor(user, context), driverId, suspensionId, request, Key(context), ct));
    private static DriverActor Actor(ICurrentUser user, HttpContext context) => new(user.UserId?.Value ?? Guid.Empty, context.Request.Headers["X-Correlation-ID"].ToString());
    private static string Key(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static async Task<IResult> Execute<T>(Task<DriverOperationResult<T>> task) { DriverOperationResult<T> result = await task; if (result.Value is not null) return result.Status == DriverOperationStatus.Created ? Results.Created((string?)null, result.Value) : Results.Ok(result.Value); int status = result.Status switch { DriverOperationStatus.NotFound => 404, DriverOperationStatus.Forbidden => 403, DriverOperationStatus.Conflict => 409, _ => 400 }; return Results.Problem(statusCode: status, title: "Driver operation failed.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode }); }
}

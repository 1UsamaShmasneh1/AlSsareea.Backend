using AlSsareea.Api.Security;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Media.Application;
using Microsoft.AspNetCore.Mvc;

namespace AlSsareea.Api.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/media/assets").WithTags("Media");
        group.MapPost("", Upload).DisableAntiforgery().RequireAuthorization(Permission(MediaPermissions.Upload));
        group.MapGet("/{id:guid}", GetMetadata);
        group.MapGet("/{id:guid}/content", GetContent);
        group.MapGet("/{id:guid}/variants/{variant}", GetVariant);
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization(Permission(MediaPermissions.Delete));
        return app;
    }

    private static async Task<IResult> Upload(HttpRequest request, [FromServices] ICurrentUser current, [FromServices] IMediaService service, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Problem(400, "multipart_required");
        IFormCollection form = await request.ReadFormAsync(ct); IFormFile? file = form.Files.GetFile("file");
        if (file is null || !Guid.TryParse(form["merchantId"], out Guid merchantId) || !Guid.TryParse(form["ownerId"], out Guid ownerId)) return Problem(400, "invalid_upload");
        string ownerType = form["ownerType"].ToString(); string access = string.IsNullOrWhiteSpace(form["accessLevel"]) ? "Private" : form["accessLevel"].ToString();
        await using Stream content = file.OpenReadStream();
        return Result(await service.UploadAsync(new MediaUploadRequest(content, file.FileName, file.ContentType, file.Length, merchantId, ownerType, ownerId, access), Actor(current), ct));
    }
    private static async Task<IResult> GetMetadata([FromRoute] Guid id, [FromServices] ICurrentUser current, [FromServices] IMediaService service, CancellationToken ct) =>
        Result(await service.GetAsync(id, Actor(current), current.UserId is null, ct));
    private static async Task<IResult> GetContent([FromRoute] Guid id, HttpContext context, [FromServices] ICurrentUser current, [FromServices] IMediaService service, CancellationToken ct) =>
        await Content(await service.GetContentAsync(id, null, Actor(current), ct), context);
    private static async Task<IResult> GetVariant([FromRoute] Guid id, [FromRoute] string variant, HttpContext context, [FromServices] ICurrentUser current, [FromServices] IMediaService service, CancellationToken ct) =>
        await Content(await service.GetContentAsync(id, variant, Actor(current), ct), context);
    private static async Task<IResult> Delete([FromRoute] Guid id, [FromServices] ICurrentUser current, [FromServices] IMediaService service, CancellationToken ct) =>
        Result(await service.DeleteAsync(id, Actor(current), ct));
    private static Task<IResult> Content(MediaOperationResult<MediaContent> result, HttpContext context)
    {
        if (result.Value is null) return Task.FromResult(Result(result));
        MediaContent content = result.Value; context.Response.Headers.ETag = content.EntityTag;
        context.Response.Headers.CacheControl = content.IsPublic ? "public,max-age=86400,immutable" : "private,no-store";
        return Task.FromResult(Results.Stream(content.Content, content.MimeType, enableRangeProcessing: true));
    }
    private static MediaActor Actor(ICurrentUser current) => new(current.UserId?.Value ?? Guid.Empty, current.Roles.Any(x => x is "admin" or "platform-admin" or "operations"));
    private static string Permission(string value) => AuthenticationPolicies.PermissionPrefix + value;
    private static IResult Result<T>(MediaOperationResult<T> result) => result.Status switch
    {
        MediaOperationStatus.Success => Results.Ok(result.Value),
        MediaOperationStatus.Created => Results.Json(result.Value, statusCode: 201),
        MediaOperationStatus.NotFound => Problem(404, result.ErrorCode),
        MediaOperationStatus.Forbidden => Problem(403, result.ErrorCode),
        MediaOperationStatus.Conflict => Problem(409, result.ErrorCode),
        _ => Problem(400, result.ErrorCode),
    };
    private static IResult Problem(int status, string? code) => Results.Problem(statusCode: status, title: status switch { 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Invalid request" }, extensions: new Dictionary<string, object?> { ["code"] = code });
}

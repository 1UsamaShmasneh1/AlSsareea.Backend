using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Contracts;
using AlSsareea.Modules.Media.Domain;
using AlSsareea.Modules.Media.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Media.Infrastructure;

internal sealed partial class MediaService(MediaDbContext db, IMediaAssetRepository repository, IMediaStorage storage, IMediaImageProcessor processor, IMediaMalwareScanner scanner, IMerchantCatalogScopeProvider merchants, IClock clock, IOptions<MediaOptions> options, ILogger<MediaService> logger) : IMediaService, IMediaAssetLookup
{
    private readonly MediaOptions _options = options.Value;
    public async Task<MediaOperationResult<MediaAssetDto>> UploadAsync(MediaUploadRequest request, MediaActor actor, CancellationToken ct) => await Run(async () =>
    {
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(request.MerchantId, actor.UserId, actor.IsPlatformOperator, ct);
        if (scope?.CanManageMerchant != true) return Failure<MediaAssetDto>(MediaOperationStatus.Forbidden, "forbidden");
        UploadStarted(logger, request.MerchantId, request.OwnerType);
        ValidatedImage image = await processor.ValidateAsync(request, ct);
        await using Stream validatedContent = image.Content;
        MalwareScanResult scan = await scanner.ScanAsync(validatedContent, ct);
        if (scan != MalwareScanResult.Safe) return Failure<MediaAssetDto>(MediaOperationStatus.Invalid, scan == MalwareScanResult.Unsafe ? "unsafe_file" : "scanner_unavailable");
        if (validatedContent.CanSeek) validatedContent.Position = 0;
        MediaAssetId id = MediaAssetId.New(); string originalKey = storage.CreateKey(id, image.FileName);
        if (!Enum.TryParse(request.AccessLevel, true, out MediaAccessLevel access)) return Failure<MediaAssetDto>(MediaOperationStatus.Invalid, "invalid_access_level");
        MediaAsset asset = MediaAsset.Create(id, request.MerchantId, request.OwnerType, request.OwnerId, image.FileName, originalKey, image.MimeType, image.Extension, image.SizeInBytes, image.Sha256Hash, image.Width, image.Height, access, "local", clock.UtcNow);
        await repository.AddAsync(asset, ct); await db.SaveChangesAsync(ct); asset.StartProcessing(clock.UtcNow); await db.SaveChangesAsync(ct);
        var writtenKeys = new List<string>();
        try
        {
            await storage.WriteAsync(originalKey, validatedContent, ct); writtenKeys.Add(originalKey);
            IReadOnlyList<ProcessedMediaVariant> variants = await processor.CreateVariantsAsync(image, ct);
            foreach (ProcessedMediaVariant variant in variants)
            {
                await using Stream content = variant.Content;
                string key = storage.CreateVariantKey(id, variant.Type, ".webp"); await storage.WriteAsync(key, content, ct); writtenKeys.Add(key);
                asset.AddOrReplaceVariant(variant.Type, key, variant.MimeType, content.Length, variant.Width, variant.Height, clock.UtcNow);
            }
            asset.MarkReady(clock.UtcNow); await db.SaveChangesAsync(ct);
            AssetReady(logger, id.Value, asset.Variants.Count);
            return Created(ToDto(asset));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ProcessingFailed(logger, exception, id.Value);
            asset.MarkFailed("processing_failed", clock.UtcNow); await db.SaveChangesAsync(CancellationToken.None);
            foreach (string key in writtenKeys) try { await storage.DeleteAsync(key, CancellationToken.None); } catch (Exception cleanupException) { PhysicalCleanupFailed(logger, cleanupException, id.Value); }
            return Failure<MediaAssetDto>(MediaOperationStatus.Invalid, "processing_failed");
        }
    });
    public async Task<MediaOperationResult<MediaAssetDto>> GetAsync(Guid id, MediaActor actor, bool publicRequest, CancellationToken ct) => await Run(async () =>
    {
        MediaAsset? asset = await repository.GetAsync(new MediaAssetId(id), false, ct);
        if (asset is null || asset.Status == MediaAssetStatus.Deleted || publicRequest && (asset.Status != MediaAssetStatus.Ready || asset.AccessLevel != MediaAccessLevel.Public)) return Failure<MediaAssetDto>(MediaOperationStatus.NotFound, "not_found");
        if (!publicRequest && !await CanManage(asset, actor, ct)) return Failure<MediaAssetDto>(MediaOperationStatus.Forbidden, "forbidden");
        return Success(ToDto(asset));
    });
    public async Task<MediaOperationResult<MediaContent>> GetContentAsync(Guid id, string? variant, MediaActor actor, CancellationToken ct) => await Run(async () =>
    {
        MediaAsset? asset = await repository.GetAsync(new MediaAssetId(id), false, ct);
        if (asset is null || asset.Status != MediaAssetStatus.Ready) return Failure<MediaContent>(MediaOperationStatus.NotFound, "not_found");
        if (asset.AccessLevel != MediaAccessLevel.Public && !await CanManage(asset, actor, ct)) return Failure<MediaContent>(MediaOperationStatus.NotFound, "not_found");
        string key = asset.StorageKey; string mime = asset.MimeType; long length = asset.SizeInBytes;
        if (!string.IsNullOrWhiteSpace(variant))
        {
            if (!Enum.TryParse(variant, true, out MediaVariantType type)) return Failure<MediaContent>(MediaOperationStatus.NotFound, "not_found");
            MediaVariant? value = asset.Variants.SingleOrDefault(x => x.Type == type);
            if (value is null) return Failure<MediaContent>(MediaOperationStatus.NotFound, "not_found");
            key = value.StorageKey; mime = value.MimeType; length = value.SizeInBytes;
        }
        Stream? content = await storage.ReadAsync(key, ct);
        return content is null ? Failure<MediaContent>(MediaOperationStatus.NotFound, "not_found") : Success(new MediaContent(content, mime, length, asset.AccessLevel == MediaAccessLevel.Public, $"\"{asset.ContentHash}\""));
    });
    public async Task<MediaOperationResult<MediaAssetDto>> DeleteAsync(Guid id, MediaActor actor, CancellationToken ct) => await Run(async () =>
    {
        MediaAsset? asset = await repository.GetAsync(new MediaAssetId(id), true, ct);
        if (asset is null) return Failure<MediaAssetDto>(MediaOperationStatus.NotFound, "not_found");
        if (!await CanManage(asset, actor, ct)) return Failure<MediaAssetDto>(MediaOperationStatus.Forbidden, "forbidden");
        bool changed = asset.Delete(clock.UtcNow); if (changed) await db.SaveChangesAsync(ct);
        foreach (string key in asset.Variants.Select(x => x.StorageKey).Append(asset.StorageKey))
            try { await storage.DeleteAsync(key, ct); } catch (Exception exception) { PhysicalCleanupFailed(logger, exception, id); }
        return Success(ToDto(asset));
    });
    public async Task<MediaAssetReference?> FindAsync(Guid assetId, CancellationToken ct = default)
    {
        MediaAsset? x = await repository.GetAsync(new MediaAssetId(assetId), false, ct);
        return x is null ? null : new(x.Id.Value, x.MerchantId, x.OwnerType, x.OwnerId, x.Status == MediaAssetStatus.Ready, x.Status == MediaAssetStatus.Deleted, x.AccessLevel.ToString(), ContentUrl(x.Id.Value));
    }
    public async Task<bool> CanUseAsync(Guid assetId, Guid merchantId, string ownerType, Guid ownerId, CancellationToken ct = default)
    {
        MediaAssetReference? x = await FindAsync(assetId, ct);
        return x is { IsReady: true, IsDeleted: false } && x.MerchantId == merchantId && x.OwnerType == ownerType && x.OwnerId == ownerId;
    }
    private async Task<bool> CanManage(MediaAsset asset, MediaActor actor, CancellationToken ct) => (await merchants.GetScopeAsync(asset.MerchantId, actor.UserId, actor.IsPlatformOperator, ct))?.CanManageMerchant == true;
    private MediaAssetDto ToDto(MediaAsset x) => new(x.Id.Value, x.MerchantId, x.OwnerType, x.OwnerId, x.OriginalFileName, x.MimeType, x.SizeInBytes, x.ContentHash, x.Width, x.Height, x.Status.ToString(), x.AccessLevel.ToString(), ContentUrl(x.Id.Value), x.Variants.OrderBy(v => v.Type).Select(v => new MediaVariantDto(v.Type.ToString(), $"{ContentUrl(x.Id.Value)}/variants/{v.Type.ToString().ToLowerInvariant()}", v.MimeType, v.SizeInBytes, v.Width, v.Height)).ToArray(), x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private string ContentUrl(Guid id) => $"{_options.PublicBasePath.TrimEnd('/')}/{id}/content";
    private static async Task<MediaOperationResult<T>> Run<T>(Func<Task<MediaOperationResult<T>>> action) { try { return await action(); } catch (DomainException) { return Failure<T>(MediaOperationStatus.Invalid, "media_validation"); } catch (DbUpdateConcurrencyException) { return Failure<T>(MediaOperationStatus.Conflict, "concurrency_conflict"); } catch (DbUpdateException) { return Failure<T>(MediaOperationStatus.Conflict, "database_constraint"); } }
    private static MediaOperationResult<T> Success<T>(T value) => new(MediaOperationStatus.Success, value);
    private static MediaOperationResult<T> Created<T>(T value) => new(MediaOperationStatus.Created, value);
    private static MediaOperationResult<T> Failure<T>(MediaOperationStatus status, string code) => new(status, default, code);
    [LoggerMessage(Level = LogLevel.Information, Message = "Media upload started for merchant {MerchantId} and owner type {OwnerType}")]
    private static partial void UploadStarted(ILogger logger, Guid merchantId, string ownerType);
    [LoggerMessage(Level = LogLevel.Information, Message = "Media asset {AssetId} is ready with {VariantCount} variants")]
    private static partial void AssetReady(ILogger logger, Guid assetId, int variantCount);
    [LoggerMessage(Level = LogLevel.Error, Message = "Media processing failed for asset {AssetId}")]
    private static partial void ProcessingFailed(ILogger logger, Exception exception, Guid assetId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Physical cleanup failed for media asset {AssetId}")]
    private static partial void PhysicalCleanupFailed(ILogger logger, Exception exception, Guid assetId);
}

internal sealed partial class MediaCleanupService(MediaDbContext db, IMediaStorage storage, IClock clock, IOptions<MediaOptions> options, ILogger<MediaCleanupService> logger) : IMediaCleanupService
{
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        DateTime failedBefore = clock.UtcNow.AddDays(-options.Value.FailedRetentionDays);
        MediaAsset[] assets = await db.Assets.Include(x => x.Variants).Where(x => x.Status == MediaAssetStatus.Deleted || x.Status == MediaAssetStatus.Failed && x.UpdatedAtUtc < failedBefore).OrderBy(x => x.UpdatedAtUtc).Take(options.Value.CleanupBatchSize).ToArrayAsync(ct);
        int count = 0;
        foreach (MediaAsset asset in assets)
        {
            bool complete = true;
            foreach (string key in asset.Variants.Select(x => x.StorageKey).Append(asset.StorageKey))
                try { await storage.DeleteAsync(key, ct); } catch (Exception exception) { complete = false; CleanupFailed(logger, exception, asset.Id.Value); }
            if (complete) count++;
        }
        CleanupCompleted(logger, assets.Length, count); return count;
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Cleanup failed for media asset {AssetId}")]
    private static partial void CleanupFailed(ILogger logger, Exception exception, Guid assetId);
    [LoggerMessage(Level = LogLevel.Information, Message = "Media cleanup inspected {AssetCount} assets and cleaned {CleanedCount}")]
    private static partial void CleanupCompleted(ILogger logger, int assetCount, int cleanedCount);
}

using AlSsareea.Modules.Media.Contracts;
using AlSsareea.Modules.Media.Domain;

namespace AlSsareea.Modules.Media.Application;

public static class MediaPermissions
{
    public const string Upload = "media.assets.upload";
    public const string View = "media.assets.view";
    public const string Delete = "media.assets.delete";
    public const string Manage = "media.assets.manage";
}
public sealed record MediaActor(Guid UserId, bool IsPlatformOperator);
public enum MediaOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record MediaOperationResult<T>(MediaOperationStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record MediaUploadRequest(Stream Content, string OriginalFileName, string DeclaredMimeType, long DeclaredLength, Guid MerchantId, string OwnerType, Guid OwnerId, string AccessLevel);
public sealed record MediaContent(Stream Content, string MimeType, long Length, bool IsPublic, string EntityTag);
public sealed record ValidatedImage(string FileName, string MimeType, string Extension, long SizeInBytes, string Sha256Hash, int Width, int Height, Stream Content);
public sealed record ProcessedMediaVariant(MediaVariantType Type, string MimeType, int Width, int Height, Stream Content);
public enum MalwareScanResult { Safe, Unsafe, Unavailable }

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetAsync(MediaAssetId id, bool tracked = true, CancellationToken cancellationToken = default);
    Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default);
}
public interface IMediaStorage
{
    string CreateKey(MediaAssetId assetId, string fileName);
    string CreateVariantKey(MediaAssetId assetId, MediaVariantType type, string extension);
    Task WriteAsync(string key, Stream content, CancellationToken cancellationToken);
    Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
public interface IMediaImageProcessor
{
    Task<ValidatedImage> ValidateAsync(MediaUploadRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessedMediaVariant>> CreateVariantsAsync(ValidatedImage image, CancellationToken cancellationToken);
}
public interface IMediaMalwareScanner { Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken); }
public interface IMediaService
{
    Task<MediaOperationResult<MediaAssetDto>> UploadAsync(MediaUploadRequest request, MediaActor actor, CancellationToken cancellationToken);
    Task<MediaOperationResult<MediaAssetDto>> GetAsync(Guid id, MediaActor actor, bool publicRequest, CancellationToken cancellationToken);
    Task<MediaOperationResult<MediaContent>> GetContentAsync(Guid id, string? variant, MediaActor actor, CancellationToken cancellationToken);
    Task<MediaOperationResult<MediaAssetDto>> DeleteAsync(Guid id, MediaActor actor, CancellationToken cancellationToken);
}
public interface IMediaCleanupService { Task<int> CleanupAsync(CancellationToken cancellationToken = default); }

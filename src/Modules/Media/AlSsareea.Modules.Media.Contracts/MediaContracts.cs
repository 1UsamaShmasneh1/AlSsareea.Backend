namespace AlSsareea.Modules.Media.Contracts;

public sealed record MediaVariantDto(string Type, string Url, string MimeType, long SizeInBytes, int Width, int Height);
public sealed record MediaAssetDto(Guid Id, Guid MerchantId, string OwnerType, Guid OwnerId, string OriginalFileName, string MimeType, long SizeInBytes, string ContentHash, int Width, int Height, string Status, string AccessLevel, string ContentUrl, IReadOnlyList<MediaVariantDto> Variants, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp);
public sealed record MediaAssetReference(Guid Id, Guid MerchantId, string OwnerType, Guid OwnerId, bool IsReady, bool IsDeleted, string AccessLevel, string ContentUrl);
public interface IMediaAssetLookup
{
    Task<MediaAssetReference?> FindAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<bool> CanUseAsync(Guid assetId, Guid merchantId, string ownerType, Guid ownerId, CancellationToken cancellationToken = default);
}

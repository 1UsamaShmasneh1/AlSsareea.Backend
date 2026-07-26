using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Media.Domain;

public sealed class MediaAsset : AggregateRoot<MediaAssetId>
{
    private readonly List<MediaVariant> _variants = [];
    private MediaAsset(MediaAssetId id) : base(id) { OwnerType = OriginalFileName = StorageKey = MimeType = FileExtension = ContentHash = StorageProvider = null!; }
    private MediaAsset(MediaAssetId id, Guid merchantId, string ownerType, Guid ownerId, string fileName, string storageKey, string mimeType, string extension, long size, string hash, int width, int height, MediaAccessLevel access, string provider, DateTime now) : base(id)
    {
        MerchantId = MediaRules.Id(merchantId, nameof(merchantId)); OwnerType = MediaRules.OwnerType(ownerType); OwnerId = MediaRules.Id(ownerId, nameof(ownerId));
        OriginalFileName = MediaRules.Required(fileName, 255, nameof(fileName)); StorageKey = MediaRules.Required(storageKey, 500, nameof(storageKey));
        MimeType = MediaRules.Required(mimeType, 100, nameof(mimeType)); FileExtension = MediaRules.Required(extension, 10, nameof(extension)).ToLowerInvariant();
        MediaRules.Positive(size, nameof(size)); MediaRules.Positive(width, nameof(width)); MediaRules.Positive(height, nameof(height));
        SizeInBytes = size; ContentHash = MediaRules.Required(hash, 64, nameof(hash)); Width = width; Height = height; AccessLevel = access;
        StorageProvider = MediaRules.Required(provider, 50, nameof(provider)); Status = MediaAssetStatus.Pending; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid();
    }
    public Guid MerchantId { get; private set; }
    public string OwnerType { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string MimeType { get; private set; } = null!;
    public string FileExtension { get; private set; } = null!;
    public long SizeInBytes { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public MediaAssetStatus Status { get; private set; }
    public MediaAccessLevel AccessLevel { get; private set; }
    public string StorageProvider { get; private set; } = null!;
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<MediaVariant> Variants => _variants;

    public static MediaAsset Create(MediaAssetId id, Guid merchantId, string ownerType, Guid ownerId, string fileName, string storageKey, string mimeType, string extension, long size, string hash, int width, int height, MediaAccessLevel access, string provider, DateTime now)
    {
        MediaRules.Utc(now); MediaAsset asset = new(id, merchantId, ownerType, ownerId, fileName, storageKey, mimeType, extension, size, hash, width, height, access, provider, now);
        asset.RaiseDomainEvent(new MediaAssetCreatedDomainEvent(id, merchantId, now)); return asset;
    }
    public void StartProcessing(DateTime now) { Ensure(MediaAssetStatus.Pending); Status = MediaAssetStatus.Processing; Touch(now); RaiseDomainEvent(new MediaAssetProcessingStartedDomainEvent(Id, now)); }
    public void AddOrReplaceVariant(MediaVariantType type, string key, string mime, long size, int width, int height, DateTime now)
    {
        Ensure(MediaAssetStatus.Processing); _variants.RemoveAll(x => x.Type == type);
        _variants.Add(MediaVariant.Create(MediaVariantId.New(), Id, type, key, mime, size, width, height, now)); Touch(now);
    }
    public void MarkReady(DateTime now) { Ensure(MediaAssetStatus.Processing); Status = MediaAssetStatus.Ready; FailureReason = null; Touch(now); RaiseDomainEvent(new MediaAssetReadyDomainEvent(Id, now)); }
    public void MarkFailed(string reason, DateTime now) { if (Status is MediaAssetStatus.Ready or MediaAssetStatus.Deleted) throw new DomainException("Ready or deleted media cannot fail processing."); Status = MediaAssetStatus.Failed; FailureReason = MediaRules.Required(reason, 1000, nameof(reason)); Touch(now); RaiseDomainEvent(new MediaAssetProcessingFailedDomainEvent(Id, now)); }
    public bool Delete(DateTime now)
    {
        if (Status == MediaAssetStatus.Deleted) return false;
        Status = MediaAssetStatus.Deleted; DeletedAtUtc = now; Touch(now); RaiseDomainEvent(new MediaAssetDeletedDomainEvent(Id, now)); return true;
    }
    private void Ensure(MediaAssetStatus required) { if (Status != required) throw new DomainException($"Media must be {required}."); }
    private void Touch(DateTime now) { MediaRules.Utc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class MediaVariant : Entity<MediaVariantId>
{
    private MediaVariant(MediaVariantId id) : base(id) { StorageKey = MimeType = null!; }
    private MediaVariant(MediaVariantId id, MediaAssetId assetId, MediaVariantType type, string key, string mime, long size, int width, int height, DateTime now) : base(id)
    { MediaAssetId = assetId; Type = type; StorageKey = MediaRules.Required(key, 500, nameof(key)); MimeType = MediaRules.Required(mime, 100, nameof(mime)); MediaRules.Positive(size, nameof(size)); MediaRules.Positive(width, nameof(width)); MediaRules.Positive(height, nameof(height)); SizeInBytes = size; Width = width; Height = height; Status = MediaVariantStatus.Ready; CreatedAtUtc = now; }
    public MediaAssetId MediaAssetId { get; private set; }
    public MediaVariantType Type { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public string MimeType { get; private set; } = null!;
    public long SizeInBytes { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public MediaVariantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    internal static MediaVariant Create(MediaVariantId id, MediaAssetId assetId, MediaVariantType type, string key, string mime, long size, int width, int height, DateTime now) { MediaRules.Utc(now); return new(id, assetId, type, key, mime, size, width, height, now); }
}

public sealed record MediaAssetCreatedDomainEvent(MediaAssetId AssetId, Guid MerchantId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record MediaAssetProcessingStartedDomainEvent(MediaAssetId AssetId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record MediaAssetReadyDomainEvent(MediaAssetId AssetId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record MediaAssetProcessingFailedDomainEvent(MediaAssetId AssetId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record MediaAssetDeletedDomainEvent(MediaAssetId AssetId, DateTime OccurredAtUtc) : IDomainEvent;

using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Media.Domain;

namespace AlSsareea.UnitTests.Media;

public sealed class MediaDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AssetFollowsProcessingReadyAndIdempotentDeleteLifecycle()
    {
        MediaAsset asset = Create();

        asset.StartProcessing(Now.AddMinutes(1));
        asset.AddOrReplaceVariant(MediaVariantType.Thumbnail, "variants/thumb.webp", "image/webp", 100, 80, 80, Now.AddMinutes(2));
        asset.MarkReady(Now.AddMinutes(3));

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        Assert.Single(asset.Variants);
        Assert.True(asset.Delete(Now.AddMinutes(4)));
        Assert.False(asset.Delete(Now.AddMinutes(5)));
        Assert.Equal(MediaAssetStatus.Deleted, asset.Status);
    }

    [Fact]
    public void InvalidLifecycleTransitionIsRejected()
    {
        MediaAsset asset = Create();

        Assert.Throws<DomainException>(() => asset.MarkReady(Now.AddMinutes(1)));
    }

    [Fact]
    public void ReprocessingSameVariantTypeReplacesIt()
    {
        MediaAsset asset = Create();
        asset.StartProcessing(Now.AddMinutes(1));

        asset.AddOrReplaceVariant(MediaVariantType.Small, "variants/first.webp", "image/webp", 100, 100, 100, Now.AddMinutes(2));
        asset.AddOrReplaceVariant(MediaVariantType.Small, "variants/second.webp", "image/webp", 120, 120, 120, Now.AddMinutes(3));

        MediaVariant variant = Assert.Single(asset.Variants);
        Assert.Equal("variants/second.webp", variant.StorageKey);
    }

    private static MediaAsset Create() => MediaAsset.Create(
        MediaAssetId.New(), Guid.NewGuid(), "CatalogProduct", Guid.NewGuid(), "meal.png",
        "original/meal.png", "image/png", ".png", 256,
        new string('a', 64), 100, 100, MediaAccessLevel.Public, "local", Now);
}

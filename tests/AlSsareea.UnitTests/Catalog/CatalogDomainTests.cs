using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Catalog.Domain;
using CatalogAggregate = AlSsareea.Modules.Catalog.Domain.Catalog;

namespace AlSsareea.UnitTests.Catalog;

public sealed class CatalogDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CatalogLifecycleRequiresPublishableContent()
    {
        var catalog = CatalogAggregate.Create(CatalogId.New(), Guid.NewGuid(), "Main", null, "ar", Now);

        Assert.Throws<DomainException>(() => catalog.Activate(false, Now.AddMinutes(1)));

        catalog.Activate(true, Now.AddMinutes(1));
        catalog.Suspend(Now.AddMinutes(2));
        catalog.Archive(Now.AddMinutes(3));

        Assert.Equal(CatalogStatus.Archived, catalog.Status);
    }

    [Fact]
    public void ProductRequiresDefaultTranslationAndSeparatesAvailabilityStates()
    {
        Product product = CreateProduct();

        Assert.Throws<DomainException>(() => product.Publish("ar", Now.AddMinutes(1)));

        product.SetTranslation("ar", "قهوة", "طازجة", Now.AddMinutes(1));
        product.Publish("ar", Now.AddMinutes(2));
        Assert.True(product.IsPurchasable);

        product.SetInventory(InventoryStatus.OutOfStock, Now.AddMinutes(3));
        Assert.False(product.IsPurchasable);
    }

    [Fact]
    public void VariantOptionAndImageRulesAreEnforced()
    {
        Product product = CreateProduct();

        Assert.Throws<DomainException>(() => product.AddVariant(
            "en", "Impossible", null, -501, InventoryStatus.InStock, false, 0, Now));

        ProductVariant first = product.AddVariant(
            "en", "Small", "SKU-S", 0, InventoryStatus.InStock, true, 0, Now.AddMinutes(1));
        ProductVariant second = product.AddVariant(
            "en", "Large", "SKU-L", 200, InventoryStatus.InStock, true, 1, Now.AddMinutes(2));
        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);

        OptionGroup group = product.AddOptionGroup(
            "en", "Sauce", SelectionType.SingleChoice, true, 1, 1, 0, Now.AddMinutes(3));
        group.AddOption("en", "Ketchup", 50, true, true, 0, Now.AddMinutes(4));
        Assert.Throws<DomainException>(() =>
            group.AddOption("en", "Unavailable", 0, true, false, 1, Now.AddMinutes(5)));

        Assert.Throws<DomainException>(() =>
            product.AddImage(null, null, null, 0, false, Now.AddMinutes(6)));
        ProductImageReference firstImage = product.AddImage(
            null, "media://one", null, 0, true, Now.AddMinutes(7));
        ProductImageReference secondImage = product.AddImage(
            Guid.NewGuid(), null, null, 1, true, Now.AddMinutes(8));
        Assert.False(firstImage.IsPrimary);
        Assert.True(secondImage.IsPrimary);
    }

    [Fact]
    public void AvailabilitySupportsOvernightPeriods()
    {
        Product product = CreateProduct();
        product.SetTranslation("en", "Coffee", null, Now.AddMinutes(1));
        product.Publish("en", Now.AddMinutes(2));
        product.AddAvailability(
            null,
            DayOfWeek.Saturday,
            new TimeOnly(22, 0),
            new TimeOnly(2, 0),
            "UTC",
            Now.AddMinutes(3));

        Assert.True(product.IsAvailableAt(
            new DateTime(2026, 7, 25, 23, 0, 0, DateTimeKind.Utc), null));
        Assert.True(product.IsAvailableAt(
            new DateTime(2026, 7, 26, 1, 0, 0, DateTimeKind.Utc), null));
        Assert.False(product.IsAvailableAt(
            new DateTime(2026, 7, 26, 3, 0, 0, DateTimeKind.Utc), null));
    }

    [Fact]
    public void LocalizationRejectsDuplicatesByUpdatingExistingTranslation()
    {
        Product product = CreateProduct();
        product.SetTranslation("he", "קפה", null, Now.AddMinutes(1));
        product.SetTranslation("he", "קפה חדש", null, Now.AddMinutes(2));

        Assert.Equal("קפה חדש", Assert.Single(product.Translations).Name);
        Assert.Throws<DomainException>(() =>
            product.SetTranslation("fr", "Café", null, Now.AddMinutes(3)));
    }

    [Fact]
    public void CategoryImageIsOptionalAndProductRejectsDuplicateMediaReference()
    {
        Category category = Category.Create(
            CategoryId.New(), CatalogId.New(), Guid.NewGuid(), null, 0,
            "en", "Meals", null, Now);
        Guid mediaId = Guid.NewGuid();

        category.SetImage(mediaId, Now.AddMinutes(1));
        Assert.Equal(mediaId, category.MediaAssetId);
        category.SetImage(null, Now.AddMinutes(2));
        Assert.Null(category.MediaAssetId);

        Product product = CreateProduct();
        product.AddImage(mediaId, null, null, 0, true, Now.AddMinutes(1));
        Assert.Throws<DomainException>(() =>
            product.AddImage(mediaId, null, null, 1, false, Now.AddMinutes(2)));
    }

    private static Product CreateProduct() =>
        Product.Create(
            ProductId.New(),
            CatalogId.New(),
            Guid.NewGuid(),
            null,
            "SKU",
            500,
            "ILS",
            null,
            0,
            Now);
}

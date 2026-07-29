namespace AlSsareea.Modules.Catalog.Contracts;

public sealed record TranslationRequest(string LanguageCode, string Name, string? Description);
public sealed record CreateCatalogRequest(string Name, string? Description, string DefaultLanguage);
public sealed record UpdateCatalogRequest(string Name, string? Description, string DefaultLanguage, Guid ConcurrencyStamp);
public sealed record ConcurrencyRequest(Guid ConcurrencyStamp);
public sealed record CreateCategoryRequest(Guid? ParentCategoryId, int SortOrder, TranslationRequest Translation);
public sealed record UpdateCategoryRequest(Guid? ParentCategoryId, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record CategoryResponse(Guid Id, Guid CatalogId, Guid MerchantId, Guid? ParentCategoryId, Guid? MediaAssetId, int SortOrder, bool IsVisible, LocalizedTextResponse Text, Guid ConcurrencyStamp);
public sealed record SetCatalogImageRequest(Guid? MediaAssetId, Guid ConcurrencyStamp);
public sealed record CreateMenuSectionRequest(int SortOrder, DateTime? AvailableFromUtc, DateTime? AvailableUntilUtc, TranslationRequest Translation);
public sealed record UpdateMenuSectionRequest(int SortOrder, DateTime? AvailableFromUtc, DateTime? AvailableUntilUtc, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record AddSectionProductRequest(Guid ProductId, int SortOrder, Guid ConcurrencyStamp);
public sealed record ReorderItemRequest(Guid Id, int SortOrder, Guid ConcurrencyStamp);
public sealed record ReorderRequest(IReadOnlyList<ReorderItemRequest> Items);
public sealed record MenuSectionResponse(Guid Id, Guid CatalogId, Guid MerchantId, int SortOrder, bool IsVisible, DateTime? AvailableFromUtc, DateTime? AvailableUntilUtc, LocalizedTextResponse Text, IReadOnlyList<Guid> ProductIds, Guid ConcurrencyStamp);
public sealed record CreateProductRequest(Guid? CategoryId, string? Sku, long BasePriceMinor, string Currency, string? TaxCategoryReference, int SortOrder, TranslationRequest Translation);
public sealed record UpdateProductRequest(Guid? CategoryId, long BasePriceMinor, string Currency, string? TaxCategoryReference, int SortOrder, Guid ConcurrencyStamp);
public sealed record ProductTranslationRequest(TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record InventoryStatusRequest(short InventoryStatus, Guid ConcurrencyStamp);
public sealed record VisibilityRequest(Guid ConcurrencyStamp);
public sealed record AddVariantRequest(string? Sku, long PriceAdjustmentMinor, short InventoryStatus, bool IsDefault, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record UpdateVariantRequest(string? Sku, long PriceAdjustmentMinor, short InventoryStatus, bool IsVisible, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record AddOptionGroupRequest(short SelectionType, bool IsRequired, int MinSelections, int MaxSelections, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record UpdateOptionGroupRequest(short SelectionType, bool IsRequired, int MinSelections, int MaxSelections, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record AddOptionRequest(long PriceAdjustmentMinor, bool IsDefault, bool IsAvailable, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record UpdateOptionRequest(long PriceAdjustmentMinor, bool IsDefault, bool IsAvailable, int SortOrder, TranslationRequest Translation, Guid ConcurrencyStamp);
public sealed record AddImageReferenceRequest(Guid? MediaId, string? ExternalReference, string? AltText, int SortOrder, bool IsPrimary, Guid ConcurrencyStamp);
public sealed record UpdateImageReferenceRequest(Guid? MediaId, string? ExternalReference, string? AltText, int SortOrder, bool IsPrimary, Guid ConcurrencyStamp);
public sealed record AvailabilityPeriodRequest(Guid? BranchId, DayOfWeek DayOfWeek, TimeOnly StartLocalTime, TimeOnly EndLocalTime, string TimeZoneId);
public sealed record SetAvailabilityRequest(IReadOnlyList<AvailabilityPeriodRequest> Periods, Guid ConcurrencyStamp);
public sealed record ChildMutationResponse(Guid ProductId, Guid ChildId, int ProductVersion, Guid ConcurrencyStamp);
public sealed record PriceRequest(Guid? VariantId, IReadOnlyList<Guid> OptionIds, string? Language);
public sealed record CatalogResponse(Guid Id, Guid MerchantId, string Name, string? Description, string DefaultLanguage, short Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp);
public sealed record LocalizedTextResponse(string LanguageCode, string Name, string? Description);
public sealed record ProductResponse(Guid Id, Guid CatalogId, Guid MerchantId, Guid? CategoryId, string? Sku, long BasePriceMinor, string Currency, string? TaxCategoryReference, short Status, short InventoryStatus, int SortOrder, bool IsVisible, bool IsFeatured, int CurrentVersion, LocalizedTextResponse Text, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid ConcurrencyStamp);
public sealed record ProductListResponse(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record SelectedPriceItem(Guid Id, string Name, long AdjustmentMinor);
public sealed record CatalogPriceResponse(Guid ProductId, int ProductVersion, string Currency, long BasePriceMinor, long VariantAdjustmentMinor, long OptionsAdjustmentMinor, long TotalPriceMinor, SelectedPriceItem? SelectedVariant, IReadOnlyList<SelectedPriceItem> SelectedOptions);
public sealed record ProductSnapshot(Guid ProductId, int ProductVersion, Guid MerchantId, Guid CatalogId, string LocalizedProductName, long BasePriceMinor, string Currency, Guid? SelectedVariantId, string? SelectedVariantName, long VariantPriceAdjustmentMinor, IReadOnlyList<SnapshotOption> SelectedOptions, string? TaxCategoryReference, long TotalPriceMinor, DateTime CapturedAtUtc);
public sealed record SnapshotOption(Guid OptionGroupId, string OptionGroupName, Guid OptionId, string OptionName, long PriceAdjustmentMinor);
public interface IProductSnapshotProvider { Task<ProductSnapshot?> BuildAsync(Guid merchantId, Guid productId, Guid? variantId, IReadOnlyList<Guid> optionIds, string language, CancellationToken cancellationToken = default); }

public sealed record CartCatalogOptionReference(Guid OptionGroupId, Guid OptionItemId, int Quantity);
public sealed record CartCatalogValidationRequest(Guid MerchantId, Guid? BranchId, Guid ProductId, Guid? VariantId, IReadOnlyList<CartCatalogOptionReference> Options, int Quantity, int? KnownProductVersion, string Language);
public sealed record CartCatalogValidationResult(bool IsValid, bool HasChanged, string? BlockingReasonCode, ProductSnapshot? Snapshot);
public interface ICartCatalogValidationService
{
    Task<CartCatalogValidationResult> ValidateAsync(CartCatalogValidationRequest request, CancellationToken cancellationToken = default);
}

public interface ICatalogPromotionScopeProvider
{
    Task<bool> ProductsBelongToMerchantAsync(
        Guid merchantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    Task<bool> CategoriesBelongToMerchantAsync(
        Guid merchantId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default);
}

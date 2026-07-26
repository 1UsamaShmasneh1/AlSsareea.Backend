using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Catalog.Application;

public static class CatalogPermissions
{
    public const string View = "catalog.view"; public const string Manage = "catalog.manage"; public const string CategoriesManage = "catalog.categories.manage"; public const string SectionsManage = "catalog.sections.manage"; public const string ProductsView = "catalog.products.view"; public const string ProductsCreate = "catalog.products.create"; public const string ProductsUpdate = "catalog.products.update"; public const string ProductsPublish = "catalog.products.publish"; public const string ProductsLifecycle = "catalog.products.lifecycle"; public const string OptionsManage = "catalog.options.manage"; public const string InventoryManage = "catalog.inventory.manage"; public const string AvailabilityManage = "catalog.availability.manage"; public const string LocalizationManage = "catalog.localization.manage"; public const string ImagesManage = "catalog.images.manage";
}
public sealed record CatalogActor(Guid UserId, bool IsPlatformOperator);
public enum CatalogOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden }
public sealed record CatalogOperationResult<T>(CatalogOperationStatus Status, T? Value = default, string? ErrorCode = null);
public static class CatalogOperation { public static CatalogOperationResult<T> Success<T>(T value) => new(CatalogOperationStatus.Success, value); public static CatalogOperationResult<T> Created<T>(T value) => new(CatalogOperationStatus.Created, value); public static CatalogOperationResult<T> Failure<T>(CatalogOperationStatus status, string code) => new(status, default, code); }
public interface ICatalogRepository { Task<Domain.Catalog?> GetAsync(Guid merchantId, CancellationToken cancellationToken = default); Task AddAsync(Domain.Catalog catalog, CancellationToken cancellationToken = default); }
public interface IProductRepository { Task<Product?> GetAsync(Guid merchantId, ProductId id, bool tracked = true, CancellationToken cancellationToken = default); Task AddAsync(Product product, CancellationToken cancellationToken = default); }
public interface ICatalogService
{
    Task<CatalogOperationResult<CatalogResponse>> CreateCatalogAsync(Guid merchantId, CreateCatalogRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CatalogResponse>> GetCatalogAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CatalogResponse>> UpdateCatalogAsync(Guid merchantId, UpdateCatalogRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CatalogResponse>> ChangeCatalogStatusAsync(Guid merchantId, string operation, ConcurrencyRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CategoryResponse>> CreateCategoryAsync(Guid merchantId, CreateCategoryRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CategoryResponse>> UpdateCategoryAsync(Guid merchantId, Guid categoryId, UpdateCategoryRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CategoryResponse>> SetCategoryVisibilityAsync(Guid merchantId, Guid categoryId, bool visible, ConcurrencyRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CategoryResponse>> SetCategoryImageAsync(Guid merchantId, Guid categoryId, SetCatalogImageRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<IReadOnlyList<CategoryResponse>>> ReorderCategoriesAsync(Guid merchantId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> CreateSectionAsync(Guid merchantId, CreateMenuSectionRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<IReadOnlyList<MenuSectionResponse>>> ListSectionsAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> UpdateSectionAsync(Guid merchantId, Guid sectionId, UpdateMenuSectionRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> SetSectionVisibilityAsync(Guid merchantId, Guid sectionId, bool visible, ConcurrencyRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<IReadOnlyList<MenuSectionResponse>>> ReorderSectionsAsync(Guid merchantId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> AddSectionProductAsync(Guid merchantId, Guid sectionId, AddSectionProductRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> RemoveSectionProductAsync(Guid merchantId, Guid sectionId, Guid productId, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<MenuSectionResponse>> ReorderSectionProductsAsync(Guid merchantId, Guid sectionId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> CreateProductAsync(Guid merchantId, CreateProductRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> GetProductAsync(Guid merchantId, Guid productId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductListResponse>> SearchProductsAsync(Guid merchantId, int page, int pageSize, string? query, Guid? categoryId, short? status, short? inventory, bool? visible, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> UpdateProductAsync(Guid merchantId, Guid productId, UpdateProductRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> ChangeProductAsync(Guid merchantId, Guid productId, string operation, Guid concurrencyStamp, short? inventory, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> SetProductTranslationAsync(Guid merchantId, Guid productId, ProductTranslationRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> AddVariantAsync(Guid merchantId, Guid productId, AddVariantRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> UpdateVariantAsync(Guid merchantId, Guid productId, Guid variantId, UpdateVariantRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> SetDefaultVariantAsync(Guid merchantId, Guid productId, Guid variantId, ConcurrencyRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> ReorderVariantsAsync(Guid merchantId, Guid productId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> AddOptionGroupAsync(Guid merchantId, Guid productId, AddOptionGroupRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> UpdateOptionGroupAsync(Guid merchantId, Guid productId, Guid groupId, UpdateOptionGroupRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> ReorderOptionGroupsAsync(Guid merchantId, Guid productId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> AddOptionAsync(Guid merchantId, Guid productId, Guid groupId, AddOptionRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> UpdateOptionAsync(Guid merchantId, Guid productId, Guid groupId, Guid optionId, UpdateOptionRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> ReorderOptionsAsync(Guid merchantId, Guid productId, Guid groupId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> AddImageAsync(Guid merchantId, Guid productId, AddImageReferenceRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> UpdateImageAsync(Guid merchantId, Guid productId, Guid imageId, UpdateImageReferenceRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> SetPrimaryImageAsync(Guid merchantId, Guid productId, Guid imageId, ConcurrencyRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> RemoveImageAsync(Guid merchantId, Guid productId, Guid imageId, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ChildMutationResponse>> ReorderImagesAsync(Guid merchantId, Guid productId, ReorderRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<ProductResponse>> SetAvailabilityAsync(Guid merchantId, Guid productId, SetAvailabilityRequest request, CatalogActor actor, CancellationToken ct);
    Task<CatalogOperationResult<CatalogPriceResponse>> CalculatePriceAsync(Guid merchantId, Guid productId, PriceRequest request, CancellationToken ct);
}
public static class DependencyInjection { public static IServiceCollection AddCatalogApplication(this IServiceCollection services) => services; }

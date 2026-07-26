using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Catalog.Application;
using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Catalog.Infrastructure;

internal sealed class CatalogService(CatalogDbContext db, ICatalogRepository catalogs, IProductRepository products, IMerchantCatalogScopeProvider merchants, IClock clock) : ICatalogService, IProductSnapshotProvider
{
    public async Task<CatalogOperationResult<CatalogResponse>> CreateCatalogAsync(Guid merchantId, CreateCatalogRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<CatalogResponse>();
        if (await catalogs.GetAsync(merchantId, ct) is not null) return Conflict<CatalogResponse>("catalog_exists");
        Catalog.Domain.Catalog x = Catalog.Domain.Catalog.Create(CatalogId.New(), merchantId, r.Name, r.Description, r.DefaultLanguage, clock.UtcNow); await catalogs.AddAsync(x, ct); await db.SaveChangesAsync(ct); return CatalogOperation.Created(ToResponse(x));
    });
    public async Task<CatalogOperationResult<CatalogResponse>> GetCatalogAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, publicOnly || actor.IsPlatformOperator, ct);
        Catalog.Domain.Catalog? x = await catalogs.GetAsync(merchantId, ct);
        if (scope is null || x is null || publicOnly && (!scope.MerchantIsActive || x.Status != CatalogStatus.Active) || !publicOnly && !scope.CanManageMerchant) return NotFound<CatalogResponse>();
        return CatalogOperation.Success(ToResponse(x));
    });
    public async Task<CatalogOperationResult<CatalogResponse>> UpdateCatalogAsync(Guid merchantId, UpdateCatalogRequest r, CatalogActor actor, CancellationToken ct) => await WithCatalog(merchantId, actor, async x => { if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<CatalogResponse>(); x.Update(r.Name, r.Description, r.DefaultLanguage, clock.UtcNow); await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x)); }, ct);
    public async Task<CatalogOperationResult<CatalogResponse>> ChangeCatalogStatusAsync(Guid merchantId, string operation, ConcurrencyRequest r, CatalogActor actor, CancellationToken ct) => await WithCatalog(merchantId, actor, async x =>
    {
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<CatalogResponse>(); DateTime now = clock.UtcNow;
        switch (operation) { case "activate": x.Activate(await db.Products.AnyAsync(p => p.MerchantId == merchantId && p.Status == ProductStatus.Active, ct), now); break; case "suspend": x.Suspend(now); break; case "archive": x.Archive(now); break; default: return Invalid<CatalogResponse>("invalid_operation"); }
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x));
    }, ct);
    public async Task<CatalogOperationResult<CategoryResponse>> CreateCategoryAsync(Guid merchantId, CreateCategoryRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<CategoryResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        if (catalog is null) return NotFound<CategoryResponse>();
        CategoryId? parentId = r.ParentCategoryId.HasValue ? new CategoryId(r.ParentCategoryId.Value) : null;
        if (parentId.HasValue && !await db.Categories.AnyAsync(x => x.Id == parentId && x.MerchantId == merchantId && x.CatalogId == catalog.Id, ct)) return Invalid<CategoryResponse>("invalid_parent");
        Category category = Category.Create(CategoryId.New(), catalog.Id, merchantId, parentId, r.SortOrder, r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow);
        db.Categories.Add(category); await db.SaveChangesAsync(ct);
        return CatalogOperation.Created(ToResponse(category, r.Translation.LanguageCode, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, publicOnly || actor.IsPlatformOperator, ct);
        if (catalog is null || scope is null || publicOnly && (!scope.MerchantIsActive || catalog.Status != CatalogStatus.Active) || !publicOnly && !scope.CanManageMerchant) return NotFound<IReadOnlyList<CategoryResponse>>();
        IQueryable<Category> query = db.Categories.AsNoTracking().Include(x => x.Translations).Where(x => x.MerchantId == merchantId);
        if (publicOnly) query = query.Where(x => x.IsVisible);
        Category[] values = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToArrayAsync(ct);
        return CatalogOperation.Success<IReadOnlyList<CategoryResponse>>(values.Select(x => ToResponse(x, language, catalog.DefaultLanguage)).ToArray());
    });
    public async Task<CatalogOperationResult<CategoryResponse>> UpdateCategoryAsync(Guid merchantId, Guid categoryId, UpdateCategoryRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<CategoryResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        Category? category = await db.Categories.Include(x => x.Translations).SingleOrDefaultAsync(x => x.Id == new CategoryId(categoryId) && x.MerchantId == merchantId, ct);
        if (catalog is null || category is null) return NotFound<CategoryResponse>();
        if (category.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<CategoryResponse>();
        CategoryId? parentId = r.ParentCategoryId.HasValue ? new CategoryId(r.ParentCategoryId.Value) : null;
        if (parentId.HasValue && !await db.Categories.AnyAsync(x => x.Id == parentId && x.MerchantId == merchantId, ct)) return Invalid<CategoryResponse>("invalid_parent");
        HashSet<CategoryId> descendants = await Descendants(category.Id, ct);
        category.Update(parentId, descendants, r.SortOrder, r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(ToResponse(category, r.Translation.LanguageCode, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<CategoryResponse>> SetCategoryVisibilityAsync(Guid merchantId, Guid categoryId, bool visible, ConcurrencyRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<CategoryResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        Category? category = await db.Categories.Include(x => x.Translations).SingleOrDefaultAsync(x => x.Id == new CategoryId(categoryId) && x.MerchantId == merchantId, ct);
        if (catalog is null || category is null) return NotFound<CategoryResponse>();
        if (category.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<CategoryResponse>();
        category.SetVisibility(visible, clock.UtcNow); await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(ToResponse(category, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<IReadOnlyList<CategoryResponse>>> ReorderCategoriesAsync(Guid merchantId, ReorderRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<IReadOnlyList<CategoryResponse>>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        if (catalog is null || r.Items.Select(x => x.Id).Distinct().Count() != r.Items.Count) return Invalid<IReadOnlyList<CategoryResponse>>("invalid_reorder");
        Category[] values = await db.Categories.Include(x => x.Translations).Where(x => x.MerchantId == merchantId).ToArrayAsync(ct);
        if (values.Length != r.Items.Count) return Invalid<IReadOnlyList<CategoryResponse>>("incomplete_reorder");
        Dictionary<Guid, ReorderItemRequest> requests = r.Items.ToDictionary(x => x.Id);
        if (values.Any(x => !requests.TryGetValue(x.Id.Value, out ReorderItemRequest? item) || item.ConcurrencyStamp != x.ConcurrencyStamp)) return Conflict<IReadOnlyList<CategoryResponse>>();
        foreach (Category value in values) value.Reorder(requests[value.Id.Value].SortOrder, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return CatalogOperation.Success<IReadOnlyList<CategoryResponse>>(values.OrderBy(x => x.SortOrder).Select(x => ToResponse(x, null, catalog.DefaultLanguage)).ToArray());
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> CreateSectionAsync(Guid merchantId, CreateMenuSectionRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        if (catalog is null) return NotFound<MenuSectionResponse>();
        MenuSection section = MenuSection.Create(MenuSectionId.New(), catalog.Id, merchantId, r.SortOrder, r.AvailableFromUtc, r.AvailableUntilUtc, r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow);
        db.MenuSections.Add(section); await db.SaveChangesAsync(ct);
        return CatalogOperation.Created(ToResponse(section, r.Translation.LanguageCode, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<IReadOnlyList<MenuSectionResponse>>> ListSectionsAsync(Guid merchantId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, publicOnly || actor.IsPlatformOperator, ct);
        if (catalog is null || scope is null || publicOnly && (!scope.MerchantIsActive || catalog.Status != CatalogStatus.Active) || !publicOnly && !scope.CanManageMerchant) return NotFound<IReadOnlyList<MenuSectionResponse>>();
        IQueryable<MenuSection> query = db.MenuSections.AsNoTracking().Include(x => x.Translations).Include(x => x.Products).Where(x => x.MerchantId == merchantId);
        if (publicOnly) query = query.Where(x => x.IsVisible && (!x.AvailableFromUtc.HasValue || x.AvailableFromUtc <= clock.UtcNow) && (!x.AvailableUntilUtc.HasValue || x.AvailableUntilUtc > clock.UtcNow));
        MenuSection[] values = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToArrayAsync(ct);
        HashSet<Guid>? visibleProductIds = publicOnly
            ? (await db.Products.AsNoTracking().Where(x => x.MerchantId == merchantId && x.Status == ProductStatus.Active && x.IsVisible && (x.InventoryStatus == InventoryStatus.InStock || x.InventoryStatus == InventoryStatus.LowStock)).Select(x => x.Id.Value).ToArrayAsync(ct)).ToHashSet()
            : null;
        MenuSectionResponse[] responses = values.Select(x =>
        {
            MenuSectionResponse response = ToResponse(x, language, catalog.DefaultLanguage);
            return visibleProductIds is null ? response : response with { ProductIds = response.ProductIds.Where(visibleProductIds.Contains).ToArray() };
        }).ToArray();
        return CatalogOperation.Success<IReadOnlyList<MenuSectionResponse>>(responses);
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> UpdateSectionAsync(Guid merchantId, Guid sectionId, UpdateMenuSectionRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MenuSection? section = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == new MenuSectionId(sectionId) && x.MerchantId == merchantId, ct);
        if (catalog is null || section is null) return NotFound<MenuSectionResponse>();
        if (section.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<MenuSectionResponse>();
        section.Update(r.SortOrder, r.AvailableFromUtc, r.AvailableUntilUtc, r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(section, r.Translation.LanguageCode, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> SetSectionVisibilityAsync(Guid merchantId, Guid sectionId, bool visible, ConcurrencyRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MenuSection? section = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == new MenuSectionId(sectionId) && x.MerchantId == merchantId, ct);
        if (catalog is null || section is null) return NotFound<MenuSectionResponse>();
        if (section.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<MenuSectionResponse>();
        section.SetVisibility(visible, clock.UtcNow); await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(ToResponse(section, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<IReadOnlyList<MenuSectionResponse>>> ReorderSectionsAsync(Guid merchantId, ReorderRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<IReadOnlyList<MenuSectionResponse>>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        if (catalog is null || r.Items.Select(x => x.Id).Distinct().Count() != r.Items.Count) return Invalid<IReadOnlyList<MenuSectionResponse>>("invalid_reorder");
        MenuSection[] values = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).Where(x => x.MerchantId == merchantId).ToArrayAsync(ct);
        if (values.Length != r.Items.Count) return Invalid<IReadOnlyList<MenuSectionResponse>>("incomplete_reorder");
        Dictionary<Guid, ReorderItemRequest> requests = r.Items.ToDictionary(x => x.Id);
        if (values.Any(x => !requests.TryGetValue(x.Id.Value, out ReorderItemRequest? item) || item.ConcurrencyStamp != x.ConcurrencyStamp)) return Conflict<IReadOnlyList<MenuSectionResponse>>();
        foreach (MenuSection value in values) value.Reorder(requests[value.Id.Value].SortOrder, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return CatalogOperation.Success<IReadOnlyList<MenuSectionResponse>>(values.OrderBy(x => x.SortOrder).Select(x => ToResponse(x, null, catalog.DefaultLanguage)).ToArray());
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> AddSectionProductAsync(Guid merchantId, Guid sectionId, AddSectionProductRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MenuSection? section = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == new MenuSectionId(sectionId) && x.MerchantId == merchantId, ct);
        Product? product = await products.GetAsync(merchantId, new ProductId(r.ProductId), false, ct);
        if (catalog is null || section is null || product is null) return NotFound<MenuSectionResponse>();
        if (section.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<MenuSectionResponse>();
        section.AddProduct(product, r.SortOrder, clock.UtcNow); await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(ToResponse(section, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> RemoveSectionProductAsync(Guid merchantId, Guid sectionId, Guid productId, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MenuSection? section = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == new MenuSectionId(sectionId) && x.MerchantId == merchantId, ct);
        if (catalog is null || section is null) return NotFound<MenuSectionResponse>();
        section.RemoveProduct(new ProductId(productId), clock.UtcNow); await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(ToResponse(section, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<MenuSectionResponse>> ReorderSectionProductsAsync(Guid merchantId, Guid sectionId, ReorderRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<MenuSectionResponse>();
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        MenuSection? section = await db.MenuSections.Include(x => x.Translations).Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == new MenuSectionId(sectionId) && x.MerchantId == merchantId, ct);
        if (catalog is null || section is null) return NotFound<MenuSectionResponse>();
        if (r.Items.Any(x => x.ConcurrencyStamp != section.ConcurrencyStamp) || r.Items.Select(x => x.Id).Distinct().Count() != r.Items.Count) return Conflict<MenuSectionResponse>();
        section.ReorderProducts(r.Items.ToDictionary(x => new ProductId(x.Id), x => x.SortOrder), clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(section, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<ProductResponse>> CreateProductAsync(Guid merchantId, CreateProductRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ProductResponse>(); Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct); if (catalog is null) return NotFound<ProductResponse>();
        if (r.Sku is not null && await db.Products.AnyAsync(x => x.MerchantId == merchantId && x.Sku == r.Sku.Trim(), ct)) return Conflict<ProductResponse>("sku_exists");
        Product x = Product.Create(ProductId.New(), catalog.Id, merchantId, r.CategoryId.HasValue ? new CategoryId(r.CategoryId.Value) : null, r.Sku, r.BasePriceMinor, r.Currency, r.TaxCategoryReference, r.SortOrder, clock.UtcNow); x.SetTranslation(r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow); await products.AddAsync(x, ct); await db.SaveChangesAsync(ct); return CatalogOperation.Created(ToResponse(x, r.Translation.LanguageCode, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<ProductResponse>> GetProductAsync(Guid merchantId, Guid productId, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct); Product? x = await products.GetAsync(merchantId, new ProductId(productId), false, ct); MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, publicOnly || actor.IsPlatformOperator, ct);
        if (catalog is null || x is null || scope is null || publicOnly && (catalog.Status != CatalogStatus.Active || !scope.MerchantIsActive || !x.IsPurchasable) || !publicOnly && !scope.CanManageMerchant) return NotFound<ProductResponse>(); return CatalogOperation.Success(ToResponse(x, language, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<ProductListResponse>> SearchProductsAsync(Guid merchantId, int page, int pageSize, string? query, Guid? categoryId, short? status, short? inventory, bool? visible, bool publicOnly, string? language, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (page < 1 || pageSize is < 1 or > 100) return Invalid<ProductListResponse>("invalid_pagination"); Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct); MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, publicOnly || actor.IsPlatformOperator, ct); if (catalog is null || scope is null || publicOnly && (catalog.Status != CatalogStatus.Active || !scope.MerchantIsActive) || !publicOnly && !scope.CanManageMerchant) return NotFound<ProductListResponse>();
        IQueryable<Product> q = db.Products.AsNoTracking().Include(x => x.Translations).Where(x => x.MerchantId == merchantId);
        if (publicOnly) q = q.Where(x => x.Status == ProductStatus.Active && x.IsVisible && (x.InventoryStatus == InventoryStatus.InStock || x.InventoryStatus == InventoryStatus.LowStock));
        if (!string.IsNullOrWhiteSpace(query)) q = q.Where(x => x.Sku != null && EF.Functions.ILike(x.Sku, $"%{query}%") || x.Translations.Any(t => EF.Functions.ILike(t.SearchText, $"%{query}%")));
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == new CategoryId(categoryId.Value)); if (status.HasValue && Enum.IsDefined((ProductStatus)status.Value)) q = q.Where(x => x.Status == (ProductStatus)status.Value); if (inventory.HasValue && Enum.IsDefined((InventoryStatus)inventory.Value)) q = q.Where(x => x.InventoryStatus == (InventoryStatus)inventory.Value); if (visible.HasValue) q = q.Where(x => x.IsVisible == visible.Value);
        int total = await q.CountAsync(ct); Product[] items = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct); return CatalogOperation.Success(new ProductListResponse(items.Select(x => ToResponse(x, language, catalog.DefaultLanguage)).ToArray(), page, pageSize, total));
    });
    public async Task<CatalogOperationResult<ProductResponse>> UpdateProductAsync(Guid merchantId, Guid productId, UpdateProductRequest r, CatalogActor actor, CancellationToken ct) => await WithProduct(merchantId, productId, actor, async (x, catalog) => { if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ProductResponse>(); x.UpdateCommercial(r.BasePriceMinor, r.Currency, r.TaxCategoryReference, r.CategoryId.HasValue ? new CategoryId(r.CategoryId.Value) : null, r.SortOrder, clock.UtcNow); await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x, null, catalog.DefaultLanguage)); }, ct);
    public async Task<CatalogOperationResult<ProductResponse>> ChangeProductAsync(Guid merchantId, Guid productId, string operation, Guid stamp, short? inventory, CatalogActor actor, CancellationToken ct) => await WithProduct(merchantId, productId, actor, async (x, catalog) =>
    {
        if (x.ConcurrencyStamp != stamp) return Conflict<ProductResponse>(); DateTime now = clock.UtcNow;
        switch (operation) { case "publish": x.Publish(catalog.DefaultLanguage, now); break; case "suspend": x.Suspend(now); break; case "archive": x.Archive(now); break; case "show": x.SetVisibility(true, now); break; case "hide": x.SetVisibility(false, now); break; case "inventory": if (!inventory.HasValue || !Enum.IsDefined((InventoryStatus)inventory.Value)) return Invalid<ProductResponse>("invalid_inventory"); x.SetInventory((InventoryStatus)inventory.Value, now); break; default: return Invalid<ProductResponse>("invalid_operation"); }
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x, null, catalog.DefaultLanguage));
    }, ct);
    public Task<CatalogOperationResult<ProductResponse>> SetProductTranslationAsync(Guid merchantId, Guid productId, ProductTranslationRequest r, CatalogActor actor, CancellationToken ct) => WithProduct(merchantId, productId, actor, async (x, catalog) =>
    {
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ProductResponse>();
        x.SetTranslation(r.Translation.LanguageCode, r.Translation.Name, r.Translation.Description, clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x, r.Translation.LanguageCode, catalog.DefaultLanguage));
    }, ct);
    public async Task<CatalogOperationResult<ChildMutationResponse>> AddVariantAsync(Guid merchantId, Guid productId, AddVariantRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ChildMutationResponse>();
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (x is null) return NotFound<ChildMutationResponse>();
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ChildMutationResponse>();
        if (r.Sku is not null && await db.ProductVariants.AnyAsync(v => v.MerchantId == merchantId && v.Sku == r.Sku.Trim(), ct)) return Conflict<ChildMutationResponse>("sku_exists");
        if (!Enum.IsDefined((InventoryStatus)r.InventoryStatus)) return Invalid<ChildMutationResponse>("invalid_inventory");
        ProductVariant child = x.AddVariant(r.Translation.LanguageCode, r.Translation.Name, r.Sku, r.PriceAdjustmentMinor, (InventoryStatus)r.InventoryStatus, r.IsDefault, r.SortOrder, clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Created(new ChildMutationResponse(x.Id.Value, child.Id.Value, x.CurrentVersion, x.ConcurrencyStamp));
    });
    public Task<CatalogOperationResult<ChildMutationResponse>> UpdateVariantAsync(Guid merchantId, Guid productId, Guid variantId, UpdateVariantRequest r, CatalogActor actor, CancellationToken ct)
    {
        if (!Enum.IsDefined((InventoryStatus)r.InventoryStatus)) return Task.FromResult(Invalid<ChildMutationResponse>("invalid_inventory"));
        return MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => x.UpdateVariant(new ProductVariantId(variantId), r.Translation.LanguageCode, r.Translation.Name, r.Sku, r.PriceAdjustmentMinor, (InventoryStatus)r.InventoryStatus, r.IsVisible, r.SortOrder, clock.UtcNow).Id.Value, ct);
    }
    public Task<CatalogOperationResult<ChildMutationResponse>> SetDefaultVariantAsync(Guid merchantId, Guid productId, Guid variantId, ConcurrencyRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => { x.SetDefaultVariant(new ProductVariantId(variantId), clock.UtcNow); return variantId; }, ct);
    public Task<CatalogOperationResult<ChildMutationResponse>> ReorderVariantsAsync(Guid merchantId, Guid productId, ReorderRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, ReorderStamp(r), actor, x => { x.ReorderVariants(r.Items.ToDictionary(i => new ProductVariantId(i.Id), i => i.SortOrder), clock.UtcNow); return productId; }, ct);
    public async Task<CatalogOperationResult<ChildMutationResponse>> AddOptionGroupAsync(Guid merchantId, Guid productId, AddOptionGroupRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ChildMutationResponse>();
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (x is null) return NotFound<ChildMutationResponse>();
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ChildMutationResponse>();
        if (!Enum.IsDefined((SelectionType)r.SelectionType)) return Invalid<ChildMutationResponse>("invalid_selection_type");
        OptionGroup child = x.AddOptionGroup(r.Translation.LanguageCode, r.Translation.Name, (SelectionType)r.SelectionType, r.IsRequired, r.MinSelections, r.MaxSelections, r.SortOrder, clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Created(new ChildMutationResponse(x.Id.Value, child.Id.Value, x.CurrentVersion, x.ConcurrencyStamp));
    });
    public Task<CatalogOperationResult<ChildMutationResponse>> UpdateOptionGroupAsync(Guid merchantId, Guid productId, Guid groupId, UpdateOptionGroupRequest r, CatalogActor actor, CancellationToken ct)
    {
        if (!Enum.IsDefined((SelectionType)r.SelectionType)) return Task.FromResult(Invalid<ChildMutationResponse>("invalid_selection_type"));
        return MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => x.UpdateOptionGroup(new OptionGroupId(groupId), r.Translation.LanguageCode, r.Translation.Name, (SelectionType)r.SelectionType, r.IsRequired, r.MinSelections, r.MaxSelections, r.SortOrder, clock.UtcNow).Id.Value, ct);
    }
    public Task<CatalogOperationResult<ChildMutationResponse>> ReorderOptionGroupsAsync(Guid merchantId, Guid productId, ReorderRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, ReorderStamp(r), actor, x => { x.ReorderOptionGroups(r.Items.ToDictionary(i => new OptionGroupId(i.Id), i => i.SortOrder), clock.UtcNow); return productId; }, ct);
    public async Task<CatalogOperationResult<ChildMutationResponse>> AddOptionAsync(Guid merchantId, Guid productId, Guid groupId, AddOptionRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ChildMutationResponse>();
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (x is null) return NotFound<ChildMutationResponse>();
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ChildMutationResponse>();
        OptionGroup? group = x.OptionGroups.SingleOrDefault(g => g.Id == new OptionGroupId(groupId));
        if (group is null) return NotFound<ChildMutationResponse>();
        ProductOption child = group.AddOption(r.Translation.LanguageCode, r.Translation.Name, r.PriceAdjustmentMinor, r.IsDefault, r.IsAvailable, r.SortOrder, clock.UtcNow);
        x.MarkOptionsChanged(clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Created(new ChildMutationResponse(x.Id.Value, child.Id.Value, x.CurrentVersion, x.ConcurrencyStamp));
    });
    public Task<CatalogOperationResult<ChildMutationResponse>> UpdateOptionAsync(Guid merchantId, Guid productId, Guid groupId, Guid optionId, UpdateOptionRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => x.UpdateOption(new OptionGroupId(groupId), new ProductOptionId(optionId), r.Translation.LanguageCode, r.Translation.Name, r.PriceAdjustmentMinor, r.IsDefault, r.IsAvailable, r.SortOrder, clock.UtcNow).Id.Value, ct);
    public Task<CatalogOperationResult<ChildMutationResponse>> ReorderOptionsAsync(Guid merchantId, Guid productId, Guid groupId, ReorderRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, ReorderStamp(r), actor, x => { x.ReorderOptions(new OptionGroupId(groupId), r.Items.ToDictionary(i => new ProductOptionId(i.Id), i => i.SortOrder), clock.UtcNow); return groupId; }, ct);
    public async Task<CatalogOperationResult<ChildMutationResponse>> AddImageAsync(Guid merchantId, Guid productId, AddImageReferenceRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ChildMutationResponse>();
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (x is null) return NotFound<ChildMutationResponse>();
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ChildMutationResponse>();
        ProductImageReference child = x.AddImage(r.MediaId, r.ExternalReference, r.AltText, r.SortOrder, r.IsPrimary, clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Created(new ChildMutationResponse(x.Id.Value, child.Id.Value, x.CurrentVersion, x.ConcurrencyStamp));
    });
    public Task<CatalogOperationResult<ChildMutationResponse>> UpdateImageAsync(Guid merchantId, Guid productId, Guid imageId, UpdateImageReferenceRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => x.UpdateImage(new ProductImageReferenceId(imageId), r.MediaId, r.ExternalReference, r.AltText, r.SortOrder, r.IsPrimary, clock.UtcNow).Id.Value, ct);
    public Task<CatalogOperationResult<ChildMutationResponse>> SetPrimaryImageAsync(Guid merchantId, Guid productId, Guid imageId, ConcurrencyRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, r.ConcurrencyStamp, actor, x => { x.SetPrimaryImage(new ProductImageReferenceId(imageId), clock.UtcNow); return imageId; }, ct);
    public Task<CatalogOperationResult<ChildMutationResponse>> RemoveImageAsync(Guid merchantId, Guid productId, Guid imageId, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, null, actor, x => { x.RemoveImage(new ProductImageReferenceId(imageId), clock.UtcNow); return imageId; }, ct);
    public Task<CatalogOperationResult<ChildMutationResponse>> ReorderImagesAsync(Guid merchantId, Guid productId, ReorderRequest r, CatalogActor actor, CancellationToken ct) =>
        MutateChild(merchantId, productId, ReorderStamp(r), actor, x => { x.ReorderImages(r.Items.ToDictionary(i => new ProductImageReferenceId(i.Id), i => i.SortOrder), clock.UtcNow); return productId; }, ct);
    public async Task<CatalogOperationResult<ProductResponse>> SetAvailabilityAsync(Guid merchantId, Guid productId, SetAvailabilityRequest r, CatalogActor actor, CancellationToken ct) => await Run(async () =>
    {
        MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, actor.UserId, actor.IsPlatformOperator, ct);
        if (scope?.CanManageMerchant != true) return Forbidden<ProductResponse>();
        if (scope.RestrictedBranchId.HasValue && r.Periods.Any(p => p.BranchId != scope.RestrictedBranchId)) return Forbidden<ProductResponse>();
        foreach (AvailabilityPeriodRequest period in r.Periods)
            if (period.BranchId.HasValue && !await merchants.IsOperationalBranchAsync(merchantId, period.BranchId.Value, ct)) return Invalid<ProductResponse>("invalid_branch");
        Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct);
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (catalog is null || x is null) return NotFound<ProductResponse>();
        if (x.ConcurrencyStamp != r.ConcurrencyStamp) return Conflict<ProductResponse>();
        x.ReplaceAvailability(r.Periods.Select(p => (p.BranchId, p.DayOfWeek, p.StartLocalTime, p.EndLocalTime, p.TimeZoneId)), clock.UtcNow);
        await db.SaveChangesAsync(ct); return CatalogOperation.Success(ToResponse(x, null, catalog.DefaultLanguage));
    });
    public async Task<CatalogOperationResult<CatalogPriceResponse>> CalculatePriceAsync(Guid merchantId, Guid productId, PriceRequest r, CancellationToken ct) => await Run(async () =>
    {
        Product? x = await products.GetAsync(merchantId, new ProductId(productId), false, ct); Catalog.Domain.Catalog? catalog = await catalogs.GetAsync(merchantId, ct); MerchantCatalogScope? scope = await merchants.GetScopeAsync(merchantId, Guid.Empty, true, ct); if (x is null || catalog is null || scope?.MerchantIsActive != true || catalog.Status != CatalogStatus.Active || !x.IsPurchasable) return NotFound<CatalogPriceResponse>();
        if (r.OptionIds.Count != r.OptionIds.Distinct().Count()) return Invalid<CatalogPriceResponse>("duplicate_option");
        ProductVariant? variant = r.VariantId.HasValue ? x.Variants.SingleOrDefault(v => v.Id == new ProductVariantId(r.VariantId.Value)) : null; if (r.VariantId.HasValue && (variant is null || !variant.IsVisible || variant.InventoryStatus is InventoryStatus.OutOfStock or InventoryStatus.Unavailable)) return Invalid<CatalogPriceResponse>("invalid_variant");
        ProductOption[] selected = x.OptionGroups.SelectMany(g => g.Options).Where(o => r.OptionIds.Contains(o.Id.Value)).ToArray(); if (selected.Length != r.OptionIds.Count || selected.Any(o => !o.IsAvailable)) return Invalid<CatalogPriceResponse>("invalid_option");
        foreach (OptionGroup group in x.OptionGroups.Where(g => g.IsVisible)) { int count = selected.Count(o => o.OptionGroupId == group.Id); if (count < group.MinSelections || count > group.MaxSelections) return Invalid<CatalogPriceResponse>("selection_limits"); }
        string language = r.Language ?? catalog.DefaultLanguage; long va = variant?.PriceAdjustmentMinor ?? 0; long oa = selected.Sum(o => o.PriceAdjustmentMinor); long total = x.BasePriceMinor + va + oa; if (total < 0) return Invalid<CatalogPriceResponse>("negative_total");
        SelectedPriceItem? vr = variant is null ? null : new(variant.Id.Value, variant.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Name ?? variant.Translations.First().Name, va); SelectedPriceItem[] options = selected.Select(o => new SelectedPriceItem(o.Id.Value, o.Translations.FirstOrDefault(t => t.LanguageCode == language)?.Name ?? o.Translations.First().Name, o.PriceAdjustmentMinor)).ToArray(); return CatalogOperation.Success(new CatalogPriceResponse(x.Id.Value, x.CurrentVersion, x.Currency, x.BasePriceMinor, va, oa, total, vr, options));
    });
    public async Task<ProductSnapshot?> BuildAsync(Guid merchantId, Guid productId, Guid? variantId, IReadOnlyList<Guid> optionIds, string language, CancellationToken ct = default) { CatalogOperationResult<CatalogPriceResponse> price = await CalculatePriceAsync(merchantId, productId, new PriceRequest(variantId, optionIds, language), ct); if (price.Value is null) return null; Product x = (await products.GetAsync(merchantId, new ProductId(productId), false, ct))!; ProductTranslation text = x.Translations.FirstOrDefault(t => t.LanguageCode == language) ?? x.Translations.First(); Dictionary<Guid, OptionGroup> groups = x.OptionGroups.ToDictionary(g => g.Id.Value); return new ProductSnapshot(productId, x.CurrentVersion, merchantId, x.CatalogId.Value, text.Name, x.BasePriceMinor, x.Currency, variantId, price.Value.SelectedVariant?.Name, price.Value.VariantAdjustmentMinor, price.Value.SelectedOptions.Select(o => { ProductOption po = x.OptionGroups.SelectMany(g => g.Options).Single(v => v.Id.Value == o.Id); OptionGroup g = groups[po.OptionGroupId.Value]; return new SnapshotOption(g.Id.Value, g.Translations.First().Name, o.Id, o.Name, o.AdjustmentMinor); }).ToArray(), x.TaxCategoryReference, price.Value.TotalPriceMinor, clock.UtcNow); }
    private async Task<HashSet<CategoryId>> Descendants(CategoryId root, CancellationToken ct)
    {
        CategoryId[] all = await db.Categories
            .Where(x => x.ParentCategoryId.HasValue)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        Dictionary<CategoryId, CategoryId?> parents = await db.Categories
            .ToDictionaryAsync(x => x.Id, x => x.ParentCategoryId, ct);
        return all.Where(id =>
        {
            CategoryId? current = parents[id];
            while (current.HasValue)
            {
                if (current.Value == root) return true;
                current = parents.GetValueOrDefault(current.Value);
            }
            return false;
        }).ToHashSet();
    }
    private async Task<CatalogOperationResult<ChildMutationResponse>> MutateChild(Guid merchantId, Guid productId, Guid? stamp, CatalogActor actor, Func<Product, Guid> mutate, CancellationToken ct) => await Run(async () =>
    {
        if (!await CanManage(merchantId, actor, ct)) return Forbidden<ChildMutationResponse>();
        Product? product = await products.GetAsync(merchantId, new ProductId(productId), true, ct);
        if (product is null) return NotFound<ChildMutationResponse>();
        if (stamp.HasValue && product.ConcurrencyStamp != stamp.Value) return Conflict<ChildMutationResponse>();
        Guid childId = mutate(product); await db.SaveChangesAsync(ct);
        return CatalogOperation.Success(new ChildMutationResponse(product.Id.Value, childId, product.CurrentVersion, product.ConcurrencyStamp));
    });
    private static Guid ReorderStamp(ReorderRequest request) =>
        request.Items.Count == 0 || request.Items.Select(x => x.ConcurrencyStamp).Distinct().Count() != 1
            ? Guid.Empty
            : request.Items[0].ConcurrencyStamp;
    private async Task<bool> CanManage(Guid merchantId, CatalogActor actor, CancellationToken ct) => (await merchants.GetScopeAsync(merchantId, actor.UserId, actor.IsPlatformOperator, ct))?.CanManageMerchant == true;
    private static async Task<CatalogOperationResult<T>> Run<T>(Func<Task<CatalogOperationResult<T>>> op) { try { return await op(); } catch (DomainException) { return Invalid<T>("domain_validation"); } catch (DbUpdateConcurrencyException) { return Conflict<T>(); } catch (DbUpdateException) { return Conflict<T>("database_constraint"); } }
    private async Task<CatalogOperationResult<CatalogResponse>> WithCatalog(Guid merchantId, CatalogActor actor, Func<Catalog.Domain.Catalog, Task<CatalogOperationResult<CatalogResponse>>> op, CancellationToken ct) => await Run(async () => { if (!await CanManage(merchantId, actor, ct)) return Forbidden<CatalogResponse>(); Catalog.Domain.Catalog? x = await catalogs.GetAsync(merchantId, ct); return x is null ? NotFound<CatalogResponse>() : await op(x); });
    private async Task<CatalogOperationResult<ProductResponse>> WithProduct(Guid merchantId, Guid productId, CatalogActor actor, Func<Product, Catalog.Domain.Catalog, Task<CatalogOperationResult<ProductResponse>>> op, CancellationToken ct) => await Run(async () => { if (!await CanManage(merchantId, actor, ct)) return Forbidden<ProductResponse>(); Catalog.Domain.Catalog? c = await catalogs.GetAsync(merchantId, ct); Product? x = await products.GetAsync(merchantId, new ProductId(productId), true, ct); return c is null || x is null ? NotFound<ProductResponse>() : await op(x, c); });
    private static CatalogResponse ToResponse(Catalog.Domain.Catalog x) => new(x.Id.Value, x.MerchantId, x.Name, x.Description, x.DefaultLanguage, (short)x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp);
    private static CategoryResponse ToResponse(Category x, string? language, string fallback)
    {
        CategoryTranslation text = x.Translations.FirstOrDefault(v => v.LanguageCode == language)
            ?? x.Translations.FirstOrDefault(v => v.LanguageCode == fallback)
            ?? x.Translations.First();
        return new(x.Id.Value, x.CatalogId.Value, x.MerchantId, x.ParentCategoryId?.Value, x.SortOrder, x.IsVisible, new(text.LanguageCode, text.Name, text.Description), x.ConcurrencyStamp);
    }
    private static MenuSectionResponse ToResponse(MenuSection x, string? language, string fallback)
    {
        MenuSectionTranslation text = x.Translations.FirstOrDefault(v => v.LanguageCode == language)
            ?? x.Translations.FirstOrDefault(v => v.LanguageCode == fallback)
            ?? x.Translations.First();
        return new(x.Id.Value, x.CatalogId.Value, x.MerchantId, x.SortOrder, x.IsVisible, x.AvailableFromUtc, x.AvailableUntilUtc, new(text.LanguageCode, text.Name, text.Description), x.Products.OrderBy(p => p.SortOrder).Select(p => p.ProductId.Value).ToArray(), x.ConcurrencyStamp);
    }
    private static ProductResponse ToResponse(Product x, string? language, string fallback) { ProductTranslation t = x.Translations.FirstOrDefault(v => v.LanguageCode == language) ?? x.Translations.FirstOrDefault(v => v.LanguageCode == fallback) ?? x.Translations.First(); return new(x.Id.Value, x.CatalogId.Value, x.MerchantId, x.CategoryId?.Value, x.Sku, x.BasePriceMinor, x.Currency, x.TaxCategoryReference, (short)x.Status, (short)x.InventoryStatus, x.SortOrder, x.IsVisible, x.IsFeatured, x.CurrentVersion, new(t.LanguageCode, t.Name, t.Description), x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyStamp); }
    private static CatalogOperationResult<T> Invalid<T>(string code) => CatalogOperation.Failure<T>(CatalogOperationStatus.Invalid, code); private static CatalogOperationResult<T> Conflict<T>(string code = "concurrency_conflict") => CatalogOperation.Failure<T>(CatalogOperationStatus.Conflict, code); private static CatalogOperationResult<T> NotFound<T>() => CatalogOperation.Failure<T>(CatalogOperationStatus.NotFound, "not_found"); private static CatalogOperationResult<T> Forbidden<T>() => CatalogOperation.Failure<T>(CatalogOperationStatus.Forbidden, "forbidden");
}

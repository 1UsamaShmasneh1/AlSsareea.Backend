using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Catalog.Domain;

public sealed class Catalog : AggregateRoot<CatalogId>
{
    private Catalog(CatalogId id) : base(id) { Name = DefaultLanguage = null!; }
    private Catalog(CatalogId id, Guid merchantId, string name, string? description, string language, DateTime now) : base(id)
    { MerchantId = CatalogRules.Id(merchantId, nameof(merchantId)); Name = CatalogRules.Required(name, 200, nameof(name)); Description = CatalogRules.Optional(description, 2000, nameof(description)); DefaultLanguage = CatalogRules.Language(language); Status = CatalogStatus.Draft; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    public Guid MerchantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string DefaultLanguage { get; private set; } = null!;
    public CatalogStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public static Catalog Create(CatalogId id, Guid merchantId, string name, string? description, string language, DateTime now) { CatalogRules.Utc(now); Catalog value = new(id, merchantId, name, description, language, now); value.RaiseDomainEvent(new CatalogCreatedDomainEvent(id, merchantId, now)); return value; }
    public void Update(string name, string? description, string language, DateTime now) { EnsureMutable(); Name = CatalogRules.Required(name, 200, nameof(name)); Description = CatalogRules.Optional(description, 2000, nameof(description)); DefaultLanguage = CatalogRules.Language(language); Touch(now); }
    public void Activate(bool hasPublishableProduct, DateTime now) { if (Status is not (CatalogStatus.Draft or CatalogStatus.Suspended) || !hasPublishableProduct) throw new DomainException("Catalog cannot be activated without a publishable product."); Status = CatalogStatus.Active; Touch(now); RaiseDomainEvent(new CatalogActivatedDomainEvent(Id, MerchantId, now)); }
    public void Suspend(DateTime now) { if (Status != CatalogStatus.Active) throw new DomainException("Only an active catalog can be suspended."); Status = CatalogStatus.Suspended; Touch(now); }
    public void Archive(DateTime now) { if (Status == CatalogStatus.Archived) throw new DomainException("Catalog is already archived."); Status = CatalogStatus.Archived; Touch(now); }
    private void EnsureMutable() { if (Status == CatalogStatus.Archived) throw new DomainException("Archived catalog cannot be modified."); }
    private void Touch(DateTime now) { CatalogRules.Utc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}

public sealed class Category : AggregateRoot<CategoryId>
{
    private readonly List<CategoryTranslation> _translations = [];
    private Category(CategoryId id) : base(id) { }
    private Category(CategoryId id, CatalogId catalogId, Guid merchantId, CategoryId? parentId, int sort, DateTime now) : base(id) { CatalogId = catalogId; MerchantId = CatalogRules.Id(merchantId, nameof(merchantId)); if (parentId == id) throw new DomainException("Category cannot be its own parent."); ParentCategoryId = parentId; CatalogRules.Sort(sort); SortOrder = sort; IsVisible = true; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    public CatalogId CatalogId { get; private set; }
    public Guid MerchantId { get; private set; }
    public CategoryId? ParentCategoryId { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsVisible { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<CategoryTranslation> Translations => _translations;
    public static Category Create(CategoryId id, CatalogId catalogId, Guid merchantId, CategoryId? parentId, int sort, string language, string name, string? description, DateTime now) { CatalogRules.Utc(now); Category x = new(id, catalogId, merchantId, parentId, sort, now); x.SetTranslation(language, name, description, now); return x; }
    public void Move(CategoryId? parentId, IReadOnlySet<CategoryId> descendants, DateTime now) { if (parentId == Id || (parentId.HasValue && descendants.Contains(parentId.Value))) throw new DomainException("Category hierarchy cannot contain a cycle."); ParentCategoryId = parentId; Touch(now); }
    public void SetTranslation(string language, string name, string? description, DateTime now) { string lang = CatalogRules.Language(language); CategoryTranslation? existing = _translations.SingleOrDefault(x => x.LanguageCode == lang); if (existing is null) _translations.Add(new CategoryTranslation(Id, lang, name, description)); else existing.Update(name, description); Touch(now); }
    public void SetVisibility(bool visible, DateTime now) { IsVisible = visible; Touch(now); }
    public void SetImage(Guid? mediaAssetId, DateTime now) { if (mediaAssetId == Guid.Empty) throw new DomainException("Media asset ID cannot be empty."); MediaAssetId = mediaAssetId; Touch(now); }
    public void Reorder(int order, DateTime now) { CatalogRules.Sort(order); SortOrder = order; Touch(now); }
    public void Update(CategoryId? parentId, IReadOnlySet<CategoryId> descendants, int sortOrder, string language, string name, string? description, DateTime now)
    {
        Move(parentId, descendants, now);
        Reorder(sortOrder, now);
        SetTranslation(language, name, description, now);
    }
    private void Touch(DateTime now) { CatalogRules.Utc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}
public sealed class CategoryTranslation { private CategoryTranslation() { LanguageCode = Name = null!; } internal CategoryTranslation(CategoryId id, string language, string name, string? description) { CategoryId = id; LanguageCode = CatalogRules.Language(language); Name = CatalogRules.Required(name, 200, nameof(name)); Description = CatalogRules.Optional(description, 2000, nameof(description)); SearchText = Name.ToLowerInvariant(); } public CategoryId CategoryId { get; private set; } public string LanguageCode { get; private set; } public string Name { get; private set; } public string? Description { get; private set; } public string SearchText { get; private set; } = null!; internal void Update(string name, string? description) { Name = CatalogRules.Required(name, 200, nameof(name)); Description = CatalogRules.Optional(description, 2000, nameof(description)); SearchText = Name.ToLowerInvariant(); } }

public sealed class MenuSection : AggregateRoot<MenuSectionId>
{
    private readonly List<MenuSectionTranslation> _translations = []; private readonly List<MenuSectionProduct> _products = [];
    private MenuSection(MenuSectionId id) : base(id) { }
    private MenuSection(MenuSectionId id, CatalogId catalogId, Guid merchantId, int sort, DateTime? from, DateTime? until, DateTime now) : base(id) { CatalogId = catalogId; MerchantId = CatalogRules.Id(merchantId, nameof(merchantId)); CatalogRules.Sort(sort); if (from.HasValue) CatalogRules.Utc(from.Value); if (until.HasValue) CatalogRules.Utc(until.Value); if (from >= until) throw new DomainException("Section availability range is invalid."); SortOrder = sort; IsVisible = true; AvailableFromUtc = from; AvailableUntilUtc = until; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    public CatalogId CatalogId { get; private set; }
    public Guid MerchantId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsVisible { get; private set; }
    public DateTime? AvailableFromUtc { get; private set; }
    public DateTime? AvailableUntilUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<MenuSectionTranslation> Translations => _translations; public IReadOnlyCollection<MenuSectionProduct> Products => _products;
    public static MenuSection Create(MenuSectionId id, CatalogId catalogId, Guid merchantId, int sort, DateTime? from, DateTime? until, string language, string name, string? description, DateTime now) { MenuSection x = new(id, catalogId, merchantId, sort, from, until, now); x._translations.Add(new MenuSectionTranslation(id, language, name, description)); return x; }
    public void AddProduct(Product product, int sort, DateTime now) { if (product.MerchantId != MerchantId || product.CatalogId != CatalogId) throw new DomainException("Product and section scope must match."); if (_products.Any(x => x.ProductId == product.Id)) throw new DomainException("Product is already in the section."); CatalogRules.Sort(sort); _products.Add(new MenuSectionProduct(Id, product.Id, sort)); Touch(now); }
    public void RemoveProduct(ProductId productId, DateTime now) { if (_products.RemoveAll(x => x.ProductId == productId) == 0) throw new DomainException("Product is not in the section."); Touch(now); }
    public void SetVisibility(bool visible, DateTime now) { IsVisible = visible; Touch(now); }
    public void Reorder(int sortOrder, DateTime now) { CatalogRules.Sort(sortOrder); SortOrder = sortOrder; Touch(now); }
    public void Update(int sortOrder, DateTime? from, DateTime? until, string language, string name, string? description, DateTime now)
    {
        CatalogRules.Sort(sortOrder);
        if (from.HasValue) CatalogRules.Utc(from.Value);
        if (until.HasValue) CatalogRules.Utc(until.Value);
        if (from >= until) throw new DomainException("Section availability range is invalid.");
        SortOrder = sortOrder;
        AvailableFromUtc = from;
        AvailableUntilUtc = until;
        SetTranslation(language, name, description, now);
    }
    public void ReorderProducts(IReadOnlyDictionary<ProductId, int> orders, DateTime now)
    {
        if (orders.Count != _products.Count || _products.Any(x => !orders.ContainsKey(x.ProductId))) throw new DomainException("All section products must be included exactly once.");
        foreach (MenuSectionProduct product in _products) product.Reorder(orders[product.ProductId]);
        Touch(now);
    }
    public void SetTranslation(string language, string name, string? description, DateTime now)
    {
        string value = CatalogRules.Language(language);
        MenuSectionTranslation? translation = _translations.SingleOrDefault(x => x.LanguageCode == value);
        if (translation is null) _translations.Add(new MenuSectionTranslation(Id, value, name, description));
        else translation.Update(name, description);
        Touch(now);
    }
    private void Touch(DateTime now) { CatalogRules.Utc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
}
public sealed class MenuSectionTranslation { private MenuSectionTranslation() { LanguageCode = Name = null!; } internal MenuSectionTranslation(MenuSectionId id, string language, string name, string? description) { MenuSectionId = id; LanguageCode = CatalogRules.Language(language); Update(name, description); } public MenuSectionId MenuSectionId { get; private set; } public string LanguageCode { get; private set; } public string Name { get; private set; } = null!; public string? Description { get; private set; } internal void Update(string name, string? description) { Name = CatalogRules.Required(name, 200, nameof(name)); Description = CatalogRules.Optional(description, 2000, nameof(description)); } }
public sealed class MenuSectionProduct { private MenuSectionProduct() { } internal MenuSectionProduct(MenuSectionId sectionId, ProductId productId, int sort) { MenuSectionId = sectionId; ProductId = productId; SortOrder = sort; } public MenuSectionId MenuSectionId { get; private set; } public ProductId ProductId { get; private set; } public int SortOrder { get; private set; } internal void Reorder(int sort) { CatalogRules.Sort(sort); SortOrder = sort; } }

public sealed record CatalogCreatedDomainEvent(CatalogId CatalogId, Guid MerchantId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record CatalogActivatedDomainEvent(CatalogId CatalogId, Guid MerchantId, DateTime OccurredAtUtc) : IDomainEvent;
public sealed record ProductChangedDomainEvent(ProductId ProductId, int Version, DateTime OccurredAtUtc) : IDomainEvent;

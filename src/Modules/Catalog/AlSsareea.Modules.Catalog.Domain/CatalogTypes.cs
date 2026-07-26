using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Catalog.Domain;

public readonly record struct CatalogId { public CatalogId(Guid value) => Value = CatalogRules.Id(value, nameof(CatalogId)); public Guid Value { get; } public static CatalogId New() => new(Guid.NewGuid()); }
public readonly record struct CategoryId { public CategoryId(Guid value) => Value = CatalogRules.Id(value, nameof(CategoryId)); public Guid Value { get; } public static CategoryId New() => new(Guid.NewGuid()); }
public readonly record struct MenuSectionId { public MenuSectionId(Guid value) => Value = CatalogRules.Id(value, nameof(MenuSectionId)); public Guid Value { get; } public static MenuSectionId New() => new(Guid.NewGuid()); }
public readonly record struct ProductId { public ProductId(Guid value) => Value = CatalogRules.Id(value, nameof(ProductId)); public Guid Value { get; } public static ProductId New() => new(Guid.NewGuid()); }
public readonly record struct ProductVariantId { public ProductVariantId(Guid value) => Value = CatalogRules.Id(value, nameof(ProductVariantId)); public Guid Value { get; } public static ProductVariantId New() => new(Guid.NewGuid()); }
public readonly record struct OptionGroupId { public OptionGroupId(Guid value) => Value = CatalogRules.Id(value, nameof(OptionGroupId)); public Guid Value { get; } public static OptionGroupId New() => new(Guid.NewGuid()); }
public readonly record struct ProductOptionId { public ProductOptionId(Guid value) => Value = CatalogRules.Id(value, nameof(ProductOptionId)); public Guid Value { get; } public static ProductOptionId New() => new(Guid.NewGuid()); }
public readonly record struct ProductImageReferenceId { public ProductImageReferenceId(Guid value) => Value = CatalogRules.Id(value, nameof(ProductImageReferenceId)); public Guid Value { get; } public static ProductImageReferenceId New() => new(Guid.NewGuid()); }
public readonly record struct ProductAvailabilityScheduleId { public ProductAvailabilityScheduleId(Guid value) => Value = CatalogRules.Id(value, nameof(ProductAvailabilityScheduleId)); public Guid Value { get; } public static ProductAvailabilityScheduleId New() => new(Guid.NewGuid()); }

public enum CatalogStatus : short { Draft = 1, Active = 2, Suspended = 3, Archived = 4 }
public enum ProductStatus : short { Draft = 1, Active = 2, Suspended = 3, Archived = 4 }
public enum InventoryStatus : short { InStock = 1, LowStock = 2, OutOfStock = 3, Unavailable = 4 }
public enum SelectionType : short { SingleChoice = 1, MultipleChoice = 2 }

internal static class CatalogRules
{
    internal static Guid Id(Guid value, string name) => value == Guid.Empty ? throw new DomainException($"{name} cannot be empty.") : value;
    internal static string Required(string? value, int max, string name) { string v = value?.Trim() ?? ""; if (v.Length == 0 || v.Length > max) throw new DomainException($"{name} is required and must not exceed {max} characters."); return v; }
    internal static string? Optional(string? value, int max, string name) { string? v = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); if (v?.Length > max) throw new DomainException($"{name} must not exceed {max} characters."); return v; }
    internal static string Language(string value) { string v = Required(value, 2, nameof(value)).ToLowerInvariant(); return v is "ar" or "he" or "en" ? v : throw new DomainException("Language must be ar, he, or en."); }
    internal static string Currency(string value) { string v = Required(value, 3, nameof(value)).ToUpperInvariant(); return v.All(char.IsLetter) ? v : throw new DomainException("Currency must be a three-letter code."); }
    internal static void Sort(int value) { if (value < 0) throw new DomainException("Sort order cannot be negative."); }
    internal static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
}

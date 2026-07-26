using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Media.Domain;

public readonly record struct MediaAssetId
{
    public MediaAssetId(Guid value) => Value = MediaRules.Id(value, nameof(MediaAssetId));
    public Guid Value { get; }
    public static MediaAssetId New() => new(Guid.NewGuid());
}

public readonly record struct MediaVariantId
{
    public MediaVariantId(Guid value) => Value = MediaRules.Id(value, nameof(MediaVariantId));
    public Guid Value { get; }
    public static MediaVariantId New() => new(Guid.NewGuid());
}

public enum MediaAssetStatus : short { Pending = 1, Processing = 2, Ready = 3, Failed = 4, Deleted = 5 }
public enum MediaAccessLevel : short { Public = 1, Private = 2, Internal = 3 }
public enum MediaVariantType : short { Thumbnail = 1, Small = 2, Medium = 3, Large = 4 }
public enum MediaVariantStatus : short { Ready = 1, Failed = 2 }

internal static class MediaRules
{
    internal static Guid Id(Guid value, string name) => value == Guid.Empty ? throw new DomainException($"{name} cannot be empty.") : value;
    internal static string Required(string? value, int max, string name)
    {
        string result = value?.Trim() ?? string.Empty;
        return result.Length is 0 || result.Length > max
            ? throw new DomainException($"{name} is required and must not exceed {max} characters.")
            : result;
    }
    internal static void Positive(long value, string name) { if (value <= 0) throw new DomainException($"{name} must be positive."); }
    internal static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
    internal static string OwnerType(string value)
    {
        string result = Required(value, 100, nameof(value));
        return result.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
            ? result
            : throw new DomainException("Owner type contains invalid characters.");
    }
}

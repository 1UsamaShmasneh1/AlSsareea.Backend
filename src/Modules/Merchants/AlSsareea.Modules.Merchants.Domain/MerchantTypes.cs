using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Merchants.Domain;

public readonly record struct MerchantId
{
    public MerchantId(Guid value) => Value = Validate(value, nameof(MerchantId));
    public Guid Value { get; }
    public static MerchantId New() => new(Guid.NewGuid());
    private static Guid Validate(Guid value, string name) => value == Guid.Empty ? throw new DomainException($"{name} cannot be empty.") : value;
}

public readonly record struct MerchantBranchId
{
    public MerchantBranchId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("MerchantBranchId cannot be empty.") : value;
    public Guid Value { get; }
    public static MerchantBranchId New() => new(Guid.NewGuid());
}

public readonly record struct MerchantEmployeeId
{
    public MerchantEmployeeId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("MerchantEmployeeId cannot be empty.") : value;
    public Guid Value { get; }
    public static MerchantEmployeeId New() => new(Guid.NewGuid());
}

public readonly record struct BusinessHourId
{
    public BusinessHourId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("BusinessHourId cannot be empty.") : value;
    public Guid Value { get; }
    public static BusinessHourId New() => new(Guid.NewGuid());
}

public readonly record struct BusinessHourPeriodId
{
    public BusinessHourPeriodId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("BusinessHourPeriodId cannot be empty.") : value;
    public Guid Value { get; }
    public static BusinessHourPeriodId New() => new(Guid.NewGuid());
}

public readonly record struct ScheduleOverrideId
{
    public ScheduleOverrideId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("ScheduleOverrideId cannot be empty.") : value;
    public Guid Value { get; }
    public static ScheduleOverrideId New() => new(Guid.NewGuid());
}

public readonly record struct SpecialHourPeriodId
{
    public SpecialHourPeriodId(Guid value) => Value = value == Guid.Empty ? throw new DomainException("SpecialHourPeriodId cannot be empty.") : value;
    public Guid Value { get; }
    public static SpecialHourPeriodId New() => new(Guid.NewGuid());
}

public enum MerchantStatus : short { PendingApproval = 1, Active = 2, Suspended = 3, Rejected = 4, Closed = 5 }
public enum MerchantBranchStatus : short { Draft = 1, Active = 2, TemporarilyClosed = 3, Suspended = 4, Closed = 5 }
public enum MerchantMembershipRole : short { Owner = 1, Manager = 2, BranchManager = 3, Employee = 4 }
public enum MerchantMembershipStatus : short { Invited = 1, Active = 2, Suspended = 3, Removed = 4 }

public sealed record BranchAddress
{
    private BranchAddress() { City = null!; Street = null!; }
    private BranchAddress(string city, string? area, string street, string? buildingNumber, string? postalCode)
    {
        City = MerchantRules.Required(city, 150, nameof(city));
        Area = MerchantRules.Optional(area, 150, nameof(area));
        Street = MerchantRules.Required(street, 200, nameof(street));
        BuildingNumber = MerchantRules.Optional(buildingNumber, 50, nameof(buildingNumber));
        PostalCode = MerchantRules.Optional(postalCode, 20, nameof(postalCode));
    }
    public string City { get; private init; }
    public string? Area { get; private init; }
    public string Street { get; private init; }
    public string? BuildingNumber { get; private init; }
    public string? PostalCode { get; private init; }
    public static BranchAddress Create(string city, string? area, string street, string? buildingNumber, string? postalCode) =>
        new(city, area, street, buildingNumber, postalCode);
}

public readonly record struct GeoCoordinate
{
    public GeoCoordinate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new DomainException("Latitude must be between -90 and 90.");
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new DomainException("Longitude must be between -180 and 180.");
        Latitude = latitude;
        Longitude = longitude;
    }
    public double Latitude { get; }
    public double Longitude { get; }
}

public readonly record struct OpeningPeriod
{
    public OpeningPeriod(TimeOnly opensAt, TimeOnly closesAt)
    {
        if (opensAt >= closesAt) throw new DomainException("Opening time must be before closing time; overnight periods are not supported.");
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }
    public TimeOnly OpensAt { get; }
    public TimeOnly ClosesAt { get; }
}

internal static class MerchantRules
{
    internal static string Required(string? value, int maximum, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximum) throw new DomainException($"{name} is required and must not exceed {maximum} characters.");
        return normalized;
    }

    internal static string? Optional(string? value, int maximum, string name)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximum) throw new DomainException($"{name} must not exceed {maximum} characters.");
        return normalized;
    }

    internal static Guid User(Guid value, string name = "userId") => value == Guid.Empty ? throw new DomainException($"{name} cannot be empty.") : value;
    internal static void Utc(DateTime value, string name) { if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{name} must be UTC."); }

    internal static string TimeZone(string value)
    {
        string normalized = Required(value, 100, nameof(value));
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(normalized); }
        catch (TimeZoneNotFoundException) { throw new DomainException("Time zone identifier is invalid."); }
        catch (InvalidTimeZoneException) { throw new DomainException("Time zone identifier is invalid."); }
        return normalized;
    }

    internal static OpeningPeriod[] Periods(IEnumerable<OpeningPeriod> periods)
    {
        OpeningPeriod[] ordered = periods.OrderBy(x => x.OpensAt).ToArray();
        for (int i = 1; i < ordered.Length; i++)
            if (ordered[i].OpensAt < ordered[i - 1].ClosesAt) throw new DomainException("Opening periods cannot overlap.");
        return ordered;
    }
}

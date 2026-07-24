using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

internal static class CustomerDomainRules
{
    internal static void Utc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{name} must be UTC.");
    }

    internal static string Required(string? value, int maximum, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maximum)
            throw new DomainException($"{name} is required and must not exceed {maximum} characters.");
        return normalized;
    }

    internal static string? Optional(string? value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string normalized = value.Trim();
        if (normalized.Length > maximum) throw new DomainException($"{name} must not exceed {maximum} characters.");
        return normalized;
    }

    internal static Guid Actor(Guid actor)
        => actor != Guid.Empty ? actor : throw new DomainException("Actor user id is required.");
}

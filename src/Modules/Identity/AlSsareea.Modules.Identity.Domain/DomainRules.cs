using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

internal static class DomainRules
{
    internal static void RequireUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{name} must be UTC.");
    }

    internal static string Required(string? value, int maximumLength, string name)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length is < 1 || result.Length > maximumLength) throw new DomainException($"{name} is required and must not exceed {maximumLength} characters.");
        return result;
    }

    internal static string? Optional(string? value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string result = value.Trim();
        if (result.Length > maximumLength) throw new DomainException($"{name} must not exceed {maximumLength} characters.");
        return result;
    }
}

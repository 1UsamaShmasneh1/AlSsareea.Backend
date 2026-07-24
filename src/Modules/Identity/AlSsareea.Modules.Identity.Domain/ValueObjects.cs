using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public readonly record struct Email
{
    private static readonly Regex Pattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant);
    public const int MaximumLength = 320;
    public Email(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 3 or > MaximumLength || !Pattern.IsMatch(trimmed)) throw new DomainException("Email address is invalid.");
        Value = trimmed;
        Normalized = trimmed.ToLowerInvariant();
    }
    public string Value { get; }
    public string Normalized { get; }
    public override string ToString() => Value;
}

public readonly record struct PhoneNumber
{
    private static readonly Regex Pattern = new(@"^\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant);
    public PhoneNumber(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (!Pattern.IsMatch(trimmed)) throw new DomainException("Phone number must be in E.164 format.");
        Value = trimmed;
    }
    public string Value { get; }
    public string Normalized => Value;
    public override string ToString() => Value;
}

public readonly record struct PasswordHash
{
    public const int MaximumLength = 512;
    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Length < 20 || !value.Contains('$', StringComparison.Ordinal))
            throw new DomainException("Password hash must use a non-empty versioned format.");
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => "[REDACTED]";
}

public readonly record struct RefreshTokenHash
{
    private static readonly Regex Pattern = new(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);
    public RefreshTokenHash(string value)
    {
        if (value is null || !Pattern.IsMatch(value)) throw new DomainException("Refresh token hash must be a 64-character SHA-256 hexadecimal value.");
        Value = value.ToLowerInvariant();
    }
    public string Value { get; }
    public override string ToString() => "[REDACTED]";
}

public readonly record struct DeviceIdentifier
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;
    public DeviceIdentifier(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinimumLength or > MaximumLength || trimmed.Any(char.IsWhiteSpace)) throw new DomainException("Device identifier is invalid.");
        Value = trimmed;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

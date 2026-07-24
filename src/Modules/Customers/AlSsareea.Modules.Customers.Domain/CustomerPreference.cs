using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Customers.Domain;

public sealed class CustomerPreference : Entity<CustomerPreferenceId>
{
    private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "ar", "he", "en" };

    private CustomerPreference(CustomerPreferenceId id) : base(id)
    {
        PreferredLanguage = null!;
        PreferredCurrency = null!;
    }

    private CustomerPreference(CustomerPreferenceId id, CustomerId customerId, string language, string currency, DateTime createdAtUtc)
        : base(id)
    {
        CustomerId = customerId;
        PreferredLanguage = ValidateLanguage(language);
        PreferredCurrency = ValidateCurrency(currency);
        AllowOrderStatusNotifications = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public CustomerId CustomerId { get; private set; }
    public string PreferredLanguage { get; private set; }
    public string PreferredCurrency { get; private set; }
    public bool AllowMarketingNotifications { get; private set; }
    public bool AllowOrderStatusNotifications { get; private set; }
    public bool AllowPromotionalNotifications { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    internal static CustomerPreference Create(CustomerPreferenceId id, CustomerId customerId, string language, string currency, DateTime now)
    {
        CustomerDomainRules.Utc(now, nameof(now));
        return new CustomerPreference(id, customerId, language, currency, now);
    }

    internal void Update(string language, string currency, bool marketing, bool orderStatus, bool promotional, DateTime now)
    {
        CustomerDomainRules.Utc(now, nameof(now));
        PreferredLanguage = ValidateLanguage(language);
        PreferredCurrency = ValidateCurrency(currency);
        AllowMarketingNotifications = marketing;
        AllowOrderStatusNotifications = orderStatus;
        AllowPromotionalNotifications = promotional;
        UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static string ValidateLanguage(string value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return Languages.Contains(normalized) ? normalized : throw new DomainException("Preferred language must be ar, he, or en.");
    }

    private static string ValidateCurrency(string value)
    {
        string normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized.Length == 3 && normalized.All(char.IsAsciiLetter) ? normalized : throw new DomainException("Preferred currency must be a three-letter code.");
    }
}

using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class User : AggregateRoot<UserId>
{
    private User(
        UserId id,
        string phoneNumber,
        PreferredLanguage preferredLanguage,
        DateTime createdAtUtc)
        : base(id)
    {
        PhoneNumber = phoneNumber;
        PreferredLanguage = preferredLanguage;
        Status = UserStatus.PendingVerification;
        CreatedAtUtc = createdAtUtc;
    }

    public string PhoneNumber { get; }

    public PreferredLanguage PreferredLanguage { get; }

    public UserStatus Status { get; }

    public DateTime CreatedAtUtc { get; }

    public static User Create(
        UserId id,
        string phoneNumber,
        PreferredLanguage preferredLanguage,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException("Phone number cannot be empty.");
        }

        if (!Enum.IsDefined(preferredLanguage))
        {
            throw new DomainException("Preferred language is not supported.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Creation time must be UTC.");
        }

        return new User(id, phoneNumber.Trim(), preferredLanguage, createdAtUtc);
    }
}

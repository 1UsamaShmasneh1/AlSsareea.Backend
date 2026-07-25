using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Merchants.Domain;

public sealed class Merchant : AggregateRoot<MerchantId>
{
    private Merchant(MerchantId id) : base(id) { LegalName = DisplayName = Email = PhoneNumber = null!; }

    private Merchant(MerchantId id, string legalName, string displayName, string? description, string? registrationNumber, string? taxNumber, string email, string phoneNumber, Guid ownerUserId, DateTime now)
        : base(id)
    {
        ApplyProfile(legalName, displayName, description, registrationNumber, taxNumber, email, phoneNumber);
        OwnerUserId = MerchantRules.User(ownerUserId, nameof(ownerUserId));
        Status = MerchantStatus.PendingApproval;
        CreatedAtUtc = UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public string LegalName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? TaxNumber { get; private set; }
    public string Email { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public MerchantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public string? ClosingReason { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static Merchant Create(MerchantId id, string legalName, string displayName, string? description, string? registrationNumber, string? taxNumber, string email, string phoneNumber, Guid ownerUserId, DateTime now)
    {
        MerchantRules.Utc(now, nameof(now));
        Merchant merchant = new(id, legalName, displayName, description, registrationNumber, taxNumber, email, phoneNumber, ownerUserId, now);
        merchant.RaiseDomainEvent(new MerchantCreatedDomainEvent(id, now));
        return merchant;
    }

    public void UpdateProfile(string legalName, string displayName, string? description, string? registrationNumber, string? taxNumber, string email, string phoneNumber, DateTime now)
    {
        EnsureNotClosed();
        ApplyProfile(legalName, displayName, description, registrationNumber, taxNumber, email, phoneNumber);
        Touch(now);
    }

    public void Activate(DateTime now)
    {
        if (Status is not (MerchantStatus.PendingApproval or MerchantStatus.Suspended)) throw new DomainException("Merchant cannot be activated from its current status.");
        if (string.IsNullOrWhiteSpace(LegalName) || string.IsNullOrWhiteSpace(DisplayName) || (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(PhoneNumber)))
            throw new DomainException("Merchant requires a valid name and contact method before activation.");
        Status = MerchantStatus.Active; ActivatedAtUtc ??= now; SuspendedAtUtc = null; SuspensionReason = null; Touch(now);
        RaiseDomainEvent(new MerchantActivatedDomainEvent(Id, now));
    }

    public void Suspend(string reason, DateTime now)
    {
        if (Status != MerchantStatus.Active) throw new DomainException("Only an active merchant can be suspended.");
        string normalizedReason = MerchantRules.Required(reason, 1000, nameof(reason));
        Status = MerchantStatus.Suspended; SuspendedAtUtc = now; SuspensionReason = normalizedReason; Touch(now);
        RaiseDomainEvent(new MerchantSuspendedDomainEvent(Id, now));
    }

    public void Reject(string reason, DateTime now)
    {
        if (Status != MerchantStatus.PendingApproval) throw new DomainException("Only a pending merchant can be rejected.");
        string normalizedReason = MerchantRules.Required(reason, 1000, nameof(reason));
        Status = MerchantStatus.Rejected; RejectedAtUtc = now; RejectionReason = normalizedReason; Touch(now);
        RaiseDomainEvent(new MerchantRejectedDomainEvent(Id, now));
    }

    public void Close(string reason, DateTime now)
    {
        if (Status == MerchantStatus.Closed) throw new DomainException("Merchant is already closed.");
        string normalizedReason = MerchantRules.Required(reason, 1000, nameof(reason));
        Status = MerchantStatus.Closed; ClosedAtUtc = now; ClosingReason = normalizedReason; Touch(now);
        RaiseDomainEvent(new MerchantClosedDomainEvent(Id, now));
    }

    public void ChangeOwner(Guid ownerUserId, DateTime now)
    {
        EnsureNotClosed();
        ownerUserId = MerchantRules.User(ownerUserId, nameof(ownerUserId));
        if (ownerUserId == OwnerUserId) throw new DomainException("The user is already the merchant owner.");
        Guid previous = OwnerUserId; OwnerUserId = ownerUserId; Touch(now);
        RaiseDomainEvent(new MerchantOwnerChangedDomainEvent(Id, previous, ownerUserId, now));
    }

    private void ApplyProfile(string legalName, string displayName, string? description, string? registrationNumber, string? taxNumber, string email, string phoneNumber)
    {
        LegalName = MerchantRules.Required(legalName, 200, nameof(legalName));
        DisplayName = MerchantRules.Required(displayName, 200, nameof(displayName));
        Description = MerchantRules.Optional(description, 2000, nameof(description));
        RegistrationNumber = MerchantRules.Optional(registrationNumber, 100, nameof(registrationNumber));
        TaxNumber = MerchantRules.Optional(taxNumber, 100, nameof(taxNumber));
        Email = MerchantRules.Required(email, 320, nameof(email));
        if (!Email.Contains('@', StringComparison.Ordinal) || Email.StartsWith('@') || Email.EndsWith('@')) throw new DomainException("Email is invalid.");
        PhoneNumber = MerchantRules.Required(phoneNumber, 32, nameof(phoneNumber));
    }

    private void Touch(DateTime now) { MerchantRules.Utc(now, nameof(now)); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private void EnsureNotClosed() { if (Status == MerchantStatus.Closed) throw new DomainException("Closed merchants cannot be modified."); }
}

using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class ExternalIdentity : Entity<ExternalIdentityId>
{
    private ExternalIdentity(ExternalIdentityId id, UserId userId, string provider, string providerSubject, DateTime createdUtc)
        : base(id)
    {
        UserId = userId;
        Provider = provider;
        ProviderSubject = providerSubject;
        CreatedUtc = createdUtc;
        LastUsedUtc = createdUtc;
    }

    public UserId UserId { get; private set; }
    public string Provider { get; private set; }
    public string ProviderSubject { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime LastUsedUtc { get; private set; }

    public static ExternalIdentity Create(ExternalIdentityId id, UserId userId, string provider, string providerSubject, DateTime createdUtc)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Length > 32) throw new DomainException("External identity provider is invalid.");
        if (string.IsNullOrWhiteSpace(providerSubject) || providerSubject.Length > 255) throw new DomainException("External identity subject is invalid.");
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc));
        return new ExternalIdentity(id, userId, provider.Trim().ToLowerInvariant(), providerSubject.Trim(), createdUtc);
    }

    public void RecordUse(DateTime occurredUtc)
    {
        DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc));
        if (occurredUtc > LastUsedUtc) LastUsedUtc = occurredUtc;
    }
}

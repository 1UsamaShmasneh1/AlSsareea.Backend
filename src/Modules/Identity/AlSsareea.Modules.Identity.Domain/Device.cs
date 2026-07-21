using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class Device : Entity<DeviceId>
{
    private Device(DeviceId id, UserId userId, DeviceIdentifier deviceIdentifier, DevicePlatform platform, string? deviceName, string? appVersion, string? operatingSystemVersion, DateTime createdUtc) : base(id)
    {
        UserId = userId; DeviceIdentifier = deviceIdentifier; Platform = platform; DeviceName = deviceName; AppVersion = appVersion; OperatingSystemVersion = operatingSystemVersion; LastSeenUtc = createdUtc; CreatedUtc = createdUtc; ConcurrencyStamp = Guid.NewGuid();
    }
    public UserId UserId { get; private set; }
    public DeviceIdentifier DeviceIdentifier { get; private set; }
    public DevicePlatform Platform { get; private set; }
    public string? DeviceName { get; private set; }
    public string? AppVersion { get; private set; }
    public string? OperatingSystemVersion { get; private set; }
    public bool IsTrusted { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime LastSeenUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? UpdatedUtc { get; private set; }
    public DateTime? RevokedUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public static Device Register(DeviceId id, UserId userId, DeviceIdentifier identifier, DevicePlatform platform, string? name, string? appVersion, string? operatingSystemVersion, DateTime createdUtc)
    {
        if (!Enum.IsDefined(platform)) throw new DomainException("Device platform is invalid."); DomainRules.RequireUtc(createdUtc, nameof(createdUtc));
        return new Device(id, userId, identifier, platform, DomainRules.Optional(name, 150, nameof(name)), DomainRules.Optional(appVersion, 50, nameof(appVersion)), DomainRules.Optional(operatingSystemVersion, 100, nameof(operatingSystemVersion)), createdUtc);
    }
    public void MarkTrusted(DateTime occurredUtc) { EnsureActive(); if (IsTrusted) throw new DomainException("Device is already trusted."); IsTrusted = true; Touch(occurredUtc); }
    public void Revoke(DateTime occurredUtc) { if (IsRevoked) throw new DomainException("Device is already revoked."); DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); IsRevoked = true; IsTrusted = false; RevokedUtc = occurredUtc; Touch(occurredUtc); }
    public void UpdateLastSeen(DateTime occurredUtc) { EnsureActive(); DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); if (occurredUtc < LastSeenUtc) throw new DomainException("Last seen time cannot move backwards."); LastSeenUtc = occurredUtc; Touch(occurredUtc); }
    private void EnsureActive() { if (IsRevoked) throw new DomainException("Revoked devices cannot be modified."); }
    private void Touch(DateTime occurredUtc) { UpdatedUtc = occurredUtc; ConcurrencyStamp = Guid.NewGuid(); }
}

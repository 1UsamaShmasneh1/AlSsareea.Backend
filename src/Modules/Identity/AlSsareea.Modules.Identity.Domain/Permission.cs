using System.Text.RegularExpressions;
using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

#pragma warning disable CA1711 // The ubiquitous-language aggregate name is mandated by the Identity domain.
public sealed class Permission : AggregateRoot<PermissionId>
{
    private static readonly Regex NamePattern = new(@"^[a-z0-9]+(?:\.[a-z0-9]+){2,}$", RegexOptions.CultureInvariant);
    private Permission(PermissionId id, string name, string displayName, string? description, string module, bool isSystem, DateTime createdUtc) : base(id)
    {
        Name = name; NormalizedName = name; DisplayName = displayName; Description = description; Module = module; IsSystem = isSystem; IsActive = true; CreatedUtc = createdUtc; ConcurrencyStamp = Guid.NewGuid();
    }
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public string Module { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? UpdatedUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public static Permission Create(PermissionId id, string name, string displayName, string? description, string module, bool isSystem, DateTime createdUtc)
    {
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc));
        string technicalName = DomainRules.Required(name, 150, nameof(name));
        if (technicalName.Any(char.IsUpper) || !NamePattern.IsMatch(technicalName)) throw new DomainException("Permission name must be a lowercase dotted technical identifier.");
        string normalizedModule = DomainRules.Required(module, 50, nameof(module)).ToLowerInvariant();
        return new Permission(id, technicalName, DomainRules.Required(displayName, 150, nameof(displayName)), DomainRules.Optional(description, 500, nameof(description)), normalizedModule, isSystem, createdUtc);
    }
    public void UpdatePresentation(string displayName, string? description, DateTime occurredUtc)
    {
        DisplayName = DomainRules.Required(displayName, 150, nameof(displayName)); Description = DomainRules.Optional(description, 500, nameof(description)); Touch(occurredUtc);
    }
    public void Deactivate(DateTime occurredUtc) { if (IsSystem) throw new DomainException("System permissions cannot be deactivated."); if (!IsActive) throw new DomainException("Permission is already inactive."); IsActive = false; Touch(occurredUtc); }
    public void Activate(DateTime occurredUtc) { if (IsActive) throw new DomainException("Permission is already active."); IsActive = true; Touch(occurredUtc); }
    private void Touch(DateTime occurredUtc) { DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); UpdatedUtc = occurredUtc; ConcurrencyStamp = Guid.NewGuid(); }
}
#pragma warning restore CA1711

using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Identity.Domain;

public sealed class Role : AggregateRoot<RoleId>
{
    private readonly List<RolePermission> _permissions = [];
    private Role(RoleId id, string name, string? description, bool isSystem, DateTime createdUtc) : base(id)
    {
        Name = name; NormalizedName = Normalize(name); Description = description; IsSystem = isSystem; IsActive = true; CreatedUtc = createdUtc; ConcurrencyStamp = Guid.NewGuid();
    }
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? UpdatedUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();
    public static Role Create(RoleId id, string name, string? description, bool isSystem, DateTime createdUtc)
    {
        DomainRules.RequireUtc(createdUtc, nameof(createdUtc));
        return new Role(id, DomainRules.Required(name, 100, nameof(name)), DomainRules.Optional(description, 500, nameof(description)), isSystem, createdUtc);
    }
    public void Rename(string name, DateTime occurredUtc)
    {
        if (IsSystem) throw new DomainException("System roles cannot be renamed.");
        Name = DomainRules.Required(name, 100, nameof(name)); NormalizedName = Normalize(Name); Touch(occurredUtc);
    }
    public void SetDescription(string? description, DateTime occurredUtc) { Description = DomainRules.Optional(description, 500, nameof(description)); Touch(occurredUtc); }
    public void Deactivate(DateTime occurredUtc) { if (!IsActive) throw new DomainException("Role is already inactive."); IsActive = false; Touch(occurredUtc); }
    public void Activate(DateTime occurredUtc) { if (IsActive) throw new DomainException("Role is already active."); IsActive = true; Touch(occurredUtc); }
    public void Delete(DateTime occurredUtc) { if (IsSystem) throw new DomainException("System roles cannot be deleted."); Deactivate(occurredUtc); }
    public void AssignPermission(PermissionId permissionId, DateTime assignedUtc, UserId? assignedByUserId = null)
    {
        DomainRules.RequireUtc(assignedUtc, nameof(assignedUtc));
        if (_permissions.Any(x => x.PermissionId == permissionId)) throw new DomainException("Permission is already assigned to the role.");
        _permissions.Add(new RolePermission(Id, permissionId, assignedUtc, assignedByUserId));
        RaiseDomainEvent(new PermissionAssignedToRoleDomainEvent(Id, permissionId, assignedUtc));
    }
    public void RemovePermission(PermissionId permissionId, DateTime occurredUtc)
    {
        RolePermission permission = _permissions.SingleOrDefault(x => x.PermissionId == permissionId) ?? throw new DomainException("Permission is not assigned to the role.");
        _permissions.Remove(permission); RaiseDomainEvent(new PermissionRemovedFromRoleDomainEvent(Id, permissionId, occurredUtc));
    }
    private void Touch(DateTime occurredUtc) { DomainRules.RequireUtc(occurredUtc, nameof(occurredUtc)); UpdatedUtc = occurredUtc; ConcurrencyStamp = Guid.NewGuid(); }
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

#pragma warning disable CA1711 // The join-entity name is mandated by the Identity domain.
public sealed class RolePermission
{
    internal RolePermission(RoleId roleId, PermissionId permissionId, DateTime assignedUtc, UserId? assignedByUserId) { RoleId = roleId; PermissionId = permissionId; AssignedUtc = assignedUtc; AssignedByUserId = assignedByUserId; }
    public RoleId RoleId { get; private set; }
    public PermissionId PermissionId { get; private set; }
    public DateTime AssignedUtc { get; private set; }
    public UserId? AssignedByUserId { get; private set; }
}
#pragma warning restore CA1711

using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.Modules.Identity.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default);
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
}

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(PermissionId id, CancellationToken cancellationToken = default);
    Task AddAsync(Permission permission, CancellationToken cancellationToken = default);
}

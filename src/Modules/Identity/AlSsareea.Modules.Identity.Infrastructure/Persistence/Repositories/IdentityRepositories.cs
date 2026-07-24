using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = includeDeleted ? dbContext.Users.IgnoreQueryFilters() : dbContext.Users;
        return query.Include(x => x.Roles).Include(x => x.Devices).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
    public async Task AddAsync(User user, CancellationToken cancellationToken = default) => await dbContext.Users.AddAsync(user, cancellationToken);
}

internal sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default) => dbContext.Roles.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default) => await dbContext.Roles.AddAsync(role, cancellationToken);
}

internal sealed class PermissionRepository(IdentityDbContext dbContext) : IPermissionRepository
{
    public Task<Permission?> GetByIdAsync(PermissionId id, CancellationToken cancellationToken = default) => dbContext.Permissions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(Permission permission, CancellationToken cancellationToken = default) => await dbContext.Permissions.AddAsync(permission, cancellationToken);
}

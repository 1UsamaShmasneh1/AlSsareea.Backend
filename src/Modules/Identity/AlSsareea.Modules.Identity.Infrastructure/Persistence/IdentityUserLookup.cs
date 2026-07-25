using AlSsareea.Modules.Identity.Contracts;
using AlSsareea.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence;

internal sealed class IdentityUserLookup(IdentityDbContext dbContext) : IIdentityUserLookup
{
    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        userId == Guid.Empty
            ? Task.FromResult(false)
            : dbContext.Users.AsNoTracking().AnyAsync(
                x => x.Id == new UserId(userId) && x.Status == UserStatus.Active && x.DeletedUtc == null,
                cancellationToken);
}

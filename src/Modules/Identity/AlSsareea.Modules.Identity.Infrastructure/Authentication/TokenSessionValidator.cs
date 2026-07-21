using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Identity.Infrastructure.Authentication;

internal sealed class TokenSessionValidator(IdentityDbContext db, IClock clock) : ITokenSessionValidator
{
    public Task<bool> IsValidAsync(UserId userId, LoginSessionId sessionId, Guid securityStamp, CancellationToken cancellationToken) =>
        (from user in db.Users.AsNoTracking() join session in db.LoginSessions.AsNoTracking() on user.Id equals session.UserId where user.Id == userId && session.Id == sessionId && user.Status == UserStatus.Active && user.SecurityStamp == securityStamp && session.State == SessionState.Active && session.ExpiresUtc > clock.UtcNow select user.Id).AnyAsync(cancellationToken);
}

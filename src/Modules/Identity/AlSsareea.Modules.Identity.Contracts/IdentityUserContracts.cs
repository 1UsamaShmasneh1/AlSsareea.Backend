namespace AlSsareea.Modules.Identity.Contracts;

public interface IIdentityUserLookup
{
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

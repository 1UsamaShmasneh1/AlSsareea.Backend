using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;

namespace AlSsareea.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId is not null && SessionId is not null;
    public UserId? UserId => TryGuid(JwtRegisteredClaimNames.Sub, out Guid value) ? new UserId(value) : null;
    public LoginSessionId? SessionId => TryGuid("sid", out Guid value) ? new LoginSessionId(value) : null;
    public IReadOnlySet<string> Roles => Values("role");
    public IReadOnlySet<string> Permissions => Values("permission");
    private bool TryGuid(string type, out Guid value) => Guid.TryParse(Principal?.FindFirstValue(type), out value) && value != Guid.Empty;
    private HashSet<string> Values(string type) => Principal?.FindAll(type).Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
}

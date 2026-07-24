using AlSsareea.Modules.Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AlSsareea.Api.Security;

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(AuthenticationPolicies.PermissionPrefix, StringComparison.Ordinal)) return await base.GetPolicyAsync(policyName);
        string permission = policyName[AuthenticationPolicies.PermissionPrefix.Length..];
        return new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)).Build();
    }
}

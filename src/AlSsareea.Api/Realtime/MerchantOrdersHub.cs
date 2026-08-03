using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Orders.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AlSsareea.Api.Realtime;

public static class MerchantOrderGroups
{
    public static string Merchant(Guid merchantId) => $"merchant:{merchantId:N}";
    public static string Branch(Guid branchId) => $"merchant-branch:{branchId:N}";
}

[Authorize(Policy = AuthenticationPolicies.PermissionPrefix + OrderPermissions.MerchantRead)]
public sealed class MerchantOrdersHub(IMerchantOrderOperationsScopeProvider scopes, ICurrentUser currentUser) : Hub
{
    public async Task JoinOrders(Guid merchantId, Guid? branchId)
    {
        Guid userId = currentUser.UserId?.Value ?? Guid.Empty;
        MerchantOrderOperationsScope? scope = await scopes.GetScopeAsync(merchantId, userId, Context.ConnectionAborted);
        if (scope is null) throw new HubException("merchant_scope_denied");

        Guid? resolvedBranch = branchId ?? scope.RestrictedBranchId;
        if (scope.RestrictedBranchId.HasValue && resolvedBranch != scope.RestrictedBranchId) throw new HubException("merchant_scope_denied");
        if (resolvedBranch.HasValue && !await scopes.IsBranchInMerchantAsync(merchantId, resolvedBranch.Value, Context.ConnectionAborted)) throw new HubException("merchant_scope_denied");

        string group = resolvedBranch.HasValue ? MerchantOrderGroups.Branch(resolvedBranch.Value) : MerchantOrderGroups.Merchant(merchantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
    }
}

using AlSsareea.Api.Realtime;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Orders.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace AlSsareea.ArchitectureTests;

public sealed class MerchantOrdersSignalRTests
{
    [Fact]
    public void HubRequiresMerchantReadPermission()
    {
        AuthorizeAttribute attribute = Assert.Single(typeof(MerchantOrdersHub).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(AuthenticationPolicies.PermissionPrefix + OrderPermissions.MerchantRead, attribute.Policy);
    }

    [Fact]
    public void GroupNamesAreDeterministicAndSeparatedByScope()
    {
        Guid id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("merchant:11111111111111111111111111111111", MerchantOrderGroups.Merchant(id));
        Assert.Equal("merchant-branch:11111111111111111111111111111111", MerchantOrderGroups.Branch(id));
        Assert.NotEqual(MerchantOrderGroups.Merchant(id), MerchantOrderGroups.Branch(id));
    }

    [Fact]
    public void RealtimePayloadContainsOnlyOperationalSummary()
    {
        string[] properties = typeof(MerchantOrderRealtimeEvent).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(properties, x => x.Contains("Token", StringComparison.OrdinalIgnoreCase) || x.Contains("Password", StringComparison.OrdinalIgnoreCase) || x.Contains("Address", StringComparison.OrdinalIgnoreCase) || x.Contains("Payment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nameof(MerchantOrderRealtimeEvent.OrderId), properties);
        Assert.Contains(nameof(MerchantOrderRealtimeEvent.UpdatedAtUtc), properties);
    }
}

using System.Reflection;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Contracts;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class NotificationsDependencyRulesTests
{
    [Fact]
    public void LayersRespectDependencyDirection()
    {
        string[] domain = typeof(Notification).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(domain, x => x.Contains(".Application", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal));
        string[] application = typeof(INotificationService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(application, x => x.Contains("Notifications.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(NotificationListResponse).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal));
    }
    [Fact]
    public void InfrastructureUsesOnlyOtherModuleContracts()
    {
        string own = typeof(NotificationsDbContext).Assembly.GetName().Name!; string[] references = typeof(NotificationsDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray(); Assert.All(references, x => Assert.Equal(own, x));
    }
    [Fact]
    public void ProviderSdksDoNotLeakIntoStableLayers()
    {
        Assembly[] layers = [typeof(Notification).Assembly, typeof(INotificationService).Assembly, typeof(NotificationListResponse).Assembly]; string[] forbidden = ["Firebase", "Google.Apis", "Amazon", "Twilio", "MailKit"];
        Assert.All(layers, layer => Assert.DoesNotContain(layer.GetReferencedAssemblies(), reference => forbidden.Any(value => reference.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true)));
    }
    [Fact]
    public void ApiEndpointsDoNotReceiveNotificationsDbContext()
    {
        MethodInfo[] endpoints = typeof(Program).Assembly.GetTypes().Where(x => x.Namespace == "AlSsareea.Api.Endpoints").SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).ToArray(); Assert.DoesNotContain(endpoints, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(NotificationsDbContext)));
    }
}

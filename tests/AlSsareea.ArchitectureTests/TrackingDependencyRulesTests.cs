using System.Reflection;
using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Domain;
using AlSsareea.Modules.Tracking.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class TrackingDependencyRulesTests
{
    [Fact]
    public void TrackingLayersRespectDependencyDirection()
    {
        string[] domain = typeof(DriverLocation).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(domain, x => x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("Npgsql", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal) || x.Contains("SignalR", StringComparison.Ordinal) || x.Contains("Tracking.Infrastructure", StringComparison.Ordinal));
        string[] application = typeof(ITrackingService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(application, x => x.Contains("Tracking.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(LocationUpdateRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(contracts, x => x.Contains("Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void InfrastructureDoesNotReferenceOtherModuleInfrastructure()
    {
        string own = typeof(TrackingDbContext).Assembly.GetName().Name!; string[] references = typeof(TrackingDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray(); Assert.All(references, x => Assert.Equal(own, x));
    }

    [Fact]
    public void EndpointsDoNotReceiveTrackingDbContextAndNoGenericRepositoryExists()
    {
        MethodInfo[] endpoints = typeof(Program).Assembly.GetTypes().Where(x => x.Namespace == "AlSsareea.Api.Endpoints").SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
        Assert.DoesNotContain(endpoints, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(TrackingDbContext)));
        Assert.DoesNotContain(typeof(ITrackingService).Assembly.GetTypes(), type => type.Name.StartsWith("IGenericRepository", StringComparison.Ordinal));
    }

    [Fact]
    public void TrackingHubExposesOnlyAuthorizedContextSubscriptions()
    {
        Type hub = typeof(Program).Assembly.GetType("AlSsareea.Api.Realtime.TrackingHub", true)!; string[] methods = hub.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(x => x.Name).ToArray();
        Assert.Equal(["SubscribeOperations", "SubscribeOrder", "SubscribeSelf"], methods.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(methods, x => x.Contains("JoinGroup", StringComparison.Ordinal));
    }
}

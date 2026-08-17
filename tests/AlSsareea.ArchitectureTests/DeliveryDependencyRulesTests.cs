using System.Reflection;
using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.ArchitectureTests;

public sealed class DeliveryDependencyRulesTests
{
    [Fact]
    public void DeliveryLayersRespectDependencyDirection()
    {
        string[] domain = typeof(DeliveryAggregate).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(domain, x => x.Contains(".Application", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal));
        string[] application = typeof(IDeliveryService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(application, x => x.Contains("Delivery.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(CreateDeliveryRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void DeliveryInfrastructureReferencesContractsNotOtherInfrastructure()
    {
        string own = typeof(DeliveryDbContext).Assembly.GetName().Name!;
        string[] references = typeof(DeliveryDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray();
        Assert.All(references, x => Assert.Equal(own, x));
    }

    [Fact]
    public void EndpointsDoNotReceiveDeliveryDbContextAndNoGenericRepositoryExists()
    {
        MethodInfo[] endpoints = typeof(Program).Assembly.GetTypes().Where(x => x.Namespace == "AlSsareea.Api.Endpoints").SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
        Assert.DoesNotContain(endpoints, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DeliveryDbContext)));
        Assert.DoesNotContain(typeof(IDeliveryService).Assembly.GetTypes(), type => type.Name.StartsWith("IGenericRepository", StringComparison.Ordinal));
    }
}

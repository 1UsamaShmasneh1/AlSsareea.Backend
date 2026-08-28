using System.Reflection;
using AlSsareea.Modules.Dispatching.Application;
using AlSsareea.Modules.Dispatching.Contracts;
using AlSsareea.Modules.Dispatching.Domain;
using AlSsareea.Modules.Dispatching.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class DispatchingDependencyRulesTests
{
    [Fact]
    public void LayersRespectDependencyDirection()
    {
        string[] domain = typeof(DispatchRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(domain, x => x.Contains(".Application", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal));
        string[] application = typeof(IDispatchService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(application, x => x.Contains("Dispatching.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(StartDispatchRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal));
    }
    [Fact]
    public void InfrastructureDoesNotReferenceOtherModuleInfrastructure()
    {
        string own = typeof(DispatchingDbContext).Assembly.GetName().Name!; string[] references = typeof(DispatchingDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray(); Assert.All(references, x => Assert.Equal(own, x));
    }
    [Fact]
    public void ApiEndpointsDoNotReceiveDispatchingDbContext()
    {
        MethodInfo[] endpoints = typeof(Program).Assembly.GetTypes().Where(x => x.Namespace == "AlSsareea.Api.Endpoints").SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).ToArray(); Assert.DoesNotContain(endpoints, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DispatchingDbContext)));
    }
}

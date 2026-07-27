using System.Reflection;
using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Pricing.Domain;
using AlSsareea.Modules.Pricing.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class PricingDependencyRulesTests
{
    [Fact]
    public void PricingLayersPointInwardAndDoNotCrossInfrastructureBoundaries()
    {
        Assembly domain = typeof(PricingPolicy).Assembly;
        Assembly application = typeof(IPricingService).Assembly;
        Assembly contracts = typeof(IPricingCalculator).Assembly;
        Assembly infrastructure = typeof(PricingDbContext).Assembly;

        AssertDoesNotReference(domain, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(domain, ".Application");
        AssertDoesNotReference(domain, ".Infrastructure");
        AssertDoesNotReference(application, ".Infrastructure");
        AssertDoesNotReference(contracts, ".Domain");
        AssertDoesNotReference(contracts, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Merchants.Infrastructure");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Maps.Infrastructure");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Catalog.Infrastructure");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Media.Infrastructure");
    }

    [Fact]
    public void ApiDoesNotExposePricingDbContext()
    {
        Type context = typeof(PricingDbContext);
        MethodInfo[] methods = typeof(Program).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance))
            .ToArray();
        Assert.DoesNotContain(methods, method =>
            method.ReturnType == context ||
            method.GetParameters().Any(parameter => parameter.ParameterType == context));
    }

    [Fact]
    public void PricingDoesNotIntroduceGenericRepositoryOrForbiddenFramework()
    {
        Type[] types = typeof(IPricingService).Assembly.GetTypes()
            .Concat(typeof(PricingDbContext).Assembly.GetTypes()).ToArray();
        Assert.DoesNotContain(types, type =>
            type.Name.Contains("Repository", StringComparison.Ordinal) && type.IsGenericTypeDefinition);
        string[] references = types.Select(x => x.Assembly).Distinct()
            .SelectMany(x => x.GetReferencedAssemblies()).Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, x => x.Contains("MediatR", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.Contains("AutoMapper", StringComparison.Ordinal));
    }

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenName) =>
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            (reference.Name ?? string.Empty).Contains(forbiddenName, StringComparison.Ordinal));
}

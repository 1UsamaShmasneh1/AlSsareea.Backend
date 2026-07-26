using System.Reflection;
using AlSsareea.Modules.Catalog.Application;
using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Domain;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class CatalogDependencyRulesTests
{
    [Fact]
    public void CatalogLayersPointInwardAndDoNotCrossInfrastructureBoundaries()
    {
        Assembly domain = typeof(Product).Assembly;
        Assembly application = typeof(ICatalogService).Assembly;
        Assembly contracts = typeof(ProductSnapshot).Assembly;
        Assembly infrastructure = typeof(CatalogDbContext).Assembly;

        AssertDoesNotReference(domain, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(domain, ".Application");
        AssertDoesNotReference(domain, ".Infrastructure");
        AssertDoesNotReference(application, ".Infrastructure");
        AssertDoesNotReference(contracts, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(contracts, ".Domain");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Merchants.Infrastructure");
    }

    [Fact]
    public void ApiDoesNotExposeCatalogDbContext()
    {
        Type contextType = typeof(CatalogDbContext);
        MethodInfo[] methods = typeof(Program).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance))
            .ToArray();

        Assert.DoesNotContain(methods, method =>
            method.ReturnType == contextType ||
            method.GetParameters().Any(parameter => parameter.ParameterType == contextType));
    }

    [Fact]
    public void CatalogUsesSpecificRepositoriesOnly()
    {
        Type[] types = typeof(ICatalogService).Assembly.GetTypes()
            .Concat(typeof(CatalogDbContext).Assembly.GetTypes())
            .ToArray();

        Assert.DoesNotContain(types, type =>
            type.Name.Contains("Repository", StringComparison.Ordinal) &&
            type.IsGenericTypeDefinition);
        Assert.DoesNotContain(types, type =>
            type.Name.Contains("MerchantsDbContext", StringComparison.Ordinal));
    }

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenName)
    {
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => (reference.Name ?? string.Empty).Contains(
                forbiddenName,
                StringComparison.Ordinal));
    }
}

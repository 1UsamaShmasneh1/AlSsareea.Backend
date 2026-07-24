using System.Reflection;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Maps.Domain;
using AlSsareea.Modules.Maps.Infrastructure.Providers;

namespace AlSsareea.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void BuildingBlocksDomainDoesNotReferenceInfrastructure()
    {
        AssertDoesNotReference(typeof(Entity<>).Assembly, ".Infrastructure");
    }

    [Fact]
    public void BuildingBlocksDomainDoesNotReferenceApplication()
    {
        AssertDoesNotReference(typeof(Entity<>).Assembly, ".Application");
    }

    [Fact]
    public void BuildingBlocksDomainDoesNotReferenceAspNetCore()
    {
        AssertDoesNotReference(typeof(Entity<>).Assembly, "Microsoft.AspNetCore");
    }

    [Fact]
    public void BuildingBlocksApplicationDoesNotReferenceInfrastructure()
    {
        AssertDoesNotReference(typeof(IClock).Assembly, ".Infrastructure");
    }

    [Fact]
    public void IdentityDomainDoesNotReferenceIdentityInfrastructure()
    {
        AssertDoesNotReference(typeof(User).Assembly, "AlSsareea.Modules.Identity.Infrastructure");
    }

    [Fact]
    public void IdentityDomainDoesNotReferenceIdentityApplication()
    {
        AssertDoesNotReference(typeof(User).Assembly, typeof(IIdentityModule).Assembly.GetName().Name!);
    }

    [Fact]
    public void BuildingBlocksDomainDoesNotReferenceEntityFrameworkCore()
    {
        AssertDoesNotReference(typeof(Entity<>).Assembly, "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void MapsDomainDoesNotReferenceApplicationOrInfrastructure()
    {
        Assembly assembly = typeof(ServiceArea).Assembly;

        AssertDoesNotReference(assembly, "AlSsareea.Modules.Maps.Application");
        AssertDoesNotReference(assembly, "AlSsareea.Modules.Maps.Infrastructure");
        AssertDoesNotReference(assembly, "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void MapsApplicationDoesNotReferenceInfrastructure()
    {
        AssertDoesNotReference(
            typeof(IServiceAreaRepository).Assembly,
            "AlSsareea.Modules.Maps.Infrastructure");
    }

    [Fact]
    public void MapsContractsAreProviderNeutral()
    {
        Assembly assembly = typeof(IMapsProvider).Assembly;

        AssertDoesNotReference(assembly, "Google");
        AssertDoesNotReference(assembly, "Mapbox");
        AssertDoesNotReference(assembly, "Npgsql");
        AssertDoesNotReference(assembly, "NetTopologySuite");
        AssertDoesNotReference(assembly, "AlSsareea.Modules.Maps.Infrastructure");
    }

    [Fact]
    public void MapsProviderImplementationStaysInInfrastructure()
    {
        Assert.Contains(
            ".Infrastructure",
            typeof(FakeMapsProvider).Assembly.GetName().Name!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityModuleDoesNotAccessMapsInfrastructure()
    {
        AssertDoesNotReference(
            typeof(User).Assembly,
            "AlSsareea.Modules.Maps.Infrastructure");
        AssertDoesNotReference(
            typeof(IIdentityModule).Assembly,
            "AlSsareea.Modules.Maps.Infrastructure");
    }

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenName)
    {
        string[] references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            reference.Contains(forbiddenName, StringComparison.Ordinal));
    }
}

using System.Reflection;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;

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

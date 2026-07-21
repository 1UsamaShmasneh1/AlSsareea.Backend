using System.Reflection;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Contracts;
using AlSsareea.Modules.Identity.Domain;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;

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
    public void IdentityDomainDoesNotReferenceAspNetCore()
    {
        AssertDoesNotReference(typeof(User).Assembly, "Microsoft.AspNetCore");
    }

    [Fact]
    public void IdentityApplicationAndContractsDoNotReferenceInfrastructure()
    {
        AssertDoesNotReference(typeof(IIdentityModule).Assembly, ".Infrastructure");
        AssertDoesNotReference(typeof(IIdentityIntegrationEvent).Assembly, ".Infrastructure");
    }

    [Fact]
    public void IdentityDomainHasNoPersistenceDataAnnotations()
    {
        Type[] domainTypes = typeof(User).Assembly.GetTypes();
        Assert.DoesNotContain(domainTypes.SelectMany(type => type.GetCustomAttributesData()), attribute =>
            attribute.AttributeType.Namespace?.StartsWith("System.ComponentModel.DataAnnotations", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(domainTypes.SelectMany(type => type.GetProperties()).SelectMany(property => property.GetCustomAttributesData()), attribute =>
            attribute.AttributeType.Namespace?.StartsWith("System.ComponentModel.DataAnnotations", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void RepositoriesExistOnlyForAggregateRoots()
    {
        string[] repositoryNames = typeof(IIdentityModule).Assembly.GetTypes()
            .Concat(typeof(IdentityDbContext).Assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(type => type.Name.TrimStart('I'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["PermissionRepository", "RoleRepository", "UserRepository"], repositoryNames);
    }

    [Fact]
    public void BuildingBlocksDomainDoesNotReferenceEntityFrameworkCore()
    {
        AssertDoesNotReference(typeof(Entity<>).Assembly, "Microsoft.EntityFrameworkCore");
    }

    [Theory]
    [MemberData(nameof(FrameworkNeutralAssemblies))]
    public void DomainAndApplicationDoNotReferencePersistenceFrameworks(Assembly assembly)
    {
        AssertDoesNotReference(assembly, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(assembly, "Npgsql");
    }

    [Fact]
    public void ApiDoesNotContainDbContext()
    {
        Assert.DoesNotContain(
            typeof(Program).Assembly.GetTypes(),
            type => InheritsFrom(type, "Microsoft.EntityFrameworkCore.DbContext"));
    }

    [Fact]
    public void ApiMethodsDoNotExposeIdentityDbContext()
    {
        Type contextType = typeof(IdentityDbContext);
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
    public void SolutionDoesNotContainGenericRepositoryOrGeneralDbContext()
    {
        string[] forbiddenDbContextNames =
        [
            "AppDbContext",
            "ApplicationDbContext",
            "AlSsareeaDbContext",
            "MainDbContext",
        ];
        Type[] types = SolutionAssemblies().SelectMany(assembly => assembly.GetTypes()).ToArray();

        Assert.DoesNotContain(types, type =>
            type.Name.Contains("Repository", StringComparison.Ordinal) && type.IsGenericTypeDefinition);
        Assert.DoesNotContain(types, type => forbiddenDbContextNames.Contains(type.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void IdentityInfrastructureDoesNotReferenceAnotherModuleInfrastructure()
    {
        string ownAssembly = typeof(IdentityDbContext).Assembly.GetName().Name!;
        string[] references = typeof(IdentityDbContext).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal))
            .Where(name => name.EndsWith(".Infrastructure", StringComparison.Ordinal))
            .ToArray();

        Assert.All(references, reference => Assert.Equal(ownAssembly, reference));
    }

    [Fact]
    public void MigrationsExistOnlyInModuleInfrastructure()
    {
        Assembly infrastructure = typeof(IdentityDbContext).Assembly;
        Type[] migrationTypes = SolutionAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => InheritsFrom(type, "Microsoft.EntityFrameworkCore.Migrations.Migration"))
            .ToArray();

        Assert.NotEmpty(migrationTypes);
        Assert.All(migrationTypes, type => Assert.Equal(infrastructure, type.Assembly));
    }

    public static TheoryData<Assembly> FrameworkNeutralAssemblies => new()
    {
        typeof(Entity<>).Assembly,
        typeof(IClock).Assembly,
        typeof(User).Assembly,
        typeof(IIdentityModule).Assembly,
        typeof(IIdentityIntegrationEvent).Assembly,
    };

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenName)
    {
        string[] references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            reference.Contains(forbiddenName, StringComparison.Ordinal));
    }

    private static bool InheritsFrom(Type type, string fullName)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Assembly[] SolutionAssemblies() =>
    [
        typeof(Program).Assembly,
        typeof(Entity<>).Assembly,
        typeof(IClock).Assembly,
        typeof(User).Assembly,
        typeof(IIdentityModule).Assembly,
        typeof(IdentityDbContext).Assembly,
    ];
}

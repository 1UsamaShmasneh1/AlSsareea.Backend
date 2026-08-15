using System.Reflection;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class DriversDependencyRulesTests
{
    [Fact]
    public void DriversLayersRespectDependencyDirection()
    {
        string[] domain = typeof(Driver).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(domain, x => x.Contains("Application", StringComparison.Ordinal) || x.Contains("Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal));
        string[] application = typeof(IDriverService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(application, x => x.Contains("Drivers.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(DriverProfileResponse).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray(); Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void InfrastructureDoesNotReferenceOtherModuleInfrastructure()
    {
        string own = typeof(DriversDbContext).Assembly.GetName().Name!; string[] references = typeof(DriversDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray(); Assert.All(references, x => Assert.Equal(own, x));
    }

    [Fact]
    public void OnlyDriverAggregateHasRepositoryAndEndpointsDoNotExposeDbContext()
    {
        Type[] repositories = typeof(IDriverRepository).Assembly.GetTypes().Where(x => x.IsInterface && x.Name.EndsWith("Repository", StringComparison.Ordinal)).ToArray(); Assert.Equal([typeof(IDriverRepository)], repositories); Type context = typeof(DriversDbContext); MethodInfo[] methods = typeof(Program).Assembly.GetTypes().Where(x => x.Namespace == "AlSsareea.Api.Endpoints").SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).ToArray(); Assert.DoesNotContain(methods, x => x.GetParameters().Any(p => p.ParameterType == context));
    }

    [Fact]
    public void EligibilityHasDedicatedDomainPolicyAndEndpointsAcceptNoEligibilityDecisionInputs()
    {
        Assert.Equal(typeof(Driver).Assembly, typeof(DriverEligibilityPolicy).Assembly);
        Type endpoints = typeof(Program).Assembly.GetType("AlSsareea.Api.Endpoints.DriverEndpoints", throwOnError: true)!;
        Type[] decisionTypes = [typeof(DocumentStatus), typeof(VehicleStatus), typeof(DriverStatus), typeof(DriverSuspensionStatus)];
        Assert.DoesNotContain(endpoints.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).SelectMany(x => x.GetParameters()), parameter => decisionTypes.Contains(parameter.ParameterType));
    }

    [Fact]
    public void ShiftPermissionsSeparateAdministrativeAndOwnedOperations()
    {
        string[] permissions = [DriverPermissions.ShiftsManage, DriverPermissions.ShiftsRead, DriverPermissions.ShiftsManageSelf, DriverPermissions.ShiftsReadSelf]; Assert.Equal(permissions.Length, permissions.Distinct(StringComparer.Ordinal).Count()); Assert.All(permissions, permission => Assert.StartsWith("drivers.shifts.", permission, StringComparison.Ordinal));
    }
}

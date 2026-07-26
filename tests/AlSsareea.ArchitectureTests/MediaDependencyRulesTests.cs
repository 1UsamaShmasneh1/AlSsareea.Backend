using System.Reflection;
using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Contracts;
using AlSsareea.Modules.Media.Domain;
using AlSsareea.Modules.Media.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class MediaDependencyRulesTests
{
    [Fact]
    public void MediaLayersPointInwardAndDoNotAccessOtherModuleInfrastructure()
    {
        Assembly domain = typeof(MediaAsset).Assembly;
        Assembly application = typeof(IMediaService).Assembly;
        Assembly contracts = typeof(IMediaAssetLookup).Assembly;
        Assembly infrastructure = typeof(MediaDbContext).Assembly;

        AssertDoesNotReference(domain, "Microsoft.EntityFrameworkCore");
        AssertDoesNotReference(domain, ".Application");
        AssertDoesNotReference(domain, ".Infrastructure");
        AssertDoesNotReference(application, ".Infrastructure");
        AssertDoesNotReference(contracts, ".Domain");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Catalog.Infrastructure");
        AssertDoesNotReference(infrastructure, "AlSsareea.Modules.Merchants.Infrastructure");
    }

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenName) =>
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            (reference.Name ?? string.Empty).Contains(forbiddenName, StringComparison.Ordinal));
}

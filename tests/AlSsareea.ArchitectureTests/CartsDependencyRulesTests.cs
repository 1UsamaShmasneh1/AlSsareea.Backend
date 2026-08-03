using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Carts.Domain;
using AlSsareea.Modules.Carts.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class CartsDependencyRulesTests
{
    [Fact]
    public void CartsLayersRespectDependencyDirection()
    {
        string[] domain = typeof(Cart).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "").ToArray();
        Assert.DoesNotContain(domain, x => x.Contains(".Application", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        string[] application = typeof(ICartService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "").ToArray();
        Assert.DoesNotContain(application, x => x.Contains(".Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(CartResponse).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "").ToArray();
        Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal));
    }
    [Fact]
    public void CartsInfrastructureDoesNotReferenceAnotherInfrastructure()
    {
        string own = typeof(CartsDbContext).Assembly.GetName().Name!;
        string[] references = typeof(CartsDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "").Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray();
        Assert.All(references, x => Assert.Equal(own, x));
    }
}

using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;

namespace AlSsareea.ArchitectureTests;

public sealed class OrdersDependencyRulesTests
{
    [Fact]
    public void OrdersLayersRespectDependencyDirection()
    {
        string[] domain = typeof(Order).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(domain, x => x.Contains(".Application", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal) || x.Contains("EntityFrameworkCore", StringComparison.Ordinal) || x.Contains("AspNetCore", StringComparison.Ordinal));
        string[] application = typeof(IOrderService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(application, x => x.Contains("Orders.Infrastructure", StringComparison.Ordinal));
        string[] contracts = typeof(CreateOrderRequest).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(contracts, x => x.Contains(".Domain", StringComparison.Ordinal) || x.Contains(".Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void OrdersInfrastructureDoesNotReferenceAnotherInfrastructure()
    {
        string own = typeof(OrdersDbContext).Assembly.GetName().Name!;
        string[] references = typeof(OrdersDbContext).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).Where(x => x.StartsWith("AlSsareea.Modules.", StringComparison.Ordinal) && x.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToArray();
        Assert.All(references, x => Assert.Equal(own, x));
    }

    [Fact]
    public void OrdersApplicationUsesContractsRatherThanOtherModuleDomains()
    {
        string[] references = typeof(IOrderService).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, x => x is "AlSsareea.Modules.Carts.Domain" or "AlSsareea.Modules.Customers.Domain" or "AlSsareea.Modules.Merchants.Domain");
    }

    [Fact]
    public void OrdersExposesOnlyAggregateRepository()
    {
        Type[] repositories = typeof(IOrderRepository).Assembly.GetTypes().Where(x => x.IsInterface && x.Name.EndsWith("Repository", StringComparison.Ordinal)).ToArray();
        Assert.Equal([typeof(IOrderRepository)], repositories);
        Assert.DoesNotContain(typeof(IOrderRepository).GetMethods(), x => x.ReturnType.Name.Contains("IQueryable", StringComparison.Ordinal));
    }

    [Fact]
    public void MoneyPropertiesUseLong()
    {
        var money = typeof(Order).Assembly.GetTypes().SelectMany(x => x.GetProperties()).Where(x => x.Name.EndsWith("Minor", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(money); Assert.All(money, x => Assert.Equal(typeof(long), x.PropertyType));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Customers.Infrastructure.Persistence.DesignTime;

public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__CustomersDatabase")
            ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(connection, npgsql => npgsql
                .UseNetTopologySuite()
                .MigrationsHistoryTable(CustomersPersistenceConstants.MigrationsHistoryTable, CustomersPersistenceConstants.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CustomersDbContext(options);
    }
}

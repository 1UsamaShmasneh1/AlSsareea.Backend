using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Pricing.Infrastructure.Persistence.DesignTime;

public sealed class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__PricingDatabase") ??
            "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        var builder = new DbContextOptionsBuilder<PricingDbContext>();
        builder.UseNpgsql(connection, options =>
            options.MigrationsHistoryTable(PricingPersistence.MigrationsHistoryTable, PricingPersistence.Schema));
        builder.UseSnakeCaseNamingConvention();
        return new PricingDbContext(builder.Options);
    }
}

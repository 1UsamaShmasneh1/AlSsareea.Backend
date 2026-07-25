using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Merchants.Infrastructure.Persistence.DesignTime;

public sealed class MerchantsDbContextFactory : IDesignTimeDbContextFactory<MerchantsDbContext>
{
    public MerchantsDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__MerchantsDatabase")
            ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        var options = new DbContextOptionsBuilder<MerchantsDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite()
                .MigrationsHistoryTable(MerchantsPersistence.MigrationsHistoryTable, MerchantsPersistence.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new MerchantsDbContext(options);
    }
}

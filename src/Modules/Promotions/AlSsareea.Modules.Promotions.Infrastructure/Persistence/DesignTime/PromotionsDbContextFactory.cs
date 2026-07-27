using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Promotions.Infrastructure.Persistence.DesignTime;

public sealed class PromotionsDbContextFactory : IDesignTimeDbContextFactory<PromotionsDbContext>
{
    public PromotionsDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__PromotionsDatabase")
            ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseNpgsql(connection, npgsql => npgsql
                .MigrationsHistoryTable(PromotionsPersistence.MigrationsHistoryTable, PromotionsPersistence.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PromotionsDbContext(options);
    }
}

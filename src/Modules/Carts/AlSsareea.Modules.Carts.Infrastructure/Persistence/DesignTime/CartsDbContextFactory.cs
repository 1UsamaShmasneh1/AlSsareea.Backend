using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Carts.Infrastructure.Persistence.DesignTime;

public sealed class CartsDbContextFactory : IDesignTimeDbContextFactory<CartsDbContext>
{
    public CartsDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ALSSAREEA_CARTS_DATABASE") ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        DbContextOptionsBuilder<CartsDbContext> builder = new();
        builder.UseNpgsql(connection, x => x.MigrationsHistoryTable(CartsPersistence.MigrationsHistoryTable, CartsPersistence.Schema)).UseSnakeCaseNamingConvention();
        return new CartsDbContext(builder.Options);
    }
}


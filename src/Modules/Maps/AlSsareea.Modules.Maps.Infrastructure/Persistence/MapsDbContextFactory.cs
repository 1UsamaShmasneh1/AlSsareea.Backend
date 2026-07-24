using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Maps.Infrastructure.Persistence;

internal sealed class MapsDbContextFactory : IDesignTimeDbContextFactory<MapsDbContext>
{
    public MapsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MapsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=alssareea_maps;Username=postgres",
            npgsqlOptions => npgsqlOptions.UseNetTopologySuite());

        return new MapsDbContext(optionsBuilder.Options);
    }
}

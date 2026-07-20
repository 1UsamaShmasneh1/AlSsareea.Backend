using AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.DesignTime;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__IdentityDatabase")
            ?? IdentityPersistenceConstants.DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityPersistenceServiceCollectionExtensions.ConfigureIdentityDbContext(
            options,
            connectionString);

        return new IdentityDbContext(options.Options);
    }
}

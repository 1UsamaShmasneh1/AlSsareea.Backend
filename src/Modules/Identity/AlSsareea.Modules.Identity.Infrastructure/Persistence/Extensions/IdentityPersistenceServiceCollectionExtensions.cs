using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Extensions;

internal static class IdentityPersistenceServiceCollectionExtensions
{
    internal static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(
            IdentityPersistenceConstants.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{IdentityPersistenceConstants.ConnectionStringName}' is required.");

        services.AddDbContext<IdentityDbContext>(options =>
            ConfigureIdentityDbContext(options, connectionString));
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>(
                "identity_database",
                tags: ["ready"]);

        return services;
    }

    internal static void ConfigureIdentityDbContext(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable(
                        IdentityPersistenceConstants.MigrationsHistoryTable,
                        IdentityPersistenceConstants.Schema);
                })
            .UseSnakeCaseNamingConvention();
    }
}

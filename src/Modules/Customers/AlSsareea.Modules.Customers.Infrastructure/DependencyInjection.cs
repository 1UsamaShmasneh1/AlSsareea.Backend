using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Customers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CustomersOptions>().Bind(configuration.GetSection(CustomersOptions.SectionName)).Validate(
            x => x.MaxAddressesPerCustomer is > 0 and <= 100 &&
                 x.DefaultLanguage is "ar" or "he" or "en" &&
                 x.DefaultCurrency.Length == 3 &&
                 x.MaxDeliveryInstructionsLength == 1000,
            "Customers configuration is invalid.").ValidateOnStart();
        string? connectionString = configuration.GetConnectionString("CustomersDatabase") ?? configuration.GetConnectionString("IdentityDatabase");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("ConnectionStrings:CustomersDatabase is required.");
        services.AddDbContext<CustomersDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .UseNetTopologySuite()
                .MigrationsAssembly(typeof(CustomersDbContext).Assembly.FullName)
                .MigrationsHistoryTable(CustomersPersistenceConstants.MigrationsHistoryTable, CustomersPersistenceConstants.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<CustomersDbContext>("customers-postgresql", tags: ["ready"]);
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomersService, CustomersService>();
        return services;
    }
}

using System.Text.Json;
using AlSsareea.Api.Configuration;
using AlSsareea.Api.Serialization;
using AlSsareea.BuildingBlocks.Application.Localization;
using AlSsareea.BuildingBlocks.Infrastructure;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure;
using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Infrastructure;
using Microsoft.AspNetCore.Localization;

namespace AlSsareea.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddHealthChecks();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        });

        services.Configure<ServiceInfoOptions>(configuration.GetSection(ServiceInfoOptions.SectionName));
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(SupportedCultures.Default);
            options.SupportedCultures = [.. SupportedCultures.All];
            options.SupportedUICultures = [.. SupportedCultures.All];
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });

        services.AddBuildingBlocksInfrastructure();
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure();
        services.AddMapsApplication();
        services.AddMapsInfrastructure(configuration);

        return services;
    }
}

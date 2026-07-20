using AlSsareea.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AlSsareea.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health").WithTags("System");

        RouteGroupBuilder system = app.MapGroup("/api/system").WithTags("System");
        system.MapGet("/info", (
            IHostEnvironment environment,
            IOptions<ServiceInfoOptions> options) =>
        {
            ServiceInfoOptions serviceInfo = options.Value;
            return Results.Ok(new
            {
                serviceInfo.Service,
                Environment = environment.EnvironmentName,
                serviceInfo.ApiVersion,
            });
        })
        .WithName("GetSystemInfo");

        return app;
    }
}

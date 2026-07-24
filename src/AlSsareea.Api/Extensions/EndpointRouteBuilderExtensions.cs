using AlSsareea.Api.Configuration;
using AlSsareea.Api.Endpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AlSsareea.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapAuthenticationEndpoints();
        app.MapCustomerEndpoints();
        app.MapHealthChecks("/health").WithTags("System");
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).WithTags("System");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
        }).WithTags("System");

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

using System.Globalization;
using AlSsareea.Api.Middleware;

namespace AlSsareea.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        if (app.Environment.IsProduction()) app.UseHsts();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseRequestLocalization();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.ContentLanguage = CultureInfo.CurrentUICulture.Name;
            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        return app;
    }
}

using System.Globalization;
using AlSsareea.Api.Middleware;

namespace AlSsareea.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseRequestLocalization();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentLanguage = CultureInfo.CurrentUICulture.Name;
            await next(context);
        });

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

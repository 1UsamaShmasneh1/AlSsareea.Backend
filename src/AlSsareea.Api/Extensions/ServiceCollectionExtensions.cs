using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AlSsareea.Api.Configuration;
using AlSsareea.Api.Security;
using AlSsareea.Api.Serialization;
using AlSsareea.BuildingBlocks.Application.Localization;
using AlSsareea.BuildingBlocks.Infrastructure;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using IdentityDomain = AlSsareea.Modules.Identity.Domain;

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
        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    string? sub = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value; string? sid = context.Principal?.FindFirst("sid")?.Value; string? stamp = context.Principal?.FindFirst("sst")?.Value;
                    if (!Guid.TryParse(sub, out Guid userId) || !Guid.TryParse(sid, out Guid sessionId) || !Guid.TryParse(stamp, out Guid securityStamp)) { context.Fail("Invalid security claims."); return; }
                    ITokenSessionValidator validator = context.HttpContext.RequestServices.GetRequiredService<ITokenSessionValidator>();
                    if (!await validator.IsValidAsync(new IdentityDomain.UserId(userId), new IdentityDomain.LoginSessionId(sessionId), securityStamp, context.HttpContext.RequestAborted)) context.Fail("Session is no longer valid.");
                },
            };
        });
        services.AddAuthorization();
        services.AddSingleton<AuthenticationRequestRateLimiter>();
        services.AddHttpContextAccessor(); services.AddScoped<ICurrentUser, CurrentUser>(); services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        AuthenticationRateLimitOptions rateLimits = configuration.GetSection(AuthenticationRateLimitOptions.SectionName).Get<AuthenticationRateLimitOptions>() ?? new AuthenticationRateLimitOptions();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static async (context, token) => { context.HttpContext.Response.Headers.RetryAfter = "60"; await context.HttpContext.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Too Many Requests", status = 429, code = "auth.rate_limit_exceeded" }, token); };
            AddFixedWindow(options, "auth-login", rateLimits.LoginPermitLimit, rateLimits.WindowSeconds);
            AddFixedWindow(options, "auth-refresh", rateLimits.RefreshPermitLimit, rateLimits.WindowSeconds);
            AddFixedWindow(options, "auth-otp", rateLimits.OtpPermitLimit, rateLimits.WindowSeconds);
        });

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
        services.AddIdentityInfrastructure(configuration);

        return services;
    }

    private static void AddFixedWindow(RateLimiterOptions options, string name, int limit, int windowSeconds) => options.AddPolicy(name, context =>
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"; string device = context.Request.Headers["X-Device-Identifier"].ToString(); string principal = context.User.FindFirst("sid")?.Value ?? "anonymous";
        string partition = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(ip + ":" + principal + ":" + device.Trim().ToLowerInvariant())));
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0, AutoReplenishment = true });
    });
}

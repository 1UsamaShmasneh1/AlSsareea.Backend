using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AlSsareea.Api.Configuration;
using AlSsareea.Api.Realtime;
using AlSsareea.Api.Security;
using AlSsareea.Api.Serialization;
using AlSsareea.BuildingBlocks.Application.Localization;
using AlSsareea.BuildingBlocks.Infrastructure;
using AlSsareea.Modules.Carts.Infrastructure;
using AlSsareea.Modules.Catalog.Application;
using AlSsareea.Modules.Catalog.Infrastructure;
using AlSsareea.Modules.Customers.Application;
using AlSsareea.Modules.Customers.Infrastructure;
using AlSsareea.Modules.Delivery.Infrastructure;
using AlSsareea.Modules.Dispatching.Infrastructure;
using AlSsareea.Modules.Drivers.Infrastructure;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure;
using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Infrastructure;
using AlSsareea.Modules.Media.Infrastructure;
using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Infrastructure;
using AlSsareea.Modules.Notifications.Infrastructure;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Infrastructure;
using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Infrastructure;
using AlSsareea.Modules.Promotions.Infrastructure;
using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Infrastructure;
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
        TrackingOptions tracking = configuration.GetSection(TrackingOptions.SectionName).Get<TrackingOptions>() ?? new TrackingOptions();
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
                OnMessageReceived = context =>
                {
                    string token = context.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrWhiteSpace(token) && (context.HttpContext.Request.Path.StartsWithSegments("/hubs/merchant-orders") || context.HttpContext.Request.Path.StartsWithSegments("/hubs/tracking"))) context.Token = token;
                    return Task.CompletedTask;
                },
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
        services.AddSignalR();
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
            AddFixedWindow(options, "auth-registration", rateLimits.RegistrationPermitLimit, rateLimits.WindowSeconds);
            AddFixedWindow(options, "auth-google", rateLimits.GooglePermitLimit, rateLimits.WindowSeconds);
            AddFixedWindow(options, "customers-self-write", 30, 60);
            AddFixedWindow(options, "customers-address-write", 30, 60);
            AddFixedWindow(options, "customers-admin-read", 120, 60);
            AddFixedWindow(options, "customers-admin-write", 30, 60);
            AddFixedWindow(options, "merchants-read", 120, 60);
            AddFixedWindow(options, "maps-read", 60, 60);
            AddFixedWindow(options, "merchants-write", 40, 60);
            AddFixedWindow(options, "catalog-read", 180, 60);
            AddFixedWindow(options, "catalog-write", 60, 60);
            AddFixedWindow(options, "pricing-read", 180, 60);
            AddFixedWindow(options, "pricing-write", 40, 60);
            AddFixedWindow(options, "pricing-calculate", 120, 60);
            AddFixedWindow(options, "promotions-read", 120, 60);
            AddFixedWindow(options, "promotions-write", 40, 60);
            AddFixedWindow(options, "carts-write", 60, 60);
            AddFixedWindow(options, "merchant-orders-read", 240, 60);
            AddFixedWindow(options, "merchant-orders-write", 120, 60);
            AddFixedWindow(options, "drivers-write", 60, 60);
            AddFixedWindow(options, "tracking-ingestion", Math.Max(1, tracking.IngestionPermitLimit), 60);
            AddFixedWindow(options, "dispatching-write", 120, 60);
            AddFixedWindow(options, "notifications-read", 180, 60);
            AddFixedWindow(options, "notifications-write", 60, 60);
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
        services.AddCustomersApplication();
        services.AddCustomersInfrastructure(configuration);
        services.AddMapsApplication();
        services.AddMapsInfrastructure(configuration);
        services.AddMerchantsApplication();
        services.AddMerchantsInfrastructure(configuration);
        services.AddCatalogApplication();
        services.AddCatalogInfrastructure(configuration);
        services.AddMediaInfrastructure(configuration);
        services.AddPricingApplication();
        services.AddPricingInfrastructure(configuration);
        services.AddPromotionsInfrastructure(configuration);
        services.AddCartsInfrastructure(configuration);
        services.AddOrdersInfrastructure(configuration);
        services.AddDriversInfrastructure(configuration);
        services.AddTrackingInfrastructure(configuration);
        services.AddDeliveryInfrastructure(configuration);
        services.AddDispatchingInfrastructure(configuration);
        services.AddNotificationsInfrastructure(configuration);
        services.AddScoped<ILocationRealtimePublisher, TrackingRealtimePublisher>();
        services.AddSingleton<IMerchantOrderRealtimePublisher, MerchantOrderRealtimePublisher>();

        return services;
    }

    private static void AddFixedWindow(RateLimiterOptions options, string name, int limit, int windowSeconds) => options.AddPolicy(name, context =>
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"; string device = context.Request.Headers["X-Device-Identifier"].ToString(); string principal = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? context.User.FindFirst("sid")?.Value ?? "anonymous";
        string partition = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(ip + ":" + principal + ":" + device.Trim().ToLowerInvariant())));
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0, AutoReplenishment = true });
    });
}

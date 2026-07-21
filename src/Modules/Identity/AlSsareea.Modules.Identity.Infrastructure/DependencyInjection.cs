using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Infrastructure.Authentication;
using AlSsareea.Modules.Identity.Infrastructure.Persistence.Extensions;
using AlSsareea.Modules.Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AuthenticationOptions>().Bind(configuration.GetSection(AuthenticationOptions.SectionName)).Validate(x => x.AccessTokenMinutes is >= 1 and <= 60 && x.RefreshTokenDays is >= 1 and <= 90 && x.SessionDays is >= 1 and <= 90, "Authentication lifetimes are invalid.").ValidateOnStart();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience) && x.SigningKey.Length >= 32 && x.ClockSkewSeconds is >= 0 and <= 120, "JWT configuration is invalid or signing key is too weak.").ValidateOnStart();
        services.AddOptions<PasswordHashingOptions>().Bind(configuration.GetSection(PasswordHashingOptions.SectionName)).Validate(x => x.Iterations >= 100_000 && x.SaltSize >= 16 && x.HashSize >= 32, "Password hashing configuration is unsafe.").ValidateOnStart();
        services.AddOptions<LockoutOptions>().Bind(configuration.GetSection(LockoutOptions.SectionName)).Validate(x => x.MaximumFailedAttempts is >= 3 and <= 20 && x.LockoutMinutes is >= 1 and <= 1440, "Lockout configuration is invalid.").ValidateOnStart();
        services.AddOptions<OtpOptions>().Bind(configuration.GetSection(OtpOptions.SectionName)).Validate(x => x.CodeLength is >= 6 and <= 8 && x.LifetimeMinutes is >= 1 and <= 15 && x.MaximumAttempts is >= 1 and <= 10 && x.ResendSeconds is >= 30 and <= 600 && x.Pepper.Length >= 32, "OTP configuration is invalid or pepper is too weak.").Validate<Microsoft.Extensions.Hosting.IHostEnvironment>((options, environment) => !string.Equals(environment.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase) || !options.DevelopmentProviderEnabled, "The development OTP provider cannot run in Production.").ValidateOnStart();
        services.AddOptions<AuthenticationRateLimitOptions>().Bind(configuration.GetSection(AuthenticationRateLimitOptions.SectionName)).Validate(x => x.LoginPermitLimit > 0 && x.RefreshPermitLimit > 0 && x.OtpPermitLimit > 0 && x.WindowSeconds is >= 10 and <= 3600, "Authentication rate limits are invalid.").ValidateOnStart();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IOtpGenerator, CryptographicOtpGenerator>();
        services.AddScoped<IOtpHasher, HmacOtpHasher>();
        bool developmentOtpEnabled = configuration.GetValue<bool>(OtpOptions.SectionName + ":DevelopmentProviderEnabled");
        if (developmentOtpEnabled) services.AddScoped<IOtpDeliveryProvider, DevelopmentOtpDeliveryProvider>();
        else services.AddScoped<IOtpDeliveryProvider, UnavailableOtpDeliveryProvider>();
        services.AddScoped<TokenGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ITokenSessionValidator, TokenSessionValidator>();
        return services.AddIdentityPersistence(configuration);
    }
}

using AlSsareea.Modules.Identity.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlSsareea.IntegrationTests;

public sealed class ApiFactory(string connectionString, int loginPermitLimit = 1000, int otpPermitLimit = 1000, GoogleIdentity? googleIdentity = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IdentityDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:CustomersDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:MapsDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:MerchantsDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:CatalogDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:MediaDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:PricingDatabase", connectionString);
        builder.UseSetting("Media:StorageRoot", "App_Data/media-integration-tests");
        builder.UseSetting("ConnectionStrings:PromotionsDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:CartsDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:OrdersDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:DriversDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:TrackingDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:DeliveryDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:DispatchingDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:NotificationsDatabase", connectionString);
        builder.UseSetting("Authentication:Jwt:Issuer", "AlSsareea.Tests");
        builder.UseSetting("Authentication:Jwt:Audience", "AlSsareea.TestClients");
        builder.UseSetting("Authentication:Jwt:SigningKey", "TEST-ONLY-JWT-SIGNING-KEY-NEVER-USE-IN-PRODUCTION-1234567890");
        builder.UseSetting("Authentication:Otp:Pepper", "TEST-ONLY-OTP-PEPPER-NEVER-USE-IN-PRODUCTION-1234567890123");
        builder.UseSetting("Authentication:Otp:DevelopmentProviderEnabled", "true");
        builder.UseSetting("Authentication:PasswordHashing:Iterations", "100000");
        builder.UseSetting("Authentication:RateLimit:LoginPermitLimit", loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Authentication:RateLimit:OtpPermitLimit", otpPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Authentication:RateLimit:RegistrationPermitLimit", "1000");
        builder.UseSetting("Authentication:RateLimit:GooglePermitLimit", "1000");
        builder.UseSetting("Authentication:Google:Enabled", googleIdentity is null ? "false" : "true");
        builder.UseSetting("Authentication:Google:AllowedClientIds:0", "integration-tests.googleusercontent.com");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = connectionString,
                ["ConnectionStrings:CustomersDatabase"] = connectionString,
                ["ConnectionStrings:MapsDatabase"] = connectionString,
                ["ConnectionStrings:MerchantsDatabase"] = connectionString,
                ["ConnectionStrings:CatalogDatabase"] = connectionString,
                ["ConnectionStrings:MediaDatabase"] = connectionString,
                ["ConnectionStrings:PricingDatabase"] = connectionString,
                ["Media:StorageRoot"] = "App_Data/media-integration-tests",
                ["ConnectionStrings:PromotionsDatabase"] = connectionString,
                ["ConnectionStrings:CartsDatabase"] = connectionString,
                ["ConnectionStrings:OrdersDatabase"] = connectionString,
                ["ConnectionStrings:DriversDatabase"] = connectionString,
                ["ConnectionStrings:TrackingDatabase"] = connectionString,
                ["ConnectionStrings:DeliveryDatabase"] = connectionString,
                ["ConnectionStrings:DispatchingDatabase"] = connectionString,
                ["ConnectionStrings:NotificationsDatabase"] = connectionString,
                ["Authentication:Jwt:Issuer"] = "AlSsareea.Tests",
                ["Authentication:Jwt:Audience"] = "AlSsareea.TestClients",
                ["Authentication:Jwt:SigningKey"] = "TEST-ONLY-JWT-SIGNING-KEY-NEVER-USE-IN-PRODUCTION-1234567890",
                ["Authentication:Otp:Pepper"] = "TEST-ONLY-OTP-PEPPER-NEVER-USE-IN-PRODUCTION-1234567890123",
                ["Authentication:Otp:DevelopmentProviderEnabled"] = "true",
                ["Authentication:PasswordHashing:Iterations"] = "100000",
                ["Authentication:RateLimit:LoginPermitLimit"] = loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Authentication:RateLimit:OtpPermitLimit"] = otpPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Authentication:RateLimit:RegistrationPermitLimit"] = "1000",
                ["Authentication:RateLimit:GooglePermitLimit"] = "1000",
                ["Authentication:Google:Enabled"] = googleIdentity is null ? "false" : "true",
                ["Authentication:Google:AllowedClientIds:0"] = "integration-tests.googleusercontent.com",
            }));
        if (googleIdentity is not null)
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleIdentityValidator>();
                services.AddSingleton<IGoogleIdentityValidator>(new FakeGoogleIdentityValidator(googleIdentity));
            });
    }

    private sealed class FakeGoogleIdentityValidator(GoogleIdentity identity) : IGoogleIdentityValidator
    {
        public Task<AuthenticationResult<GoogleIdentity>> ValidateAsync(string idToken, string? nonce, CancellationToken cancellationToken) =>
            Task.FromResult(idToken == "valid-google-token"
                ? AuthenticationResult<GoogleIdentity>.Success(identity)
                : AuthenticationResult<GoogleIdentity>.Failure(AuthenticationErrorCodes.ExternalTokenInvalid, 401));
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AlSsareea.IntegrationTests;

public sealed class ApiFactory(string connectionString, int loginPermitLimit = 1000, int otpPermitLimit = 1000) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IdentityDatabase", connectionString);
        builder.UseSetting("ConnectionStrings:CustomersDatabase", connectionString);
        builder.UseSetting("Authentication:Jwt:Issuer", "AlSsareea.Tests");
        builder.UseSetting("Authentication:Jwt:Audience", "AlSsareea.TestClients");
        builder.UseSetting("Authentication:Jwt:SigningKey", "TEST-ONLY-JWT-SIGNING-KEY-NEVER-USE-IN-PRODUCTION-1234567890");
        builder.UseSetting("Authentication:Otp:Pepper", "TEST-ONLY-OTP-PEPPER-NEVER-USE-IN-PRODUCTION-1234567890123");
        builder.UseSetting("Authentication:Otp:DevelopmentProviderEnabled", "true");
        builder.UseSetting("Authentication:PasswordHashing:Iterations", "100000");
        builder.UseSetting("Authentication:RateLimit:LoginPermitLimit", loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Authentication:RateLimit:OtpPermitLimit", otpPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = connectionString,
                ["ConnectionStrings:CustomersDatabase"] = connectionString,
                ["Authentication:Jwt:Issuer"] = "AlSsareea.Tests",
                ["Authentication:Jwt:Audience"] = "AlSsareea.TestClients",
                ["Authentication:Jwt:SigningKey"] = "TEST-ONLY-JWT-SIGNING-KEY-NEVER-USE-IN-PRODUCTION-1234567890",
                ["Authentication:Otp:Pepper"] = "TEST-ONLY-OTP-PEPPER-NEVER-USE-IN-PRODUCTION-1234567890123",
                ["Authentication:Otp:DevelopmentProviderEnabled"] = "true",
                ["Authentication:PasswordHashing:Iterations"] = "100000",
                ["Authentication:RateLimit:LoginPermitLimit"] = loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Authentication:RateLimit:OtpPermitLimit"] = otpPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
    }
}

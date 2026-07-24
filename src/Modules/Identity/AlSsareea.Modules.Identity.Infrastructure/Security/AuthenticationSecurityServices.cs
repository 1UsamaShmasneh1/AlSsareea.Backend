using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AlSsareea.Modules.Identity.Application;
using AlSsareea.Modules.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlSsareea.Modules.Identity.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private readonly PasswordHashingOptions _options = options.Value;

    public PasswordHashResult Hash(string password)
    {
        ValidatePassword(password);
        byte[] salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _options.Iterations, HashAlgorithmName.SHA256, _options.HashSize);
        return new PasswordHashResult($"${Algorithm}$v=1$i={_options.Iterations}$s={Convert.ToBase64String(salt)}$h={Convert.ToBase64String(hash)}");
    }

    public PasswordVerificationResult Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || !TryParse(encodedHash, out int iterations, out byte[] salt, out byte[] expected)) return PasswordVerificationResult.Failed;
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordVerificationResult.Failed;
        return iterations < _options.Iterations || salt.Length < _options.SaltSize || expected.Length < _options.HashSize
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static void ValidatePassword(string password)
    {
        if (password is null || password.Length is < 10 or > 128 || string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password must contain between 10 and 128 characters.", nameof(password));
    }

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0; salt = []; hash = [];
        try
        {
            string[] parts = value.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || parts[0] != Algorithm || parts[1] != "v=1" || !parts[2].StartsWith("i=", StringComparison.Ordinal) || !parts[3].StartsWith("s=", StringComparison.Ordinal) || !parts[4].StartsWith("h=", StringComparison.Ordinal)) return false;
            if (!int.TryParse(parts[2].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations is < 10_000 or > 10_000_000) return false;
            salt = Convert.FromBase64String(parts[3][2..]); hash = Convert.FromBase64String(parts[4][2..]);
            return salt.Length >= 8 && hash.Length >= 16;
        }
        catch (FormatException) { return false; }
    }
}

public sealed class CryptographicOtpGenerator(IOptions<OtpOptions> options) : IOtpGenerator
{
    private readonly OtpOptions _options = options.Value;

    public GeneratedOtp Generate()
    {
        int maximum = checked((int)Math.Pow(10, _options.CodeLength));
        return new GeneratedOtp(RandomNumberGenerator.GetInt32(maximum).ToString($"D{_options.CodeLength}", CultureInfo.InvariantCulture));
    }
}

public sealed class HmacOtpHasher(IOptions<OtpOptions> options) : IOtpHasher
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public string Hash(string code, OtpChallengeId challengeId) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(challengeId.Value.ToString("N") + ":" + code)));

    public bool Verify(string code, OtpChallengeId challengeId, string storedHash)
    {
        byte[] actual = Encoding.ASCII.GetBytes(Hash(code, challengeId));
        byte[] expected = Encoding.ASCII.GetBytes(storedHash);
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

internal sealed class DevelopmentOtpDeliveryProvider : IOtpDeliveryProvider
{
    public bool IsAvailable => true;

    public Task SendAsync(string normalizedDestination, OtpPurpose purpose, string code, CancellationToken cancellationToken)
    {
        _ = normalizedDestination;
        _ = purpose;
        _ = code;
        return Task.CompletedTask;
    }
}

internal sealed class UnavailableOtpDeliveryProvider : IOtpDeliveryProvider
{
    public bool IsAvailable => false;
    public Task SendAsync(string normalizedDestination, OtpPurpose purpose, string code, CancellationToken cancellationToken) => throw new InvalidOperationException("No production OTP delivery provider is configured.");
}

internal sealed record GeneratedAccessToken(string Token, int ExpiresInSeconds);
internal sealed record GeneratedRefreshToken(string RawToken, RefreshTokenHash Hash);

internal sealed class TokenGenerator(IOptions<JwtOptions> jwtOptions, IOptions<AuthenticationOptions> authenticationOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly AuthenticationOptions _authentication = authenticationOptions.Value;

    internal GeneratedAccessToken GenerateAccessToken(User user, LoginSessionId sessionId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, DateTime utcNow)
    {
        DateTime expires = utcNow.AddMinutes(_authentication.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()), new("sid", sessionId.Value.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")), new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(utcNow).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64), new("sst", user.SecurityStamp.ToString()), new("user_type", user.UserType.ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, utcNow, expires, credentials);
        return new GeneratedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), checked((int)(expires - utcNow).TotalSeconds));
    }

    internal static GeneratedRefreshToken GenerateRefreshToken()
    {
        string raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(raw, new RefreshTokenHash(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))));
    }

    internal static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static string HmacSha256(string key, string value) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value)));
}

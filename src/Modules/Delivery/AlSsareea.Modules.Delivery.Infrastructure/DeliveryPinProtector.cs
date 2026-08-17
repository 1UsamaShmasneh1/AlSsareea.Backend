using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AlSsareea.Modules.Delivery.Application;

namespace AlSsareea.Modules.Delivery.Infrastructure;

internal sealed class DeliveryPinProtector : IDeliveryPinProtector
{
    private const int Iterations = 120_000;

    public DeliveryPinSecret Generate()
    {
        string pin = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Hash(pin, salt);
        return new(pin, Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string candidate, string hash, string salt)
    {
        if (candidate.Length != 6 || !candidate.All(char.IsAsciiDigit)) return false;
        try
        {
            byte[] expected = Convert.FromBase64String(hash);
            byte[] actual = Hash(candidate, Convert.FromBase64String(salt));
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }

    private static byte[] Hash(string pin, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, 32);
}

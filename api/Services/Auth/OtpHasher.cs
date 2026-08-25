using System.Security.Cryptography;
using System.Text;

namespace Amanah.Api.Services.Auth;

public static class OtpHasher
{
    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    public static bool Verify(string code, string storedHash)
    {
        var computedHash = Hash(code);

        if (computedHash.Length != storedHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(computedHash),
            Convert.FromHexString(storedHash));
    }
}

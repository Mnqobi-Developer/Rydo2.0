using System.Globalization;
using System.Security.Cryptography;

namespace Rydo.Infrastructure.Admin;

public static class AdminPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return string.Join(
            '$',
            "pbkdf2-sha256",
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');

        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            if (iterations is < 100_000 or > 1_000_000 ||
                salt.Length is < 16 or > 64 ||
                expected.Length != HashSize)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

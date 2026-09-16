using System.Security.Cryptography;
using System.Text;

namespace XamantaSDK.Auth;

public class SecretProtector
{
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int Iterations = 100_000;
    private const string Scheme = "pbkdf2-sha256";
    private static readonly HashAlgorithmName Prf = HashAlgorithmName.SHA256;

    private readonly string _pepper;

    public SecretProtector(IConfiguration configuration)
    {
        _pepper = configuration["OAuth:SecretPepper"] ?? string.Empty;
    }

    public string NewToken(int bytes = 32)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buffer)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public string Derive(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Pbkdf2(secret, salt, Iterations);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string secret, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations))
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Pbkdf2(secret, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private byte[] Pbkdf2(string secret, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(_pepper + secret), salt, iterations, Prf, KeyBytes);
}

using System.Security.Cryptography;
using System.Text;

namespace BursaryHub.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>
/// PBKDF2 / SHA-256 password hasher.
/// Format stored: {iterations}.{base64salt}.{base64hash}
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int Iterations  = 10_000;
    private const int SaltBytes   = 16;
    private const int HashBytes    = 32; // SHA-256 = 256 bits

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Pbkdf2(password, salt);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;

            int iterations  = int.Parse(parts[0]);
            byte[] salt     = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);

            byte[] actual = Pbkdf2(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations = Iterations)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(HashBytes);
    }
}

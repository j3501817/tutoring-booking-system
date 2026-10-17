using System.Security.Cryptography;

namespace PlatoTutoring.Web.Services;

public static class PasswordHelper
{
    private const string Prefix = "PBKDF2$";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string HashPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            throw new ArgumentException("密碼不可為空白。", nameof(plainPassword));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Prefix}{Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool VerifyPassword(string plainPassword, string storedPasswordHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(storedPasswordHash))
            return false;

        if (!storedPasswordHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // 相容預設展示帳號密碼
            return string.Equals(plainPassword, storedPasswordHash, StringComparison.Ordinal);
        }

        var parts = storedPasswordHash.Substring(Prefix.Length).Split('$');
        if (parts.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedKey = Convert.FromBase64String(parts[1]);
            var actualKey = Rfc2898DeriveBytes.Pbkdf2(
                plainPassword,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
        }
        catch
        {
            return false;
        }
    }
}

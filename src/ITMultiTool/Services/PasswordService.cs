using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ITMultiTool.Services;

/// <summary>
/// The IT MultiTool administrator password. Only a salted PBKDF2 hash is stored, under
/// HKLM\SOFTWARE\ITMultiTool, which standard users can read but not change.
/// </summary>
public static class PasswordService
{
    public const int MinimumLength = 8;

    private const string SettingsKey = @"SOFTWARE\ITMultiTool";
    private const string HashValue = "PasswordHash";
    private const string SaltValue = "PasswordSalt";
    private const string IterationsValue = "PasswordIterations";

    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static bool HasPassword()
    {
        using var key = OpenSettings(writable: false);
        return key?.GetValue(HashValue) is byte[] { Length: > 0 };
    }

    public static bool Verify(string password)
    {
        using var key = OpenSettings(writable: false);
        if (key?.GetValue(HashValue) is not byte[] expected ||
            key.GetValue(SaltValue) is not byte[] salt ||
            key.GetValue(IterationsValue) is not int iterations)
            return false;

        var actual = Hash(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static void SetPassword(string password)
    {
        var problem = Validate(password);
        if (problem is not null)
            throw new ArgumentException(problem);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Hash(password, salt, Iterations);

        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.CreateSubKey(SettingsKey);
        key.SetValue(SaltValue, salt, RegistryValueKind.Binary);
        key.SetValue(IterationsValue, Iterations, RegistryValueKind.DWord);
        key.SetValue(HashValue, hash, RegistryValueKind.Binary);
    }

    /// <summary>Returns why a new password is not acceptable, or null if it is.</summary>
    public static string? Validate(string password)
    {
        if (password.Length < MinimumLength)
            return $"The password must be at least {MinimumLength} characters.";
        if (!password.Any(char.IsLetter) || !password.Any(c => !char.IsLetter(c)))
            return "The password must contain letters and at least one number or symbol.";
        return null;
    }

    private static byte[] Hash(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);

    private static RegistryKey? OpenSettings(bool writable)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return hklm.OpenSubKey(SettingsKey, writable);
    }
}

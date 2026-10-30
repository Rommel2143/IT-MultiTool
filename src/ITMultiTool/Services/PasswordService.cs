using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITMultiTool.Services;

/// <summary>
/// The IT MultiTool administrator password. Only a salted PBKDF2 hash is stored, in
/// <see cref="FileName"/> next to the app, so every PC that runs the app from the same
/// shared folder uses the same password.
/// </summary>
public static class PasswordService
{
    public const int MinimumLength = 8;
    public const string FileName = "ITMultiTool.password.json";

    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private sealed record PasswordFile(int Iterations, string Salt, string Hash);

    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, FileName);

    public static bool HasPassword() => Read() is not null;

    public static bool Verify(string password)
    {
        var stored = Read();
        if (stored is null)
            return false;

        try
        {
            var expected = Convert.FromBase64String(stored.Hash);
            var actual = Hash(password, Convert.FromBase64String(stored.Salt), stored.Iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static void SetPassword(string password)
    {
        var problem = Validate(password);
        if (problem is not null)
            throw new ArgumentException(problem);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var stored = new PasswordFile(Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(Hash(password, salt, Iterations)));
        var json = JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true });

        WriteAtomically(FilePath, json);

        // Keep the block page's local copy of the app protected by the same password.
        var installedCopy = Path.Combine(PortalService.InstallDirectory, FileName);
        if (!string.Equals(Path.GetFullPath(installedCopy), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(PortalService.InstallDirectory))
        {
            try { WriteAtomically(installedCopy, json); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
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

    /// <summary>
    /// Returns null only when no password has been set. A file that exists but can't be read
    /// (network drop, damaged file) throws instead, so it never looks like a first run that
    /// would let someone create a new password.
    /// </summary>
    private static PasswordFile? Read()
    {
        if (!File.Exists(FilePath))
            return null;

        PasswordFile? stored;
        try
        {
            stored = JsonSerializer.Deserialize<PasswordFile>(File.ReadAllText(FilePath));
        }
        catch (JsonException)
        {
            stored = null;
        }

        return stored is { Iterations: > 0, Salt.Length: > 0, Hash.Length: > 0 }
            ? stored
            : throw new InvalidDataException($"The password file is damaged: {FilePath}");
    }

    // Write to a temp file and swap it in, so a PC reading the share never sees a half-written file.
    private static void WriteAtomically(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private static byte[] Hash(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}

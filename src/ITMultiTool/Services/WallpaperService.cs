using System.ComponentModel;
using Microsoft.Win32;

namespace ITMultiTool.Services;

public enum WallpaperStyle
{
    Fill,
    Fit,
    Stretch,
    Tile,
    Center,
    Span,
}

/// <summary>
/// Sets the desktop wallpaper and locks/unlocks it via the same registry values
/// the "Prevent changing desktop background" and "Desktop Wallpaper" group policies write.
/// </summary>
public static class WallpaperService
{
    private const string DesktopKey = @"Control Panel\Desktop";
    private const string ActiveDesktopPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop";
    private const string SystemPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string NoChangingWallPaper = "NoChangingWallPaper";

    /// <summary>
    /// Wallpapers are copied here so the file stays available to every user and
    /// survives the original being moved or deleted.
    /// </summary>
    public static string StoreDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ITMultiTool", "Wallpaper");

    public static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"];

    public static string? GetCurrentWallpaper()
    {
        using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
        var path = key?.GetValue("Wallpaper") as string;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static WallpaperStyle GetCurrentStyle()
    {
        using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
        var style = key?.GetValue("WallpaperStyle") as string;
        var tile = key?.GetValue("TileWallpaper") as string;
        return (style, tile) switch
        {
            ("10", _) => WallpaperStyle.Fill,
            ("6", _) => WallpaperStyle.Fit,
            ("2", _) => WallpaperStyle.Stretch,
            ("22", _) => WallpaperStyle.Span,
            ("0", "1") => WallpaperStyle.Tile,
            ("0", _) => WallpaperStyle.Center,
            _ => WallpaperStyle.Fill,
        };
    }

    /// <summary>
    /// Applies <paramref name="imagePath"/> as the desktop wallpaper. If the wallpaper is
    /// locked, the lock is re-applied with the new image so it stays enforced.
    /// </summary>
    public static void SetWallpaper(string imagePath, WallpaperStyle style)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        var storedPath = CopyToStore(imagePath);
        var (styleValue, tileValue) = ToRegistryValues(style);

        using (var key = Registry.CurrentUser.CreateSubKey(DesktopKey))
        {
            key.SetValue("WallpaperStyle", styleValue);
            key.SetValue("TileWallpaper", tileValue);
        }

        if (IsLocked())
            WritePolicyWallpaper(storedPath, style);

        Apply(storedPath);
    }

    public static bool IsLocked()
    {
        return ReadDword(RegistryHive.LocalMachine, ActiveDesktopPolicyKey, NoChangingWallPaper) == 1
            || ReadDword(RegistryHive.CurrentUser, ActiveDesktopPolicyKey, NoChangingWallPaper) == 1;
    }

    /// <summary>
    /// Prevents users from changing the desktop background and pins the current wallpaper.
    /// </summary>
    public static void Lock()
    {
        var current = GetCurrentWallpaper();
        string? pinned = null;
        if (current is not null && File.Exists(current))
            pinned = IsInStore(current) ? current : CopyToStore(current);

        // Machine-wide: blocks the Personalization page for every user.
        using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var key = hklm.CreateSubKey(ActiveDesktopPolicyKey))
            key.SetValue(NoChangingWallPaper, 1, RegistryValueKind.DWord);

        using (var key = Registry.CurrentUser.CreateSubKey(ActiveDesktopPolicyKey))
            key.SetValue(NoChangingWallPaper, 1, RegistryValueKind.DWord);

        if (pinned is not null)
        {
            WritePolicyWallpaper(pinned, GetCurrentStyle());
            Apply(pinned);
        }

        NotifyPolicyChanged();
    }

    public static void Unlock()
    {
        using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var key = hklm.OpenSubKey(ActiveDesktopPolicyKey, writable: true))
            key?.DeleteValue(NoChangingWallPaper, throwOnMissingValue: false);

        using (var key = Registry.CurrentUser.OpenSubKey(ActiveDesktopPolicyKey, writable: true))
            key?.DeleteValue(NoChangingWallPaper, throwOnMissingValue: false);

        using (var key = Registry.CurrentUser.OpenSubKey(SystemPolicyKey, writable: true))
        {
            key?.DeleteValue("Wallpaper", throwOnMissingValue: false);
            key?.DeleteValue("WallpaperStyle", throwOnMissingValue: false);
        }

        var current = GetCurrentWallpaper();
        if (current is not null && File.Exists(current))
            Apply(current);

        NotifyPolicyChanged();
    }

    /// <summary>
    /// Explorer and the Settings app cache policy restrictions; without this broadcast they keep
    /// enforcing (or ignoring) the lock until the user signs out.
    /// </summary>
    private static void NotifyPolicyChanged() =>
        NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, NativeMethods.WM_SETTINGCHANGE, UIntPtr.Zero,
            "Policy", NativeMethods.SMTO_ABORTIFHUNG, 5000, out _);

    private static void WritePolicyWallpaper(string path, WallpaperStyle style)
    {
        // The policy uses its own numbering: 0 center, 1 tile, 2 stretch, 3 fit, 4 fill, 5 span.
        var policyStyle = style switch
        {
            WallpaperStyle.Center => "0",
            WallpaperStyle.Tile => "1",
            WallpaperStyle.Stretch => "2",
            WallpaperStyle.Fit => "3",
            WallpaperStyle.Span => "5",
            _ => "4",
        };

        using var key = Registry.CurrentUser.CreateSubKey(SystemPolicyKey);
        key.SetValue("Wallpaper", path);
        key.SetValue("WallpaperStyle", policyStyle);
    }

    private static void Apply(string path)
    {
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, path,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE))
            throw new Win32Exception();
    }

    private static string CopyToStore(string source)
    {
        Directory.CreateDirectory(StoreDirectory);

        // A fresh name each time forces Explorer to reload instead of using its cached copy.
        // Explorer's TranscodedWallpaper cache has no extension but is always a JPEG.
        var extension = Path.GetExtension(source);
        if (string.IsNullOrEmpty(extension))
            extension = ".jpg";
        var target = Path.Combine(StoreDirectory, $"wallpaper_{DateTime.Now:yyyyMMddHHmmss}{extension}");
        File.Copy(source, target, overwrite: true);

        foreach (var old in Directory.EnumerateFiles(StoreDirectory, "wallpaper_*"))
        {
            if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        return target;
    }

    private static bool IsInStore(string path) =>
        Path.GetFullPath(path).StartsWith(StoreDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static (string Style, string Tile) ToRegistryValues(WallpaperStyle style) => style switch
    {
        WallpaperStyle.Fill => ("10", "0"),
        WallpaperStyle.Fit => ("6", "0"),
        WallpaperStyle.Stretch => ("2", "0"),
        WallpaperStyle.Tile => ("0", "1"),
        WallpaperStyle.Center => ("0", "0"),
        WallpaperStyle.Span => ("22", "0"),
        _ => ("10", "0"),
    };

    private static int? ReadDword(RegistryHive hive, string subKey, string name)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(subKey);
        return key?.GetValue(name) as int?;
    }
}

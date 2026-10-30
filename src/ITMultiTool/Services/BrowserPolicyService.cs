using Microsoft.Win32;

namespace ITMultiTool.Services;

/// <summary>
/// Keeps browsers from getting around the hosts file.
/// <list type="bullet">
/// <item>Portal off: blocked sites go into each browser's URL blocklist policy, so the browser shows its own block page.</item>
/// <item>Portal on: the blocklists are cleared (they would hide the company block page) and instead QUIC and the
/// browser's built-in secure DNS are turned off, so every request follows the hosts file to the portal.</item>
/// </list>
/// Entries an administrator added outside this tool are left in place.
/// </summary>
public static class BrowserPolicyService
{
    private sealed record BrowserPolicy(string PolicyKey, string BlocklistKey, Func<string, IEnumerable<string>> Patterns, bool IsChromium);

    private static readonly BrowserPolicy[] Browsers =
    [
        // Chromium: "example.com" blocks the domain and all of its subdomains.
        new(@"SOFTWARE\Policies\Microsoft\Edge", "URLBlocklist", domain => [domain], true),
        new(@"SOFTWARE\Policies\Google\Chrome", "URLBlocklist", domain => [domain], true),
        new(@"SOFTWARE\Policies\BraveSoftware\Brave", "URLBlocklist", domain => [domain], true),
        // Firefox reads its policies at startup only.
        new(@"SOFTWARE\Policies\Mozilla\Firefox", @"WebsiteFilter\Block",
            domain => [$"*://{domain}/*", $"*://*.{domain}/*"], false),
    ];

    /// <summary>
    /// Replaces the entries this tool manages (<paramref name="previous"/>) with <paramref name="current"/>.
    /// </summary>
    public static void Sync(IReadOnlyCollection<string> previous, IReadOnlyCollection<string> current)
    {
        var portal = PortalService.IsInstalled();
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        foreach (var browser in Browsers)
        {
            var managed = previous.Concat(current).SelectMany(browser.Patterns).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var wanted = portal ? [] : current.SelectMany(browser.Patterns).ToList();
            WriteList(hklm, $@"{browser.PolicyKey}\{browser.BlocklistKey}", managed, wanted);

            if (browser.IsChromium)
            {
                SetOrClear(hklm, browser.PolicyKey, "QuicAllowed", portal, 0);
                SetOrClear(hklm, browser.PolicyKey, "DnsOverHttpsMode", portal, "off");
            }
            else
            {
                // Lets Firefox trust the portal's certificate from the Windows store.
                SetOrClear(hklm, browser.PolicyKey + @"\Certificates", "ImportEnterpriseRoots", portal, 1);
            }
        }

        NativeMethods.RefreshPolicyEx(bMachine: true, dwOptions: 0);
    }

    private static void WriteList(RegistryKey hklm, string path, HashSet<string> managed, List<string> wanted)
    {
        using var existing = hklm.OpenSubKey(path, writable: true);
        if (existing is null && wanted.Count == 0)
            return;

        using var key = existing ?? hklm.CreateSubKey(path);

        var others = key.GetValueNames()
            .Select(name => key.GetValue(name) as string)
            .OfType<string>()
            .Where(pattern => !managed.Contains(pattern))
            .ToList();

        foreach (var name in key.GetValueNames())
            key.DeleteValue(name, throwOnMissingValue: false);

        // Policy lists are numbered values "1", "2", ...
        var entries = others.Concat(wanted).ToList();
        for (var i = 0; i < entries.Count; i++)
            key.SetValue((i + 1).ToString(), entries[i], RegistryValueKind.String);

        if (entries.Count == 0)
        {
            key.Close();
            hklm.DeleteSubKey(path, throwOnMissingSubKey: false);
        }
    }

    /// <summary>
    /// Sets a single policy value while the portal is on; when it's off, removes it only if it still
    /// holds the value this tool wrote, so an administrator's own setting survives.
    /// </summary>
    private static void SetOrClear(RegistryKey hklm, string path, string name, bool set, object value)
    {
        if (set)
        {
            using var key = hklm.CreateSubKey(path);
            key.SetValue(name, value, value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
            return;
        }

        using var existing = hklm.OpenSubKey(path, writable: true);
        if (existing?.GetValue(name) is { } current && Equals(current, value))
            existing.DeleteValue(name, throwOnMissingValue: false);
    }
}

using System.Text;

namespace ITMultiTool.Services;

/// <summary>
/// Blocks websites by pointing them at 0.0.0.0 / :: in the hosts file. Only lines inside
/// this tool's marked section are ever touched; the rest of the file is preserved as-is.
/// </summary>
public static class WebsiteBlockerService
{
    private const string BeginMarker = "# >>> IT-MultiTool blocked sites >>>";
    private const string EndMarker = "# <<< IT-MultiTool blocked sites <<<";

    public static string HostsPath { get; } = Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

    public static IReadOnlyList<string> GetBlockedSites()
    {
        var (_, section, _) = ReadHosts();
        return section
            .Select(line => line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && !parts[0].StartsWith('#'))
            .Select(parts => StripWww(parts[1]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Adds sites to the block list. Returns the normalized domains that were newly added.</summary>
    public static IReadOnlyList<string> Block(IEnumerable<string> sites)
    {
        var current = GetBlockedSites().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<string>();
        foreach (var site in sites)
        {
            var domain = NormalizeDomain(site);
            if (current.Add(domain))
                added.Add(domain);
        }

        if (added.Count > 0)
            Save(current);
        return added;
    }

    public static void Unblock(IEnumerable<string> sites)
    {
        var current = GetBlockedSites().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites)
            current.Remove(StripWww(site.Trim()));
        Save(current);
    }

    public static void UnblockAll() => Save([]);

    /// <summary>
    /// Turns user input such as "https://www.Example.com:443/path" into "example.com".
    /// </summary>
    public static string NormalizeDomain(string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
            throw new ArgumentException("Enter a website address.");

        if (!text.Contains("://"))
            text = "http://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || Uri.CheckHostName(uri.Host) != UriHostNameType.Dns)
            throw new ArgumentException($"\"{input.Trim()}\" is not a valid website address.");

        var host = StripWww(uri.IdnHost.ToLowerInvariant().TrimEnd('.'));
        if (!host.Contains('.') || host == "localhost")
            throw new ArgumentException($"\"{input.Trim()}\" is not a valid website address.");
        return host;
    }

    /// <summary>Clears the Windows DNS client cache so changes take effect immediately.</summary>
    public static bool FlushDns() => NativeMethods.DnsFlushResolverCache();

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static void Save(IEnumerable<string> domains)
    {
        var (before, _, after) = ReadHosts();

        var lines = new List<string>(before);
        // Avoid gluing our section onto the last existing line.
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        var sorted = domains.Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (sorted.Count > 0)
        {
            lines.Add("");
            lines.Add(BeginMarker);
            foreach (var domain in sorted)
            {
                foreach (var host in new[] { domain, "www." + domain })
                {
                    lines.Add($"0.0.0.0 {host}");
                    lines.Add($":: {host}");
                }
            }
            lines.Add(EndMarker);
        }

        lines.AddRange(after.SkipWhile(string.IsNullOrWhiteSpace));
        WriteHosts(lines);
        FlushDns();
    }

    private static (List<string> Before, List<string> Section, List<string> After) ReadHosts()
    {
        var before = new List<string>();
        var section = new List<string>();
        var after = new List<string>();

        if (!File.Exists(HostsPath))
            return (before, section, after);

        var target = before;
        foreach (var line in File.ReadAllLines(HostsPath))
        {
            var trimmed = line.Trim();
            if (trimmed == BeginMarker)
            {
                target = section;
                continue;
            }
            if (trimmed == EndMarker)
            {
                target = after;
                continue;
            }
            target.Add(line);
        }

        return (before, section, after);
    }

    private static void WriteHosts(List<string> lines)
    {
        var info = new FileInfo(HostsPath);
        if (info.Exists && info.IsReadOnly)
            info.IsReadOnly = false;

        var content = string.Join("\r\n", lines) + "\r\n";

        // The DNS client or antivirus can briefly hold the file open.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(HostsPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200);
            }
        }
    }
}

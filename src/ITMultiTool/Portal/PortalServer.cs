using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ITMultiTool.Services;

namespace ITMultiTool.Portal;

/// <summary>
/// Answers every HTTP (80) and HTTPS (443) request on the loopback addresses that blocked sites
/// resolve to, and replies with the company block page. Runs headless via "ITMultiTool.exe --portal".
/// </summary>
public static class PortalServer
{
    private const int MaxHeaderBytes = 32 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private static readonly ConcurrentDictionary<string, X509Certificate2> Certificates = new(StringComparer.OrdinalIgnoreCase);
    private static X509Certificate2? _ca;

    public static int Run()
    {
        try
        {
            RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            PortalLog.Write("Portal stopped: " + ex);
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        _ca = PortalCertificateService.LoadRootCa();
        if (_ca is null)
            PortalLog.Write("Root certificate not found; HTTPS block pages are disabled.");

        var loops = new List<Task>();
        foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
        {
            foreach (var (port, tls) in new[] { (80, false), (443, true) })
            {
                if (tls && _ca is null)
                    continue;

                var listener = new TcpListener(address, port);
                try
                {
                    listener.Start();
                }
                catch (SocketException ex)
                {
                    PortalLog.Write($"Cannot listen on {address}:{port} ({ex.SocketErrorCode}). Is another web server using this port?");
                    continue;
                }
                loops.Add(AcceptLoopAsync(listener, tls));
            }
        }

        if (loops.Count == 0)
            throw new InvalidOperationException("No ports could be opened.");

        PortalLog.Write($"Portal started with {loops.Count} listeners.");
        await Task.WhenAll(loops);
    }

    private static async Task AcceptLoopAsync(TcpListener listener, bool tls)
    {
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleAsync(client, tls);
        }
    }

    private static async Task HandleAsync(TcpClient client, bool tls)
    {
        using var _ = client;
        using var timeout = new CancellationTokenSource(RequestTimeout);
        try
        {
            Stream stream = client.GetStream();
            string? sniHost = null;

            if (tls)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                stream = ssl;
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [SslApplicationProtocol.Http11],
                    ServerCertificateSelectionCallback = (_, name) =>
                    {
                        sniHost = name;
                        return GetCertificate(name);
                    },
                }, timeout.Token);
            }

            await using (stream)
            {
                var head = await ReadHeadAsync(stream, timeout.Token);
                var host = ParseHost(head) ?? sniHost ?? "this website";
                var isHead = head.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase);

                var body = Encoding.UTF8.GetBytes(BlockPage.Render(host));
                var header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 403 Forbidden\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Cache-Control: no-store, max-age=0\r\n" +
                    "Connection: close\r\n\r\n");

                await stream.WriteAsync(header, timeout.Token);
                if (!isHead)
                    await stream.WriteAsync(body, timeout.Token);
                await stream.FlushAsync(timeout.Token);
            }
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException or OperationCanceledException or SocketException)
        {
            // Clients routinely drop connections or reject the handshake; nothing to do.
        }
        catch (Exception ex)
        {
            PortalLog.Write("Request failed: " + ex.Message);
        }
    }

    private static X509Certificate2 GetCertificate(string? requested)
    {
        var host = requested is not null && Uri.CheckHostName(requested) == UriHostNameType.Dns
            ? requested.ToLowerInvariant()
            : "localhost";

        // Bound the cache; certificates are cheap to re-issue.
        if (Certificates.Count > 500)
            Certificates.Clear();

        return Certificates.GetOrAdd(host, h => PortalCertificateService.CreateServerCertificate(_ca!, h));
    }

    private static async Task<string> ReadHeadAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[MaxHeaderBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), token);
            if (read == 0)
                break;
            length += read;
            if (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) >= 0)
                break;
        }
        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    private static string? ParseHost(string head)
    {
        foreach (var line in head.Split("\r\n"))
        {
            if (!line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = line[5..].Trim();
            // Drop the port, but leave bracketed IPv6 literals alone.
            var colon = value.LastIndexOf(':');
            if (colon > 0 && !value.EndsWith(']'))
                value = value[..colon];
            return value.Length > 0 ? value : null;
        }
        return null;
    }
}

internal static class PortalLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ITMultiTool", "portal.log");
    private static readonly Lock Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
                    File.Delete(LogPath);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

using System.Net;
using ITMultiTool.Services;

namespace ITMultiTool.Portal;

public static class BlockPage
{
    public static string Render(string host)
    {
        var category = SiteCategories.All.FirstOrDefault(c => c.Domains.Any(d =>
            host.Equals(d, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)));

        var site = WebUtility.HtmlEncode(host);
        var reason = WebUtility.HtmlEncode(category?.Name ?? "Restricted by IT policy");
        var company = WebUtility.HtmlEncode(Branding.CompanyName);
        var computer = WebUtility.HtmlEncode(Environment.MachineName);
        var time = DateTime.Now.ToString("MMMM d, yyyy h:mm tt");

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>Website blocked | {{company}}</title>
            <style>
              :root { --blue: #0077c8; --blue-dark: #005a99; --ink: #1d2733; --muted: #5b6776; --line: #e3e8ee; --bg: #f3f6f9; }
              * { box-sizing: border-box; }
              body { margin: 0; min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center;
                     padding: 24px 16px; background: var(--bg); color: var(--ink);
                     font: 16px/1.5 "Segoe UI", system-ui, -apple-system, sans-serif; }
              .card { width: 100%; max-width: 560px; background: #fff; border-radius: 14px; overflow: hidden;
                      box-shadow: 0 1px 2px rgba(16,24,40,.06), 0 12px 32px rgba(16,24,40,.08); }
              .band { height: 6px; background: var(--blue); }
              .body { padding: 36px 40px 32px; text-align: center; }
              .logo { width: 120px; height: auto; display: block; margin: 0 auto 6px; }
              .company { font-size: 13px; font-weight: 600; letter-spacing: .08em; text-transform: uppercase; color: var(--blue-dark); }
              h1 { margin: 22px 0 8px; font-size: 26px; line-height: 1.25; }
              .lead { margin: 0 auto; max-width: 420px; color: var(--muted); }
              .site { margin: 22px 0 0; padding: 12px 16px; border: 1px solid var(--line); border-radius: 10px; background: #fafbfc;
                      font: 600 17px/1.4 Consolas, "Cascadia Mono", monospace; word-break: break-all; }
              dl { display: grid; grid-template-columns: auto 1fr; gap: 6px 16px; margin: 22px 0 0; padding: 0; text-align: left; font-size: 14px; }
              dt { color: var(--muted); }
              dd { margin: 0; font-weight: 600; }
              .help { margin: 26px 0 0; padding-top: 20px; border-top: 1px solid var(--line); font-size: 14px; color: var(--muted); }
              footer { margin-top: 18px; font-size: 12px; color: var(--muted); }
              @media (max-width: 480px) { .body { padding: 28px 20px 24px; } h1 { font-size: 22px; } }
            </style>
            </head>
            <body>
              <main class="card">
                <div class="band"></div>
                <div class="body">
                  <img class="logo" src="{{Branding.LogoDataUri}}" alt="TRC logo">
                  <div class="company">{{company}}</div>
                  <h1>This website is blocked</h1>
                  <p class="lead">Access to this site is not allowed on company computers under the {{company}} IT policy.</p>
                  <div class="site">{{site}}</div>
                  <dl>
                    <dt>Category</dt><dd>{{reason}}</dd>
                    <dt>Computer</dt><dd>{{computer}}</dd>
                    <dt>Date and time</dt><dd>{{time}}</dd>
                  </dl>
                  <p class="help">If you need this website for your work, please contact the IT Department and include the details above.</p>
                </div>
              </main>
              <footer>&copy; {{DateTime.Now.Year}} {{company}} &middot; IT Department</footer>
            </body>
            </html>
            """;
    }
}

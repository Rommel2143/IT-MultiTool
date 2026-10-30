using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ITMultiTool.Services;

public static class Branding
{
    public const string CompanyName = "Philippines TRC Inc.";
    public static readonly Color Blue = Color.FromArgb(0, 119, 200);

    private static readonly Lazy<string> SmallLogoBase64 = new(() => Convert.ToBase64String(ScaledPng(240)));

    /// <summary>Returns a new copy of the full-size logo; the caller owns it.</summary>
    public static Image LoadLogo()
    {
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("trc-logo.png")
            ?? throw new InvalidOperationException("Logo resource is missing.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    /// <summary>A small PNG of the logo for embedding in the block page as a data URI.</summary>
    public static string LogoDataUri => "data:image/png;base64," + SmallLogoBase64.Value;

    private static byte[] ScaledPng(int width)
    {
        using var logo = LoadLogo();
        var height = (int)Math.Round(logo.Height * (width / (double)logo.Width));
        using var scaled = new Bitmap(width, height);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(logo, 0, 0, width, height);
        }

        using var output = new MemoryStream();
        scaled.Save(output, ImageFormat.Png);
        return output.ToArray();
    }
}

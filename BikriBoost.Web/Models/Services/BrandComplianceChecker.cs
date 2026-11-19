using BikriBoost.Web.Models.Entities;
using SkiaSharp;
using System.Text.RegularExpressions;
namespace BikriBoost.Web.Models.Services;

public sealed record ImageCheck(int Width, int Height, string Colors, bool Passed, string Reasons);
public sealed class BrandComplianceChecker
{
    public static string[] Palette(string palette)
    {
        var colors = palette.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Rules.Require(colors.Length is > 0 and <= 10 && colors.All(c => Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")), "Use 1–10 comma-separated colors such as #195B43,#D6F19A.");
        return colors;
    }
    public static double Distance(SKColor a, SKColor b) => Math.Sqrt(Math.Pow(a.Red - b.Red, 2) + Math.Pow(a.Green - b.Green, 2) + Math.Pow(a.Blue - b.Blue, 2));
    public ImageCheck Inspect(byte[] data, BrandKit kit)
    {
        using var stream = new SKMemoryStream(data);
        using var codec = SKCodec.Create(stream);
        Rules.Require(codec != null, "Upload a valid PNG, JPEG or WebP image.");
        var info = codec!.Info;
        Rules.Require(info.Width > 0 && info.Height > 0 && (long)info.Width * info.Height <= 40_000_000, "Images must be at most 40 megapixels.");
        using var bitmap = SKBitmap.Decode(data);
        Rules.Require(bitmap != null, "This image could not be decoded.");
        var colors = Palette(kit.Palette).Select(SKColor.Parse).ToArray();
        var histogram = new Dictionary<uint, int>();
        int count = 0, matched = 0;
        for (int y = 0; y < info.Height; y += Math.Max(1, info.Height / 100))
        for (int x = 0; x < info.Width; x += Math.Max(1, info.Width / 100))
        {
            var c = bitmap!.GetPixel(x, y);
            if (c.Alpha < 128)
                continue;
            count++;
            if (colors.Any(p => Distance(c, p) <= (double)kit.ColorTolerance))
                matched++;
            var key = (uint)((c.Red / 16 * 16) << 16 | (c.Green / 16 * 16) << 8 | c.Blue / 16 * 16);
            histogram[key] = histogram.GetValueOrDefault(key) + 1;
        }
        var dominant = string.Join(',', histogram.OrderByDescending(k => k.Value).Take(5).Select(k => $"#{k.Key:X6}"));
        var reasons = new List<string>();
        if (info.Width < kit.MinWidth || info.Height < kit.MinHeight)
            reasons.Add($"Minimum resolution is {kit.MinWidth} × {kit.MinHeight}; received {info.Width} × {info.Height}.");
        if (count == 0 || matched / (decimal)count < 0.60m)
            reasons.Add("At least 60% of sampled opaque pixels must match the palette within the RGB distance tolerance.");
        return new(info.Width, info.Height, dominant, reasons.Count == 0, reasons.Count == 0 ? "Resolution and brand color checks passed." : string.Join(" ", reasons));
    }
}

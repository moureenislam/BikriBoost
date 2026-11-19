using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using SkiaSharp;
namespace BikriBoost.Tests.Unit;

public class Feature20Tests
{
    public static byte[] Image(SKColor color, int width = 100, int height = 100)
    {
        using var b = new SKBitmap(width, height);
        b.Erase(color);
        using var i = SKImage.FromBitmap(b);
        using var data = i.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    [Fact]
    public void ExactMinimumResolutionAndPalettePass()
    {
        var r = new BrandComplianceChecker().Inspect(Image(SKColors.Red), new BrandKit { Palette = "#FF0000", MinWidth = 100, MinHeight = 100, ColorTolerance = 0 });
        Assert.True(r.Passed);
    }
    [Fact]
    public void WrongColorAndSmallImageFail()
    {
        var r = new BrandComplianceChecker().Inspect(Image(SKColors.Blue), new BrandKit { Palette = "#FF0000", MinWidth = 101, MinHeight = 101 });
        Assert.False(r.Passed);
        Assert.Contains("resolution", r.Reasons);
        Assert.Contains("60%", r.Reasons);
    }
    [Fact] public void InvalidFileIsRejected() => Assert.Throws<BusinessRuleException>(() => new BrandComplianceChecker().Inspect([1, 2, 3], new BrandKit()));
    [Theory][InlineData("red")][InlineData("#123")][InlineData("")] public void PaletteRejectsInvalidColors(string s) => Assert.Throws<BusinessRuleException>(() => BrandComplianceChecker.Palette(s));
}

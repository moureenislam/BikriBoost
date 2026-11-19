using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Models.Services;

public sealed class AssetService(AppDbContext db, MarketingUnitOfWork work, BrandComplianceChecker checker, IWebHostEnvironment env)
{
    public string Root => Path.Combine(env.ContentRootPath, "App_Data", "assets");
    public string PathFor(Asset asset) => Path.Combine(Root, Path.GetFileName(asset.FilePath));
    public async Task<int> Upload(IFormFile file, string name, AssetType type, int? previousId, string userId)
    {
        Rules.Require(file.Length is > 0 and <= 8 * 1024 * 1024, "Upload a file between 1 byte and 8 MB.");
        name = Rules.Text(name, 200, "Asset name");
        Rules.Require(type is AssetType.Image or AssetType.Logo or AssetType.Font, "Choose Image, Logo or Font.");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string mime;
        ImageCheck? check = null;
        if (type == AssetType.Font)
        {
            Rules.Require((extension == ".ttf" && bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0) || (extension == ".otf" && bytes.Length >= 4 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "OTTO"), "Upload a valid .ttf or .otf font.");
            mime = extension == ".ttf" ? "font/ttf" : "font/otf";
        }
        else
        {
            Rules.Require(extension is ".png" or ".jpg" or ".jpeg" or ".webp", "Images must be PNG, JPEG or WebP.");
            mime = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            var kit = await db.BrandKits.SingleOrDefaultAsync(k => k.IsActive) ?? throw new BusinessRuleException("Save a brand kit before uploading images.");
            check = checker.Inspect(bytes, kit);
        }
        Directory.CreateDirectory(Root);
        var filename = Guid.NewGuid().ToString("N") + extension;
        await File.WriteAllBytesAsync(Path.Combine(Root, filename), bytes);
        try
        {
            return await work.Run(async () =>
            {
                var previous = previousId.HasValue ? await db.Assets.SingleOrDefaultAsync(a => a.Id == previousId) : null;
                Rules.Require(!previousId.HasValue || previous != null, "Previous asset not found.");
                Rules.Require(previous == null || previous.Type == type, "Asset versions must use the same asset type.");
                Rules.Require(previous == null || !await db.Assets.AnyAsync(a => a.PreviousVersionId == previous.Id), "This asset already has a newer version. Select its latest version.");
                var asset = new Asset { Name = name, Type = type, FilePath = filename, ContentType = mime, SizeBytes = bytes.Length, Width = check?.Width, Height = check?.Height, VersionNumber = (previous?.VersionNumber ?? 0) + 1, PreviousVersionId = previous?.Id, CheckResult = check == null ? BrandCheckResult.NotChecked : check.Passed ? BrandCheckResult.Pass : BrandCheckResult.Fail, CheckReasons = check?.Reasons ?? "Font stored; color and resolution checks apply only to images.", DominantColors = check?.Colors, UploadedByUserId = userId };
                db.Assets.Add(asset);
                await db.SaveChangesAsync();
                return asset.Id;
            });
        }
        catch { File.Delete(Path.Combine(Root, filename)); throw; }
    }
    public Task<int> SaveKit(string name, string palette, string? primaryFont, string? secondaryFont, int minWidth, int minHeight, decimal tolerance, int? logoId) => work.Run(async () =>
    {
        name = Rules.Text(name, 100, "Brand name");
        BrandComplianceChecker.Palette(palette);
        Rules.Require(minWidth is >= 1 and <= 8000 && minHeight is >= 1 and <= 8000, "Minimum dimensions must be between 1 and 8,000 pixels.");
        Rules.Require(tolerance is >= 0 and <= 442, "Color tolerance must be 0–442.");
        Rules.Require((primaryFont?.Length ?? 0) <= 100 && (secondaryFont?.Length ?? 0) <= 100, "Font names must be at most 100 characters.");
        Rules.Require(!logoId.HasValue || await db.Assets.AnyAsync(a => a.Id == logoId && a.Type == AssetType.Logo), "Choose a logo from this workspace.");
        var kit = await db.BrandKits.SingleOrDefaultAsync(k => k.IsActive) ?? new BrandKit();
        if (kit.Id == 0)
            db.BrandKits.Add(kit);
        kit.Name = name;
        kit.Palette = palette.ToUpperInvariant();
        kit.PrimaryFont = primaryFont?.Trim();
        kit.SecondaryFont = secondaryFont?.Trim();
        kit.MinWidth = minWidth;
        kit.MinHeight = minHeight;
        kit.ColorTolerance = tolerance;
        kit.LogoAssetId = logoId;
        return kit.Id;
    });
    public Task<int> Recheck(int id) => work.Run(async () =>
    {
        var a = await db.Assets.SingleOrDefaultAsync(a => a.Id == id) ?? throw new BusinessRuleException("Asset not found.");
        Rules.Require(a.Type is AssetType.Image or AssetType.Logo, "Only images have brand checks.");
        var kit = await db.BrandKits.SingleOrDefaultAsync(k => k.IsActive) ?? throw new BusinessRuleException("Create a brand kit first.");
        var result = checker.Inspect(await File.ReadAllBytesAsync(PathFor(a)), kit);
        a.CheckResult = result.Passed ? BrandCheckResult.Pass : BrandCheckResult.Fail;
        a.CheckReasons = result.Reasons;
        a.DominantColors = result.Colors;
        return id;
    });
}

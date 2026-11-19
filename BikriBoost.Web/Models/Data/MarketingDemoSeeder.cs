using BikriBoost.Web.Jobs;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
namespace BikriBoost.Web.Models.Data;

public static class MarketingDemoSeeder
{
    public static async Task Seed(IServiceProvider services, IWebHostEnvironment env)
    {
        using var scope = services.CreateScope();
        var root = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var merchant = await root.Merchants.SingleAsync(m => m.Slug == "demo-shop");
        await using var db = new AppDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>(), new JobMerchant(merchant.Id));
        if (await db.Posts.AnyAsync() || await db.Assets.AnyAsync())
            return;
        var owner = await db.Users.FirstAsync(u => u.Email == "owner@demo.bikriboost.test");
        var now = DateTime.UtcNow;
        var rootDir = Path.Combine(env.ContentRootPath, "App_Data", "assets");
        Directory.CreateDirectory(rootDir);
        using (var bitmap = new SKBitmap(1080, 1080))
        {
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColor.Parse("#195B43"));
                using var paint = new SKPaint { Color = SKColor.Parse("#D6F19A"), IsAntialias = true };
                canvas.DrawCircle(880, 170, 260, paint);
                using var large = new SKFont(SKTypeface.FromFamilyName("Arial"), 94);
                using var small = new SKFont(SKTypeface.FromFamilyName("Arial"), 28);
                canvas.DrawText("A fresh start.", 70, 550, large, paint);
                canvas.DrawText("Made for your everyday.", 76, 625, small, paint);
                canvas.DrawText("BIKRIBOOST  /  NEW SEASON", 76, 950, small, paint);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Combine(rootDir, "demo-creative.png"), data.ToArray());
        }
        var asset = new Asset { Name = "New season / campaign creative", Type = AssetType.Image, FilePath = "demo-creative.png", ContentType = "image/png", Width = 1080, Height = 1080, SizeBytes = new FileInfo(Path.Combine(rootDir, "demo-creative.png")).Length, CheckResult = BrandCheckResult.Pass, CheckReasons = "Demo creative meets the default resolution and palette checks.", DominantColors = "#195B43,#D6F19A", UploadedByUserId = owner.Id };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        db.BrandKits.Add(new BrandKit { Name = "BikriBoost Studio", Palette = "#195B43,#D6F19A,#FFFFFF", PrimaryFont = "Arial", SecondaryFont = "Segoe UI", MinWidth = 1080, MinHeight = 1080, ColorTolerance = 30 });
        var product = new Product { Name = "Everyday cotton tote", BasePrice = 650, Category = "Accessories" };
        var variant = new ProductVariant { Product = product, Sku = "TOTE-SAGE", Color = "Sage", Size = "Standard", Stock = 40, Reserved = 2, Price = 650, CostPrice = 240, SafetyThreshold = 3 };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        for (int i = 0; i < 5; i++)
        {
            var status = i switch
            {
                0 => PostStatus.PendingApproval,
                1 => PostStatus.Approved,
                2 => PostStatus.Scheduled,
                _ => PostStatus.Draft
            };
            var post = new Post { Caption = new[] { "A fresh start for your everyday. Meet our new cotton tote.", "Small details. Big difference. Our new season collection is here.", "Your weekend companion, in a new shade of sage.", "Behind the scenes: made with care, carried everywhere.", "Tell us: what is always in your everyday bag?" }[i], Status = status, CreatedByUserId = owner.Id, Media = [new() { AssetId = asset.Id, Url = $"/Assets/File/{asset.Id}", Width = 1080, Height = 1080 }], Targets = [new() { Platform = SocialPlatform.Facebook }], TaggedVariants = [new() { ProductVariantId = variant.Id }] };
            if (i < 3)
                post.ApprovalRequests.Add(new()
                {
                    Status = i == 0 ? ApprovalStatus.Pending : ApprovalStatus.Approved,
                    SubmittedByUserId = owner.Id,
                    SubmittedAt = now.AddDays(-1),
                    ReviewedByUserId = i == 0 ? null : owner.Id,
                    ReviewedAt = i == 0 ? null : now
                });
            db.Posts.Add(post);
            await db.SaveChangesAsync();
            if (i == 2)
                db.ScheduledPosts.Add(new()
                {
                    PostId = post.Id,
                    ScheduledFor = now.AddDays(2),
                    Status = ScheduledPostStatus.Scheduled
                });
        }
        await db.SaveChangesAsync();
    }
}

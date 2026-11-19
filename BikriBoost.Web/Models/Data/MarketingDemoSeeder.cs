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
        if (await db.Campaigns.AnyAsync() || await db.Posts.AnyAsync() || await db.Assets.AnyAsync())
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
        var campaign = new Campaign { Name = "New season essentials", Budget = 15000, Notes = "Seeded demonstration campaign" };
        var second = new Campaign { Name = "Weekend favorites", Budget = 8000 };
        db.Campaigns.AddRange(campaign, second);
        var product = new Product { Name = "Everyday cotton tote", BasePrice = 650, Category = "Accessories" };
        var variant = new ProductVariant { Product = product, Sku = "TOTE-SAGE", Color = "Sage", Size = "Standard", Stock = 40, Reserved = 2, Price = 650, CostPrice = 240, SafetyThreshold = 3 };
        db.ProductVariants.Add(variant);
        var customer = new Customer { Name = "Demo Customer", Phone = "01711000001", DistrictId = 1 };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var link = new TrackedLink { CampaignId = campaign.Id, Code = "demonewseason000000000001", DestinationUrl = "/Campaigns/Landing", UtmSource = "facebook", UtmMedium = "social", UtmCampaign = campaign.Name, UtmContent = "season-tote-01", Clicks = 184 };
        db.TrackedLinks.Add(link);
        db.Coupons.Add(new Coupon { CampaignId = campaign.Id, Code = "FRESH10", Type = CouponType.Percent, Value = 10, MinOrderAmount = 500, TotalLimit = 100, PerPhoneLimit = 1, ExpiresAt = now.AddMonths(1) });
        await db.SaveChangesAsync();
        for (int i = 0; i < 6; i++)
        {
            var order = new Order { OrderNumber = $"DEMO-S-{i + 1:000}", CustomerId = customer.Id, Status = OrderStatus.Settled, ShippingName = customer.Name, ShippingPhone = customer.Phone!, ShippingAddress = "Demo address, Dhaka", DistrictId = 1, Subtotal = 1300, DeliveryCharge = 80, Total = 1380, SettledAt = now.AddDays(-i), DeliveredAt = now.AddDays(-i - 1), CreatedAt = now.AddDays(-i - 3), Items = [new() { ProductVariantId = variant.Id, Quantity = 2, UnitPrice = 650, UnitCost = 240, LineTotal = 1300 }], Shipment = new() { CourierId = 1, ConsignmentId = $"DEMO-AWB-{i + 1}", Status = ShipmentStatus.Delivered, DeliveryCharge = 80, CodAmount = 1380 } };
            db.Attributions.Add(new Attribution { Order = order, CampaignId = campaign.Id, TrackedLinkId = link.Id, Source = AttributionSource.Utm });
        }
        for (int i = 0; i < 4; i++)
            db.Orders.Add(new Order { OrderNumber = $"DEMO-P-{i + 1:000}", CustomerId = customer.Id, Status = OrderStatus.Pending, ShippingName = customer.Name, ShippingPhone = $"0171100000{i + 2}", ShippingAddress = "Demo address, Dhaka", DistrictId = 1, Subtotal = 650, DeliveryCharge = 80, Total = 730, Items = [new() { ProductVariantId = variant.Id, Quantity = 1, UnitPrice = 650, UnitCost = 240, LineTotal = 650 }] });
        var returned = new Order { OrderNumber = "DEMO-R-001", CustomerId = customer.Id, Status = OrderStatus.Returned, ShippingName = customer.Name, ShippingPhone = customer.Phone!, ShippingAddress = "Demo address", DistrictId = 1, Subtotal = 650, Total = 730, LogisticsLoss = 120, ReturnCases = [new() { Reason = "Demo returned parcel", Status = ReturnStatus.Closed, ReceivedAt = now.AddDays(-2), LogisticsLoss = 120 }] };
        db.Attributions.Add(new Attribution { Order = returned, CampaignId = campaign.Id, Source = AttributionSource.Utm });
        for (int i = 0; i < 5; i++)
        {
            var status = i switch
            {
                0 => PostStatus.PendingApproval,
                1 => PostStatus.Approved,
                2 => PostStatus.Scheduled,
                _ => PostStatus.Draft
            };
            var post = new Post { Caption = new[] { "A fresh start for your everyday. Meet our new cotton tote.", "Small details. Big difference. Our new season collection is here.", "Your weekend companion, in a new shade of sage.", "Behind the scenes: made with care, carried everywhere.", "Tell us: what is always in your everyday bag?" }[i], CampaignId = campaign.Id, Status = status, CreatedByUserId = owner.Id, Media = [new() { AssetId = asset.Id, Url = $"/Assets/File/{asset.Id}", Width = 1080, Height = 1080 }], Targets = [new() { Platform = SocialPlatform.Facebook }], TaggedVariants = [new() { ProductVariantId = variant.Id }] };
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
        db.AdSpends.AddRange(new AdSpend { CampaignId = campaign.Id, Date = DateOnly.FromDateTime(BusinessTime.Local(now)), Amount = 1800, Platform = SocialPlatform.Facebook, CreativeKey = "season-tote-01" }, new AdSpend { CampaignId = second.Id, Date = DateOnly.FromDateTime(BusinessTime.Local(now)), Amount = 650, Platform = SocialPlatform.Instagram, CreativeKey = "weekend-01" });
        var current = FatigueDetector.Monday(DateOnly.FromDateTime(BusinessTime.Local(now)));
        for (int i = 0; i < 4; i++)
        {
            db.CreativeWeeklyMetrics.Add(new()
            {
                CampaignId = campaign.Id,
                CreativeKey = "season-tote-01",
                WeekStart = current.AddDays(-28 + i * 7),
                Spend = i < 2 ? 1000 : 1800,
                Impressions = 10000,
                Clicks = 200,
                Orders = i < 2 ? 20 : 10
            });
            db.CreativeWeeklyMetrics.Add(new()
            {
                CampaignId = second.Id,
                CreativeKey = "weekend-01",
                WeekStart = current.AddDays(-28 + i * 7),
                Spend = 700,
                Impressions = 5000,
                Clicks = 100,
                Orders = 15
            });
        }
        await db.SaveChangesAsync();
        await new FatigueDetector(db, new MarketingUnitOfWork(db), TimeProvider.System).Evaluate();
    }
}

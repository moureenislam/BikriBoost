using System.Net;
using System.Text;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Tests.Integration;

public class MarketingWorkflowTests
{
    [Theory]
    [InlineData("/Approvals")]
    [InlineData("/Calendar")]
    [InlineData("/Assets")]
    [InlineData("/Campaigns")]
    [InlineData("/Dashboard")]
    [InlineData("/Alerts")]
    public async Task AllSixPagesRenderAndRequireAuthentication(string path)
    {
        using var f = new MarketingFactory();
        await f.Seed();
        using var owner = f.Client();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(path)).StatusCode);
        using var anon = f.Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task MarketerCannotApproveOrChangeBrandSettings()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        using var c = f.Client("Marketer");
        var form = await MarketingFactory.Form(c, "/Approvals", new() { { "id", "1" }, { "approve", "true" } });
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/Approvals/Decide", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/Assets/SaveKit", await MarketingFactory.Form(c, "/Assets", new()))).StatusCode);
    }
    [Fact]
    public async Task MutationsRequireAntiForgery()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        using var c = f.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Calendar/RunDue", new FormUrlEncodedContent([]))).StatusCode);
    }
    [Fact]
    public async Task TenantCannotReadAnotherCampaign()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        using var c = f.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Campaigns/Details/2")).StatusCode);
        await using var db = f.Db();
        Assert.Single(await db.Campaigns.ToListAsync());
    }
    [Fact]
    public async Task DraftReviewScheduleAndStockRecheckWorkTogether()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using var db = f.Db();
        var clock = new MutableTime(DateTimeOffset.UtcNow);
        var work = new MarketingUnitOfWork(db);
        var approval = new ApprovalService(db, work, clock);
        var id = await approval.SaveDraft(null, "Test draft", null, [1], "test-user");
        await approval.Submit(id, "test-user");
        await approval.Review(id, true, "Ready", "test-user");
        var scheduler = new SchedulingService(db, work, new SimulatedPublisher(), clock);
        await scheduler.Schedule(id, clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        var variant = await db.ProductVariants.SingleAsync();
        variant.Stock = 0;
        await db.SaveChangesAsync();
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal(0, await scheduler.PublishDue());
        Assert.Equal(ScheduledPostStatus.Held, (await db.ScheduledPosts.SingleAsync()).Status);
        variant.Stock = 10;
        await db.SaveChangesAsync();
        await scheduler.Schedule(id, clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal(1, await scheduler.PublishDue());
        Assert.Equal(0, await scheduler.PublishDue());
        Assert.StartsWith("simulated-", (await db.PostTargets.SingleAsync()).ExternalPostId);
    }
    [Fact]
    public async Task UnresolvedAnnotationBlocksApproval()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using var db = f.Db();
        var p = new Post { Caption = "Review", Status = PostStatus.PendingApproval, Media = [new() { Url = "test" }], ApprovalRequests = [new() { Status = ApprovalStatus.Pending }] };
        db.Posts.Add(p);
        await db.SaveChangesAsync();
        var s = new ApprovalService(db, new MarketingUnitOfWork(db), TimeProvider.System);
        await s.Annotate(p.Id, p.Media.Single().Id, 0, 100, "Fix this", "test-user");
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Review(p.Id, true, null, "test-user"));
        await s.Resolve((await db.Annotations.SingleAsync()).Id);
        await s.Review(p.Id, true, null, "test-user");
        Assert.Equal(PostStatus.Approved, p.Status);
    }
    [Fact]
    public async Task CouponAttributionIsAtomicAndCannotRedeemTwice()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using var db = f.Db();
        var s = new CampaignService(db, new MarketingUnitOfWork(db), TimeProvider.System);
        var id = await s.Coupon(1, "TEN", CouponType.Percent, 10, 500, null, 1, 1);
        await s.Attribute(1, "TEN", null);
        var order = await db.Orders.SingleAsync();
        Assert.Equal(50, order.Discount);
        Assert.Equal(500, order.Total);
        Assert.Equal(1, (await db.Coupons.SingleAsync()).TimesUsed);
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Attribute(1, "TEN", null));
        Assert.Single(await db.Attributions.ToListAsync());
    }
    [Fact]
    public async Task CrossCampaignCouponFailureRollsBackMoneyAndUsage()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using (var db = f.Db())
        {
            db.Campaigns.Add(new()
            {
                Id = 3,
                Name = "Other"
            });
            db.TrackedLinks.Add(new()
            {
                Id = 1,
                CampaignId = 3,
                Code = "abc"
            });
            await db.SaveChangesAsync();
            var s = new CampaignService(db, new MarketingUnitOfWork(db), TimeProvider.System);
            await s.Coupon(1, "TEN", CouponType.Percent, 10, null, null, 1, 1);
            await Assert.ThrowsAsync<BusinessRuleException>(() => s.Attribute(1, "TEN", 1));
        }
        await using var fresh = f.Db();
        Assert.Equal(0, (await fresh.Coupons.SingleAsync()).TimesUsed);
        Assert.Equal(550, (await fresh.Orders.SingleAsync()).Total);
        Assert.Empty(await fresh.Attributions.ToListAsync());
    }
    [Fact]
    public async Task InvalidCsvDoesNotPartiallyImportAndValidCsvIsIdempotent()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using var db = f.Db();
        var s = new RoiCalculator(db, new MarketingUnitOfWork(db));
        var day = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        string header = "Date,CampaignId,Platform,CreativeKey,Amount\n";
        var valid = $"{day},1,Facebook,creative,100\n";
        using var invalid = new MemoryStream(Encoding.UTF8.GetBytes(header + valid + $"{day},1,Facebook,creative,-1\n"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Import(invalid));
        Assert.Empty(await db.AdSpends.ToListAsync());
        using var good = new MemoryStream(Encoding.UTF8.GetBytes(header + valid));
        Assert.Equal(1, await s.Import(good));
        using var repeated = new MemoryStream(Encoding.UTF8.GetBytes(header + valid));
        Assert.Equal(0, await s.Import(repeated));
    }
    [Fact]
    public async Task FatigueJobDoesNotDuplicateAlerts()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        await using var db = f.Db();
        var s = new FatigueDetector(db, new MarketingUnitOfWork(db), TimeProvider.System);
        var monday = FatigueDetector.Monday(DateOnly.FromDateTime(BusinessTime.Local(DateTime.UtcNow)));
        for (int i = 0; i < 4; i++)
            await s.SaveMetric("creative", 1, monday.AddDays(-28 + i * 7), i < 2 ? 1000 : 2000, 1000, 100, i < 2 ? 10 : 5);
        Assert.Equal(1, await s.Evaluate());
        Assert.Equal(0, await s.Evaluate());
        Assert.Single(await db.FatigueAlerts.ToListAsync());
    }
    [Fact]
    public async Task PublicLinkTracksAndRedirectsOnlyToLocalLanding()
    {
        using var f = new MarketingFactory();
        await f.Seed();
        string code;
        await using (var db = f.Db())
        {
            var s = new CampaignService(db, new MarketingUnitOfWork(db), TimeProvider.System);
            await s.CreateLink(1, "facebook", "social", "creative");
            code = (await db.TrackedLinks.SingleAsync()).Code;
        }
        using var c = f.Client(null);
        var r = await c.GetAsync("/r/" + code);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.StartsWith("/Campaigns/Landing?", r.Headers.Location!.OriginalString);
        Assert.Contains("utm_source=facebook", r.Headers.Location.OriginalString);
        await using var fresh = f.Db();
        Assert.Equal(1, (await fresh.TrackedLinks.SingleAsync()).Clicks);
    }
    private sealed class MutableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now;
    }
}

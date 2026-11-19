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
    public async Task ContributionPagesRenderAndRequireAuthentication(string path)
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
    private sealed class MutableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now;
    }
}

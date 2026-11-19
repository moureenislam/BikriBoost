using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Models.Services;

public interface IPublisher
{
    Task<string> Publish(Post post, SocialPlatform platform);
}
// This adapter has no network side effects. IDs explicitly identify simulated publishing.
public sealed class SimulatedPublisher : IPublisher
{
    public Task<string> Publish(Post post, SocialPlatform platform) => Task.FromResult($"simulated-{post.Id}-{platform}");
}
public sealed class SchedulingService(AppDbContext db, MarketingUnitOfWork work, IPublisher publisher, TimeProvider time)
{
    private async Task<Post> Post(int id) => await db.Posts.Include(p => p.ApprovalRequests).Include(p => p.Targets)
        .Include(p => p.TaggedVariants).ThenInclude(t => t.ProductVariant).ThenInclude(v => v!.Product)
        .SingleOrDefaultAsync(p => p.Id == id) ?? throw new BusinessRuleException("Post not found.");
    public static string? BlockReason(Post post)
    {
        if (post.ApprovalRequests.OrderByDescending(a => a.Id).FirstOrDefault()?.Status != ApprovalStatus.Approved)
            return "Post requires owner approval.";
        if (post.TaggedVariants.Any(t => t.ProductVariant == null || t.ProductVariant.IsLockedOut || t.ProductVariant.Available <= t.ProductVariant.SafetyThreshold || t.ProductVariant.Product?.IsActive == false))
            return "A promoted SKU has reached its safety stock threshold or is inactive.";
        return null;
    }
    public Task<int> Schedule(int postId, DateTime utc) => work.Run(async () =>
    {
        var post = await Post(postId);
        Rules.Require(utc > time.GetUtcNow().UtcDateTime, "Choose a future date and time (Bangladesh time).");
        Rules.Require(post.Status is PostStatus.Approved or PostStatus.Scheduled or PostStatus.Held, "Only approved posts can be scheduled.");
        var reason = BlockReason(post);
        Rules.Require(reason == null, reason ?? "");
        var schedule = await db.ScheduledPosts.SingleOrDefaultAsync(s => s.PostId == postId);
        if (schedule == null)
        {
            schedule = new ScheduledPost { PostId = postId };
            db.ScheduledPosts.Add(schedule);
        }
        Rules.Require(schedule.Status != ScheduledPostStatus.Published, "Published posts cannot be rescheduled.");
        schedule.ScheduledFor = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        schedule.Status = ScheduledPostStatus.Scheduled;
        schedule.HoldReason = null;
        post.Status = PostStatus.Scheduled;
        return postId;
    });
    public Task<int> Cancel(int id) => work.Run(async () =>
    {
        var s = await db.ScheduledPosts.Include(s => s.Post).SingleOrDefaultAsync(s => s.Id == id) ?? throw new BusinessRuleException("Schedule not found.");
        Rules.Require(s.Status is ScheduledPostStatus.Scheduled or ScheduledPostStatus.Held, "This schedule cannot be cancelled.");
        s.Status = ScheduledPostStatus.Cancelled;
        s.Post!.Status = PostStatus.Approved;
        s.HoldReason = null;
        return id;
    });
    public async Task<int> PublishDue()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var ids = await db.ScheduledPosts.Where(s => s.Status == ScheduledPostStatus.Scheduled && s.ScheduledFor <= now).Select(s => s.Id).ToListAsync();
        var count = 0;
        foreach (var id in ids)
            count += await work.Run(async () =>
            {
                var s = await db.ScheduledPosts.SingleAsync(s => s.Id == id);
                if (s.Status != ScheduledPostStatus.Scheduled)
                    return 0;
                var p = await Post(s.PostId);
                var reason = BlockReason(p);
                if (reason != null)
                {
                    s.Status = ScheduledPostStatus.Held;
                    s.HoldReason = reason;
                    p.Status = PostStatus.Held;
                    return 0;
                }
                foreach (var target in p.Targets)
                {
                    target.ExternalPostId = await publisher.Publish(p, target.Platform);
                    target.PublishedAt = now;
                }
                s.Status = ScheduledPostStatus.Published;
                s.PublishedAt = now;
                s.HoldReason = "Simulated locally; nothing was sent to social media.";
                p.Status = PostStatus.Published;
                return 1;
            });
        return count;
    }
}

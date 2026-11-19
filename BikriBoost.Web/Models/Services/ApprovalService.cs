using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Models.Services;

public sealed class ApprovalService(AppDbContext db, MarketingUnitOfWork work, TimeProvider time)
{
    public async Task<Post> Get(int id) => await db.Posts.Include(p => p.Media).Include(p => p.TaggedVariants).ThenInclude(t => t.ProductVariant)
        .Include(p => p.Annotations).Include(p => p.ApprovalRequests).SingleOrDefaultAsync(p => p.Id == id)
        ?? throw new BusinessRuleException("Post not found in your workspace.");
    public Task<int> SaveDraft(int? id, string caption, int? assetId, int[] variantIds, string userId) => work.Run(async () =>
    {
        caption = Rules.Text(caption, 2200, "Caption");
        var post = id.HasValue ? await Get(id.Value) : new Post { CreatedByUserId = userId };
        Rules.Require(post.Status == PostStatus.Draft, "Only drafts can be edited. Request changes before revising an approved post.");
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync();
        Rules.Require(variants.Count == variantIds.Distinct().Count(), "A selected SKU does not belong to this workspace.");
        Asset? asset = assetId.HasValue ? await db.Assets.SingleOrDefaultAsync(a => a.Id == assetId) : null;
        Rules.Require(!assetId.HasValue || asset is { Type: AssetType.Image or AssetType.Logo, CheckResult: BrandCheckResult.Pass }, "Select an image that passed brand checks.");
        post.Caption = caption;
        if (!id.HasValue)
            db.Posts.Add(post);
        // Keep media IDs stable so historical annotation coordinates retain their meaning.
        if (asset != null && !post.Media.Any(m => m.AssetId == asset.Id))
            post.Media.Add(new PostMedia { AssetId = asset.Id, Url = $"/Assets/File/{asset.Id}", Width = asset.Width ?? 0, Height = asset.Height ?? 0 });
        db.PostTaggedVariants.RemoveRange(post.TaggedVariants);
        post.TaggedVariants = variants.Select(v => new PostTaggedVariant { ProductVariantId = v.Id }).ToList();
        if (!id.HasValue)
            post.Targets.Add(new PostTarget { Platform = SocialPlatform.Facebook });
        await db.SaveChangesAsync();
        return post.Id;
    });
    public Task<int> Submit(int id, string userId) => work.Run(async () =>
    {
        var post = await Get(id);
        Rules.Require(post.Status == PostStatus.Draft, "Only a draft can be submitted.");
        Rules.Require(!string.IsNullOrWhiteSpace(post.Caption), "Add a caption first.");
        post.Status = PostStatus.PendingApproval;
        post.ApprovalRequests.Add(new ApprovalRequest { Status = ApprovalStatus.Pending, SubmittedAt = time.GetUtcNow().UtcDateTime, SubmittedByUserId = userId });
        return id;
    });
    public Task<int> Review(int id, bool approve, string? comment, string userId) => work.Run(async () =>
    {
        var post = await Get(id);
        Rules.Require(post.Status == PostStatus.PendingApproval, "This post is not awaiting review.");
        Rules.Require((comment?.Length ?? 0) <= 1000, "Review comments must be at most 1,000 characters.");
        if (!approve)
            Rules.Text(comment, 1000, "Revision instructions");
        if (approve)
            Rules.Require(!post.Annotations.Any(a => !a.IsResolved), "Resolve every image annotation before approval.");
        var request = post.ApprovalRequests.OrderByDescending(a => a.Id).First(a => a.Status == ApprovalStatus.Pending);
        request.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.ChangesRequested;
        request.ReviewerComment = comment?.Trim();
        request.ReviewedByUserId = userId;
        request.ReviewedAt = time.GetUtcNow().UtcDateTime;
        post.Status = approve ? PostStatus.Approved : PostStatus.Draft;
        return id;
    });
    public Task<int> Annotate(int postId, int mediaId, decimal x, decimal y, string comment, string userId) => work.Run(async () =>
    {
        var post = await Get(postId);
        Rules.Require(post.Status is PostStatus.Draft or PostStatus.PendingApproval, "Annotations can only be added during drafting or review.");
        Rules.Require(post.Media.Any(m => m.Id == mediaId), "Image not found on this post.");
        ValidateCoordinates(x, y);
        post.Annotations.Add(new Annotation
        {
            PostMediaId = mediaId,
            XPercent = x,
            YPercent = y,
            Comment = Rules.Text(comment, 1000, "Comment"),
            AuthorUserId = userId,
            ApprovalRequestId = post.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending).OrderByDescending(a => a.Id).FirstOrDefault()?.Id
        });
        return postId;
    });
    public Task<int> Resolve(int id) => work.Run(async () =>
    {
        var annotation = await db.Annotations.SingleOrDefaultAsync(a => a.Id == id) ?? throw new BusinessRuleException("Annotation not found.");
        annotation.IsResolved = true;
        return annotation.PostId;
    });
    public static void ValidateCoordinates(decimal x, decimal y) => Rules.Require(x >= 0 && x <= 100 && y >= 0 && y <= 100, "Marker coordinates must be between 0 and 100 percent.");
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Catalog-to-social, approval, assets, campaigns, ROI, reviews (Features 17–24) ──

/// <summary>Per-platform limits PostValidator checks (17). Shared reference data.</summary>
[Index(nameof(Platform), IsUnique = true)]
public class PlatformRule
{
    public int Id { get; set; }
    public SocialPlatform Platform { get; set; }
    public int MaxCaptionLength { get; set; }
    public int MaxHashtags { get; set; }
    public int MaxMediaCount { get; set; }
    [Precision(6, 3)] public decimal MinAspectRatio { get; set; }
    [Precision(6, 3)] public decimal MaxAspectRatio { get; set; }
}

public class Campaign : TenantEntity
{
    [MaxLength(150)] public string Name { get; set; } = "";
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public decimal? Budget { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }

    public ICollection<Coupon> Coupons { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<TrackedLink> TrackedLinks { get; set; } = [];
}

public class Post : TenantEntity
{
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string Caption { get; set; } = "";
    public PostStatus Status { get; set; }
    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public ICollection<PostMedia> Media { get; set; } = [];
    public ICollection<PostTarget> Targets { get; set; } = [];
    public ICollection<PostTaggedVariant> TaggedVariants { get; set; } = [];
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = [];
    public ICollection<Annotation> Annotations { get; set; } = [];
}

public class PostMedia : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public MediaType MediaType { get; set; }
    [MaxLength(500)] public string Url { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int SortOrder { get; set; }
}

[Index(nameof(PostId), nameof(Platform), IsUnique = true)]
public class PostTarget : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public SocialPlatform Platform { get; set; }
    [MaxLength(100)] public string? ExternalPostId { get; set; }
    public DateTime? PublishedAt { get; set; }
}

/// <summary>SKUs a post promotes; the publish job holds the post if any of them is locked out (19).</summary>
[Index(nameof(PostId), nameof(ProductVariantId), IsUnique = true)]
public class PostTaggedVariant : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}

public class ApprovalRequest : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? SubmittedByUserId { get; set; }
    public AppUser? SubmittedByUser { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public AppUser? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(1000)] public string? ReviewerComment { get; set; }
}

/// <summary>Marker positions are percentages so they stay in place at any image size (18).</summary>
public class Annotation : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public int PostMediaId { get; set; }
    public PostMedia? PostMedia { get; set; }
    public int? ApprovalRequestId { get; set; }
    public ApprovalRequest? ApprovalRequest { get; set; }
    [Precision(5, 2)] public decimal XPercent { get; set; }
    [Precision(5, 2)] public decimal YPercent { get; set; }
    [MaxLength(1000)] public string Comment { get; set; } = "";
    public string? AuthorUserId { get; set; }
    public AppUser? AuthorUser { get; set; }
    public bool IsResolved { get; set; }
}

[Index(nameof(MerchantId), nameof(ScheduledFor))]
public class ScheduledPost : TenantEntity
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public DateTime ScheduledFor { get; set; }
    public ScheduledPostStatus Status { get; set; }
    [MaxLength(64)] public string? HangfireJobId { get; set; }
    [MaxLength(300)] public string? HoldReason { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class Asset : TenantEntity
{
    [MaxLength(200)] public string Name { get; set; } = "";
    public AssetType Type { get; set; }
    [MaxLength(500)] public string FilePath { get; set; } = "";
    [MaxLength(100)] public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int VersionNumber { get; set; } = 1;
    /// <summary>The previous version of this asset, if any.</summary>
    public int? PreviousVersionId { get; set; }
    public Asset? PreviousVersion { get; set; }
    public BrandCheckResult CheckResult { get; set; }
    [MaxLength(1000)] public string? CheckReasons { get; set; }
    /// <summary>Comma-separated hex colours extracted by SkiaSharp.</summary>
    [MaxLength(200)] public string? DominantColors { get; set; }
    public string? UploadedByUserId { get; set; }
    public AppUser? UploadedByUser { get; set; }
}

public class BrandKit : TenantEntity
{
    [MaxLength(100)] public string Name { get; set; } = "";
    public int? LogoAssetId { get; set; }
    public Asset? LogoAsset { get; set; }
    [MaxLength(100)] public string? PrimaryFont { get; set; }
    [MaxLength(100)] public string? SecondaryFont { get; set; }
    /// <summary>Comma-separated hex colours, e.g. "#1F4E79,#F2A900".</summary>
    [MaxLength(200)] public string Palette { get; set; } = "";
    /// <summary>Maximum colour distance still counted as on-palette.</summary>
    [Precision(6, 2)] public decimal ColorTolerance { get; set; } = 30m;
    public int MinWidth { get; set; } = 1080;
    public int MinHeight { get; set; } = 1080;
    public bool IsActive { get; set; } = true;
}

[Index(nameof(MerchantId), nameof(Code), IsUnique = true)]
public class Coupon : TenantEntity
{
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    [MaxLength(32)] public string Code { get; set; } = "";
    public CouponType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? TotalLimit { get; set; }
    public int? PerPhoneLimit { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Code is globally unique because the /r/{code} route carries no merchant.</summary>
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(MessengerRef))]
public class TrackedLink : TenantEntity
{
    public int CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(1000)] public string DestinationUrl { get; set; } = "";
    [MaxLength(100)] public string? UtmSource { get; set; }
    [MaxLength(100)] public string? UtmMedium { get; set; }
    [MaxLength(100)] public string? UtmCampaign { get; set; }
    [MaxLength(100)] public string? UtmContent { get; set; }
    /// <summary>The ref value on an m.me link; Messenger passes it to our webhook (21).</summary>
    [MaxLength(64)] public string? MessengerRef { get; set; }
    public int Clicks { get; set; }
}

[Index(nameof(OrderId), IsUnique = true)]
public class Attribution : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public int? TrackedLinkId { get; set; }
    public TrackedLink? TrackedLink { get; set; }
    public AttributionSource Source { get; set; }
}

[Index(nameof(MerchantId), nameof(Date))]
public class AdSpend : TenantEntity
{
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public DateOnly Date { get; set; }
    public SocialPlatform Platform { get; set; }
    [MaxLength(100)] public string? CreativeKey { get; set; }
    public decimal Amount { get; set; }
    public AdSpendSource Source { get; set; }
}

[Index(nameof(MerchantId), nameof(CreativeKey), nameof(WeekStart), IsUnique = true)]
public class CreativeWeeklyMetric : TenantEntity
{
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    [MaxLength(100)] public string CreativeKey { get; set; } = "";
    public DateOnly WeekStart { get; set; }
    public decimal Spend { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int Orders { get; set; }
}

public class FatigueAlert : TenantEntity
{
    [MaxLength(100)] public string CreativeKey { get; set; } = "";
    public DateOnly WeekStart { get; set; }
    public decimal BaselineCostPerOrder { get; set; }
    public decimal CurrentCostPerOrder { get; set; }
    [Precision(7, 4)] public decimal BaselineConversionRate { get; set; }
    [Precision(7, 4)] public decimal CurrentConversionRate { get; set; }
    public FatigueAlertStatus Status { get; set; }
}

/// <summary>One invite per Delivered order; only the token's hash is stored.</summary>
[Index(nameof(OrderId), IsUnique = true)]
[Index(nameof(TokenHash), IsUnique = true)]
public class ReviewInvite : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    [MaxLength(64)] public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

/// <summary>Each order allows one review; the owner approves before a proof card is drawn.</summary>
[Index(nameof(ReviewInviteId), IsUnique = true)]
[Index(nameof(OrderId), IsUnique = true)]
public class Review : TenantEntity
{
    public int ReviewInviteId { get; set; }
    public ReviewInvite? ReviewInvite { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public byte Rating { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    [MaxLength(100)] public string CustomerDisplayName { get; set; } = "";
    public ReviewStatus Status { get; set; }
    public string? ModeratedByUserId { get; set; }
    public AppUser? ModeratedByUser { get; set; }
    public DateTime? ModeratedAt { get; set; }
    [MaxLength(500)] public string? CardImagePath { get; set; }
}

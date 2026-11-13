using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Audit (Feature 15) ─────────────────────────────────────────────────────

/// <summary>
/// Append-only. Each row stores the previous row's hash, so editing or deleting a row
/// breaks the chain. The app's database user should only have INSERT and SELECT on this table.
/// </summary>
[Index(nameof(MerchantId), nameof(OccurredAt))]
[Index(nameof(EntityType), nameof(EntityId))]
[Index(nameof(UserId))]
public class AuditEvent : ITenantEntity
{
    public long Id { get; set; }
    public int MerchantId { get; set; }
    public string? UserId { get; set; }
    public AuditAction Action { get; set; }
    [MaxLength(100)] public string EntityType { get; set; } = "";
    [MaxLength(64)] public string EntityId { get; set; } = "";
    /// <summary>JSON of changed fields (old and new values). Empty for reads.</summary>
    public string? ChangesJson { get; set; }
    [MaxLength(45)] public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string PreviousHash { get; set; } = "";
    [MaxLength(64)] public string Hash { get; set; } = "";
}

// ── AI Business Starter Coach (Feature 25) ─────────────────────────────────
// Not tenant-scoped: a first-time seller may not have a merchant yet.

public class BusinessProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    [MaxLength(200)] public string ProductIdea { get; set; } = "";
    [MaxLength(100)] public string? Category { get; set; }
    public decimal StartingBudget { get; set; }
    public int HoursPerWeek { get; set; }
    [MaxLength(300)] public string? Skills { get; set; }
    [MaxLength(200)] public string? Devices { get; set; }
    public bool HasTradeLicense { get; set; }
    /// <summary>"bn" or "en"; the coach answers in this language.</summary>
    [MaxLength(5)] public string Language { get; set; } = "bn";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CoachPlan? Plan { get; set; }
}

/// <summary>Everything here is computed by our own rules; the AI only explains it.</summary>
[Index(nameof(BusinessProfileId), IsUnique = true)]
public class CoachPlan
{
    public int Id { get; set; }
    public int BusinessProfileId { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public ReadinessVerdict Verdict { get; set; }
    [MaxLength(1000)] public string VerdictReasons { get; set; } = "";
    public decimal BudgetStock { get; set; }
    public decimal BudgetPackaging { get; set; }
    public decimal BudgetDelivery { get; set; }
    public decimal BudgetBoosting { get; set; }
    public decimal BudgetBranding { get; set; }
    /// <summary>Which version of the rules produced this plan, for reproducibility.</summary>
    [MaxLength(16)] public string RulesVersion { get; set; } = "1";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoadmapItem> RoadmapItems { get; set; } = [];
    public ICollection<CoachChatMessage> ChatMessages { get; set; } = [];
}

public class RoadmapItem
{
    public int Id { get; set; }
    public int CoachPlanId { get; set; }
    public CoachPlan? CoachPlan { get; set; }
    public int Week { get; set; }
    public int SortOrder { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    /// <summary>The BikriBoost feature this step opens, e.g. 16, 19 or 20.</summary>
    public int? FeatureNumber { get; set; }
    [MaxLength(200)] public string? LinkPath { get; set; }
    public bool IsDone { get; set; }
}

[Index(nameof(CoachPlanId), nameof(CreatedAt))]
public class CoachChatMessage
{
    public int Id { get; set; }
    public int CoachPlanId { get; set; }
    public CoachPlan? CoachPlan { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = "";
    /// <summary>True when the AI call failed and the "coach unavailable" reply was shown.</summary>
    public bool IsFallback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

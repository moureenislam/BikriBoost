using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Orders and payments (Features 2–7) ────────────────────────────────────

/// <summary>Rule-based parse of a chat message (2). Missing fields are listed, never guessed.</summary>
public class OrderDraft : TenantEntity
{
    /// <summary>The inbox thread it came from; null when the text was pasted in directly.</summary>
    public int? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public string RawText { get; set; } = "";
    [MaxLength(150)] public string? ParsedName { get; set; }
    [MaxLength(20)] public string? ParsedPhone { get; set; }
    [MaxLength(500)] public string? ParsedAddress { get; set; }
    public int? DistrictId { get; set; }
    public District? District { get; set; }
    public int? ThanaId { get; set; }
    public Thana? Thana { get; set; }
    /// <summary>JSON list of { sku, quantity } matched from the text.</summary>
    public string? ParsedItemsJson { get; set; }
    [MaxLength(200)] public string? MissingFields { get; set; }
    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    /// <summary>Set once the draft becomes a real order.</summary>
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
}

[Index(nameof(MerchantId), nameof(OrderNumber), IsUnique = true)]
[Index(nameof(MerchantId), nameof(Status), nameof(CreatedAt))]
[Index(nameof(ShippingPhone))]
public class Order : TenantEntity
{
    [MaxLength(32)] public string OrderNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    /// <summary>Only OrderLifecycleService changes this; only Feature 9 may set Settled.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(150)] public string ShippingName { get; set; } = "";
    [MaxLength(20)] public string ShippingPhone { get; set; } = "";
    [MaxLength(500)] public string ShippingAddress { get; set; } = "";
    public int DistrictId { get; set; }
    public District? District { get; set; }
    public int? ThanaId { get; set; }
    public Thana? Thana { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    /// <summary>Advance the customer must pay before dispatch (set by RiskScorer, Feature 6). 0 = none.</summary>
    public decimal AdvanceRequired { get; set; }
    /// <summary>Advance confirmed so far through verified TrxIDs (Feature 4).</summary>
    public decimal AdvancePaid { get; set; }
    /// <summary>Courier and return losses recorded against the order (11), subtracted in ROI (22).</summary>
    public decimal LogisticsLoss { get; set; }

    /// <summary>A High risk score locks the order until an advance is verified (6).</summary>
    public bool IsLockedForAdvance { get; set; }

    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    /// <summary>The moderator who earns commission when this order is Settled (14).</summary>
    public string? AttributedModeratorUserId { get; set; }
    public AppUser? AttributedModeratorUser { get; set; }
    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Cash the courier must collect at the door.</summary>
    [NotMapped] public decimal CodAmount => Math.Max(0, Total - AdvancePaid);
    /// <summary>Advance still owed before the order may be dispatched.</summary>
    [NotMapped] public decimal AdvanceDue => Math.Max(0, AdvanceRequired - AdvancePaid);

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<PaymentClaim> PaymentClaims { get; set; } = [];
    public ICollection<ReturnCase> ReturnCases { get; set; } = [];
    public RiskAssessment? RiskAssessment { get; set; }
    public Shipment? Shipment { get; set; }
}

public class OrderItem : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>Cost at the time of sale, so later price edits do not change past ROI.</summary>
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

[Index(nameof(OrderId), nameof(ChangedAt))]
public class OrderStatusHistory : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(300)] public string? Note { get; set; }
}

[Index(nameof(MerchantId), nameof(InvoiceNumber), IsUnique = true)]
public class Invoice : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    [MaxLength(32)] public string InvoiceNumber { get; set; } = "";
    public InvoiceStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    /// <summary>The chat message that carried the invoice card.</summary>
    public int? SentInMessageId { get; set; }
    public Message? SentInMessage { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];
    public ICollection<PaymentLink> PaymentLinks { get; set; } = [];
}

public class InvoiceLine : TenantEntity
{
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    [MaxLength(200)] public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

[Index(nameof(Token), IsUnique = true)]
[Index(nameof(Status), nameof(ExpiresAt))]
public class PaymentLink : TenantEntity
{
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    /// <summary>Random URL-safe token from a cryptographic generator.</summary>
    [MaxLength(64)] public string Token { get; set; } = "";
    public PaymentProvider? Provider { get; set; }
    public decimal Amount { get; set; }
    public PaymentLinkStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    [MaxLength(100)] public string? GatewayPaymentId { get; set; }
}

/// <summary>The unique index on (Provider, TrxId) enforces platform-wide TrxID uniqueness (4).</summary>
[Index(nameof(Provider), nameof(TrxId), IsUnique = true)]
[Index(nameof(MerchantId), nameof(Status))]
public class PaymentClaim : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentProvider Provider { get; set; }
    [MaxLength(32)] public string TrxId { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentClaimSource Source { get; set; }
    public PaymentClaimStatus Status { get; set; }
    [MaxLength(20)] public string? SenderPhone { get; set; }
    public int? PaymentLinkId { get; set; }
    public PaymentLink? PaymentLink { get; set; }
    public string? ReviewedByUserId { get; set; }
    public AppUser? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }
}

[Index(nameof(OrderId), IsUnique = true)]
public class RiskAssessment : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int Score { get; set; }
    public RiskLevel Level { get; set; }
    /// <summary>JSON breakdown of each factor and its points, shown on the risk badge.</summary>
    public string FactorsJson { get; set; } = "[]";
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public string? OverriddenByUserId { get; set; }
    public AppUser? OverriddenByUser { get; set; }
    [MaxLength(300)] public string? OverrideReason { get; set; }
}

[Index(nameof(MerchantId), nameof(IpAddress), IsUnique = true)]
public class BlockedIp : TenantEntity
{
    [MaxLength(45)] public string IpAddress { get; set; } = "";
    [MaxLength(300)] public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? BlockedByUserId { get; set; }
    public AppUser? BlockedByUser { get; set; }
}

/// <summary>Collected only on our own public pages (Pay, Track); chat orders carry no customer IP.</summary>
[Index(nameof(MerchantId), nameof(FingerprintHash))]
public class DeviceFingerprint : TenantEntity
{
    [MaxLength(64)] public string FingerprintHash { get; set; } = "";
    [MaxLength(45)] public string IpAddress { get; set; } = "";
    [MaxLength(500)] public string? UserAgent { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public bool IsBlocked { get; set; }
}

/// <summary>
/// Shared cross-merchant registry (7). Deliberately NOT ITenantEntity: every merchant can
/// look up a phone's history, but only by its HMAC, never the plain number.
/// </summary>
[Index(nameof(PhoneHmac))]
[Index(nameof(MerchantId), nameof(OrderId), IsUnique = true)]
public class FraudReport
{
    public int Id { get; set; }
    [MaxLength(64)] public string PhoneHmac { get; set; } = "";
    public int MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public int OrderId { get; set; }
    public FraudReason Reason { get; set; }
    public FraudReportStatus Status { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Logistics and reconciliation (Features 8–11) ───────────────────────────

/// <summary>Shared reference data: Pathao, Steadfast, RedX.</summary>
[Index(nameof(Code), IsUnique = true)]
public class Courier
{
    public int Id { get; set; }
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    /// <summary>False means TrackingService polls this courier with a Hangfire job.</summary>
    public bool HasWebhook { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(OrderId), IsUnique = true)]
[Index(nameof(CourierId), nameof(ConsignmentId), IsUnique = true)]
public class Shipment : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int CourierId { get; set; }
    public Courier? Courier { get; set; }
    [MaxLength(64)] public string ConsignmentId { get; set; } = "";
    [MaxLength(64)] public string? Awb { get; set; }
    public ShipmentStatus Status { get; set; }
    public decimal CodAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    public ICollection<ShipmentEvent> Events { get; set; } = [];
}

[Index(nameof(ShipmentId), nameof(OccurredAt))]
public class ShipmentEvent : TenantEntity
{
    public int ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public ShipmentStatus Status { get; set; }
    [MaxLength(100)] public string RawStatus { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
    public ShipmentEventSource Source { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>Only the SHA-256 hash of the token is stored; the plain token lives only in the SMS link.</summary>
[Index(nameof(TokenHash), IsUnique = true)]
public class TrackingToken : TenantEntity
{
    public int ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    [MaxLength(64)] public string TokenHash { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

/// <summary>District × courier delivery history used by CourierRecommender (8). Shared across merchants.</summary>
[Index(nameof(DistrictId), nameof(CourierId), IsUnique = true)]
public class DeliveryStat
{
    public int Id { get; set; }
    public int DistrictId { get; set; }
    public District? District { get; set; }
    public int CourierId { get; set; }
    public Courier? Courier { get; set; }
    public int Parcels { get; set; }
    public int Delivered { get; set; }
    public int Returned { get; set; }
    public decimal AvgDeliveryHours { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SettlementBatch : TenantEntity
{
    public int CourierId { get; set; }
    public Courier? Courier { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = "";
    public string? UploadedByUserId { get; set; }
    public AppUser? UploadedByUser { get; set; }
    public SettlementBatchStatus Status { get; set; }
    public int TotalLines { get; set; }
    public int MatchedLines { get; set; }
    public decimal TotalPayout { get; set; }

    public ICollection<SettlementLine> Lines { get; set; } = [];
    public ICollection<Discrepancy> Discrepancies { get; set; } = [];
}

[Index(nameof(SettlementBatchId), nameof(ConsignmentId))]
public class SettlementLine : TenantEntity
{
    public int SettlementBatchId { get; set; }
    public SettlementBatch? SettlementBatch { get; set; }
    [MaxLength(64)] public string ConsignmentId { get; set; } = "";
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal CodFee { get; set; }
    public decimal NetPayout { get; set; }
    public bool IsMatched { get; set; }
}

public class Discrepancy : TenantEntity
{
    public int SettlementBatchId { get; set; }
    public SettlementBatch? SettlementBatch { get; set; }
    public int? SettlementLineId { get; set; }
    public SettlementLine? SettlementLine { get; set; }
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public DiscrepancyType Type { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolvedByUserId { get; set; }
    public AppUser? ResolvedByUser { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public class ReturnCase : TenantEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public ReturnType Type { get; set; }
    public ReturnStatus Status { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = "";
    public DateTime? ReceivedAt { get; set; }
    /// <summary>For an exchange, the replacement order.</summary>
    public int? ExchangeOrderId { get; set; }
    public Order? ExchangeOrder { get; set; }
    public decimal LogisticsLoss { get; set; }
    public string? OpenedByUserId { get; set; }
    public AppUser? OpenedByUser { get; set; }

    public QcInspection? Inspection { get; set; }
}

/// <summary>Stock is restocked only after a QC pass.</summary>
[Index(nameof(ReturnCaseId), IsUnique = true)]
public class QcInspection : TenantEntity
{
    public int ReturnCaseId { get; set; }
    public ReturnCase? ReturnCase { get; set; }
    public QcGrade Grade { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public bool Restocked { get; set; }
    public string? InspectedByUserId { get; set; }
    public AppUser? InspectedByUser { get; set; }
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
}

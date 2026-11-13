using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Catalog and stock (Features 5, 16) ────────────────────────────────────

public class Product : TenantEntity
{
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(100)] public string? Category { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductVariant> Variants { get; set; } = [];
}

[Index(nameof(MerchantId), nameof(Sku), IsUnique = true)]
public class ProductVariant : TenantEntity, IVersioned
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [MaxLength(64)] public string Sku { get; set; } = "";
    [MaxLength(32)] public string? Size { get; set; }
    [MaxLength(32)] public string? Color { get; set; }
    [MaxLength(64)] public string? Material { get; set; }

    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }

    /// <summary>Units physically on hand.</summary>
    public int Stock { get; set; }
    /// <summary>Units held by Confirmed orders that have not shipped yet.</summary>
    public int Reserved { get; set; }
    /// <summary>Checkout locks when available stock reaches this. Defaults to 0 as in the brief.</summary>
    public int SafetyThreshold { get; set; }
    public bool IsLockedOut { get; set; }

    /// <summary>Concurrency token: stops two moderators reserving the last unit.</summary>
    [ConcurrencyCheck] public int Version { get; set; }

    [NotMapped] public int Available => Stock - Reserved;
}

[Index(nameof(OrderId), nameof(ProductVariantId))]
public class StockReservation : TenantEntity
{
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime? ReleasedAt { get; set; }
}

[Index(nameof(ProductVariantId), nameof(CreatedAt))]
public class StockEvent : TenantEntity
{
    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public StockEventType Type { get; set; }
    public int QuantityChange { get; set; }
    public int StockAfter { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

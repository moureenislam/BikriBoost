using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BikriBoost.Web.ViewModels;

/// <summary>Orders/Index: the order list, filtered by status.</summary>
public class OrderListViewModel
{
    public OrderStatus? Status { get; set; }
    public Dictionary<OrderStatus, int> Counts { get; set; } = [];
    public List<OrderRow> Orders { get; set; } = [];
}

public record OrderRow(int Id, string OrderNumber, string Customer, string Phone, string District,
    decimal Total, OrderStatus Status, bool Locked, DateTime CreatedAt);

/// <summary>Orders/Capture and Orders/Draft: paste a chat message, then check the parsed draft (Feature 2).</summary>
public class DraftViewModel
{
    public string RawText { get; set; } = "";
    public NewOrderInput Form { get; set; } = new();
    public List<string> Missing { get; set; } = [];
    public string? UnmatchedItemsText { get; set; }
    public List<string> DetectedNotes { get; set; } = [];
    public SelectList Districts { get; set; } = null!;
    public SelectList Thanas { get; set; } = null!;
    public List<SelectListItem> Variants { get; set; } = [];
}

/// <summary>Orders/Details: one order with its timeline, next steps and advance payment.</summary>
public class OrderDetailsViewModel
{
    public Order Order { get; set; } = null!;
    public string DistrictName { get; set; } = "";
    public string? ThanaName { get; set; }
    public List<(OrderItem Item, string Name)> Items { get; set; } = [];
    public List<OrderStatusHistory> History { get; set; } = [];
    public List<OrderStatus> NextSteps { get; set; } = [];
    public SelectList Couriers { get; set; } = null!;
    public string? CourierName { get; set; }
    public List<PaymentClaim> Payments { get; set; } = [];
    public bool IsOwner { get; set; }

    /// <summary>Feature 4: the shop can ask for an advance before the parcel leaves.</summary>
    public bool CanRequestAdvance => Order.Status is OrderStatus.Pending or OrderStatus.Confirmed
        && !Payments.Any(p => p.Status == PaymentClaimStatus.Pending);
    public bool CanSubmitPayment => Order.AdvanceDue > 0 && !Payments.Any(p => p.Status == PaymentClaimStatus.Pending)
        && Order.Status is OrderStatus.Pending or OrderStatus.Confirmed;
}

/// <summary>Payments/Review: TrxIDs waiting for the owner (Feature 4).</summary>
public class PaymentReviewViewModel
{
    public List<PaymentClaim> Pending { get; set; } = [];
    public List<PaymentClaim> Recent { get; set; } = [];
    public bool IsOwner { get; set; }
}

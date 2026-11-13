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
}

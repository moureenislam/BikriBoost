using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Turns a checked draft (Feature 2) into a Pending order: finds or creates the customer by phone,
/// and prices the items. The new order starts as Pending.
/// </summary>
public class OrderService(AppDbContext db, ICurrentMerchant currentMerchant)
{
    public const decimal DhakaDeliveryCharge = 70m;
    public const decimal OutsideDhakaDeliveryCharge = 130m;
    public const int MaxQuantityPerLine = 100;

    public static decimal DefaultDeliveryCharge(string? districtName) =>
        districtName == "Dhaka" ? DhakaDeliveryCharge : OutsideDhakaDeliveryCharge;

    public async Task<Order> CreateAsync(NewOrderInput input, string userId)
    {
        var merchantId = currentMerchant.MerchantId ?? throw new RuleViolationException("Sign in to a shop first.");
        var phone = PhoneNumber.Normalize(input.Phone)
            ?? throw new RuleViolationException("Phone must be a valid Bangladeshi mobile number (01[3-9] + 8 digits).");
        if (string.IsNullOrWhiteSpace(input.Name)) throw new RuleViolationException("Enter the customer's name.");
        if (string.IsNullOrWhiteSpace(input.Address)) throw new RuleViolationException("Enter the delivery address.");
        var district = await db.Districts.FirstOrDefaultAsync(d => d.Id == input.DistrictId)
            ?? throw new RuleViolationException("Choose the delivery district.");
        if (input.ThanaId is int thanaId && !await db.Thanas.AnyAsync(t => t.Id == thanaId && t.DistrictId == district.Id))
            throw new RuleViolationException("That thana is not in the chosen district.");

        var lines = input.Items.Where(i => i.ProductVariantId is not null && i.Quantity > 0).ToList();
        if (lines.Count == 0) throw new RuleViolationException("Add at least one item.");
        if (lines.Any(l => l.Quantity > MaxQuantityPerLine))
            throw new RuleViolationException($"A line can have at most {MaxQuantityPerLine} units.");
        if (lines.GroupBy(l => l.ProductVariantId).Any(g => g.Count() > 1))
            throw new RuleViolationException("The same item is listed twice; combine the quantities.");

        var charge = input.DeliveryCharge ?? DefaultDeliveryCharge(district.NameEn);
        if (charge is < 0 or > 2000) throw new RuleViolationException("Delivery charge must be between Tk 0 and Tk 2,000.");

        var ids = lines.Select(l => l.ProductVariantId!.Value).ToList();
        var variants = await db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (variants.Count != ids.Count) throw new RuleViolationException("One of the items is not in this shop's catalogue.");

        await using var tx = await db.Database.BeginTransactionAsync();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer is null)
        {
            customer = new Customer { MerchantId = merchantId, Phone = phone };
            db.Customers.Add(customer);
        }
        customer.Name = input.Name.Trim();
        customer.Address = input.Address.Trim();
        customer.DistrictId = district.Id;
        customer.ThanaId = input.ThanaId;

        var order = new Order
        {
            MerchantId = merchantId,
            OrderNumber = await NextOrderNumberAsync(),
            Customer = customer,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.Cod,
            ShippingName = input.Name.Trim(),
            ShippingPhone = phone,
            ShippingAddress = input.Address.Trim(),
            DistrictId = district.Id,
            ThanaId = input.ThanaId,
            DeliveryCharge = charge,
            CreatedByUserId = userId,
            AttributedModeratorUserId = userId,
        };
        foreach (var line in lines)
        {
            var v = variants[line.ProductVariantId!.Value];
            order.Items.Add(new OrderItem
            {
                MerchantId = merchantId, ProductVariantId = v.Id, Quantity = line.Quantity,
                UnitPrice = v.Price, UnitCost = v.CostPrice, LineTotal = v.Price * line.Quantity,
            });
        }
        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.Total = order.Subtotal + order.DeliveryCharge - order.Discount;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            MerchantId = merchantId, ToStatus = OrderStatus.Pending, ChangedByUserId = userId,
            Note = input.DraftId is null ? "Created" : "Created from chat draft",
        });
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        if (input.DraftId is int draftId && await db.OrderDrafts.FirstOrDefaultAsync(d => d.Id == draftId) is { } draft)
        {
            draft.OrderId = order.Id;
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return order;
    }

    /// <summary>Saves the raw message and what the parser found, so the draft can be traced later.</summary>
    public async Task<OrderDraft> SaveDraftAsync(string rawText, ParsedOrder parsed, string userId)
    {
        var draft = new OrderDraft
        {
            RawText = rawText,
            ParsedName = parsed.Name,
            ParsedPhone = parsed.Phone,
            ParsedAddress = parsed.Address is { Length: > 500 } a ? a[..500] : parsed.Address,
            DistrictId = parsed.DistrictId,
            ThanaId = parsed.ThanaId,
            ParsedItemsJson = System.Text.Json.JsonSerializer.Serialize(
                parsed.Items.Select(i => new { i.ProductVariantId, i.Quantity })),
            MissingFields = string.Join(", ", parsed.Missing) is { Length: > 200 } m ? m[..200] : string.Join(", ", parsed.Missing),
            CreatedByUserId = userId,
        };
        db.OrderDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft;
    }

    private async Task<string> NextOrderNumberAsync()
    {
        var count = await db.Orders.CountAsync();
        return $"BB-{count + 1:D5}";
    }
}

public class NewOrderInput
{
    public int? DraftId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? DistrictId { get; set; }
    public int? ThanaId { get; set; }
    public decimal? DeliveryCharge { get; set; }
    public List<NewOrderLine> Items { get; set; } = [];
}

public class NewOrderLine
{
    public int? ProductVariantId { get; set; }
    public int Quantity { get; set; }
}

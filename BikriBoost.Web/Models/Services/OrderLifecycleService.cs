using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Feature 5 · Strict State-Machine Order Lifecycle.
///
///   Pending ──► Confirmed ──► Dispatched ──► Delivered ──► Settled
///      │            │              │             │
///      └► Cancelled ┘              └► Returned ◄─┘
///
/// - Only the moves in <see cref="Allowed"/> are possible; anything else is refused.
/// - Confirm reserves stock; Cancel releases it; Dispatch takes it out of the warehouse.
/// - Only the Owner marks Delivered or Returned.
/// - Settled is never offered to a user: it is meant to come only from the courier settlement (Feature 9, a teammate's part).
/// - Every change writes an OrderStatusHistory row, inside one database transaction.
/// </summary>
public class OrderLifecycleService(AppDbContext db)
{
    public static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Dispatched, OrderStatus.Cancelled],
        [OrderStatus.Dispatched] = [OrderStatus.Delivered, OrderStatus.Returned],
        [OrderStatus.Delivered] = [OrderStatus.Returned, OrderStatus.Settled],
        [OrderStatus.Settled] = [],
        [OrderStatus.Returned] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static readonly OrderStatus[] OwnerOnly = [OrderStatus.Delivered, OrderStatus.Returned];

    public static bool CanMove(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    /// <summary>The buttons to show: allowed moves this user may make (never Settled).</summary>
    public static IEnumerable<OrderStatus> NextStepsFor(OrderStatus from, bool isOwner) =>
        Allowed[from].Where(s => s != OrderStatus.Settled && (isOwner || !OwnerOnly.Contains(s)));

    public Task ConfirmAsync(int orderId, string userId) =>
        MoveAsync(orderId, OrderStatus.Confirmed, userId, isOwner: false, null, ReserveStockAsync);

    public Task CancelAsync(int orderId, string userId, string? note) =>
        MoveAsync(orderId, OrderStatus.Cancelled, userId, isOwner: false, note, ReleaseStockAsync);

    public Task DispatchAsync(int orderId, int courierId, string? consignmentId, decimal deliveryCharge, string userId) =>
        MoveAsync(orderId, OrderStatus.Dispatched, userId, isOwner: false, $"Courier consignment {consignmentId?.Trim()}",
            order => ShipAsync(order, courierId, consignmentId, deliveryCharge));

    public Task MarkDeliveredAsync(int orderId, string userId, bool isOwner) =>
        MoveAsync(orderId, OrderStatus.Delivered, userId, isOwner, null, order =>
        {
            order.DeliveredAt = DateTime.UtcNow;
            if (order.Shipment is { } s) { s.Status = ShipmentStatus.Delivered; s.DeliveredAt = order.DeliveredAt; }
            return Task.CompletedTask;
        });

    public Task MarkReturnedAsync(int orderId, string userId, bool isOwner, string? note) =>
        MoveAsync(orderId, OrderStatus.Returned, userId, isOwner, note, order =>
        {
            if (order.Shipment is { } s) s.Status = ShipmentStatus.Returned;
            return Task.CompletedTask; // stock comes back only after QC inspection (Feature 11)
        });

    private async Task MoveAsync(int orderId, OrderStatus to, string userId, bool isOwner, string? note, Func<Order, Task> sideEffects)
    {
        var order = await db.Orders
            .Include(o => o.Items).ThenInclude(i => i.ProductVariant)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new RuleViolationException("Order not found.");

        if (!CanMove(order.Status, to) || to == OrderStatus.Settled)
            throw new RuleViolationException($"An order cannot move from {order.Status} to {to}.");
        if (OwnerOnly.Contains(to) && !isOwner)
            throw new RuleViolationException($"Only the shop owner can mark an order as {to}.");

        await using var tx = await db.Database.BeginTransactionAsync();
        await sideEffects(order);
        AddHistory(order, to, userId, note);
        order.Status = to;
        await db.SaveChangesAsync(); // throws DbUpdateConcurrencyException if someone changed the same stock first
        await tx.CommitAsync();
    }

    private void AddHistory(Order order, OrderStatus to, string userId, string? note) =>
        db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            MerchantId = order.MerchantId,
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = to,
            ChangedByUserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });

    private Task ReserveStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var v = item.ProductVariant!;
            if (v.Available < item.Quantity)
                throw new RuleViolationException($"Not enough stock for {v.Sku}: {v.Available} available, {item.Quantity} needed.");
            v.Reserved += item.Quantity;
            db.StockReservations.Add(new StockReservation
            {
                MerchantId = order.MerchantId, ProductVariant = v, OrderId = order.Id,
                Quantity = item.Quantity, Status = ReservationStatus.Active,
            });
            AddStockEvent(order, v, StockEventType.Reserved, 0, $"Reserved {item.Quantity} for {order.OrderNumber}");
        }
        order.ConfirmedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private async Task ReleaseStockAsync(Order order)
    {
        if (order.Status == OrderStatus.Confirmed)
        {
            var reservations = await db.StockReservations
                .Where(r => r.OrderId == order.Id && r.Status == ReservationStatus.Active).ToListAsync();
            foreach (var r in reservations)
            {
                var v = order.Items.First(i => i.ProductVariantId == r.ProductVariantId).ProductVariant!;
                v.Reserved -= Math.Min(r.Quantity, v.Reserved);
                r.Status = ReservationStatus.Released;
                r.ReleasedAt = DateTime.UtcNow;
                AddStockEvent(order, v, StockEventType.Released, 0, $"Released {r.Quantity} from cancelled {order.OrderNumber}");
            }
        }
        order.CancelledAt = DateTime.UtcNow;
    }

    private async Task ShipAsync(Order order, int courierId, string? rawConsignmentId, decimal deliveryCharge)
    {
        var consignmentId = rawConsignmentId?.Trim() ?? "";
        if (consignmentId == "")
            throw new RuleViolationException("Enter the courier's consignment ID before dispatch.");
        if (deliveryCharge < 0)
            throw new RuleViolationException("The courier charge cannot be negative.");
        if (!await db.Couriers.AnyAsync(c => c.Id == courierId && c.IsActive))
            throw new RuleViolationException("Choose a courier.");
        // Consignment IDs are unique per courier across the whole platform.
        if (await db.Shipments.IgnoreQueryFilters().AnyAsync(s => s.CourierId == courierId && s.ConsignmentId == consignmentId))
            throw new RuleViolationException($"Consignment ID {consignmentId} is already used for this courier.");

        var reservations = await db.StockReservations
            .Where(r => r.OrderId == order.Id && r.Status == ReservationStatus.Active).ToListAsync();
        foreach (var r in reservations)
        {
            var v = order.Items.First(i => i.ProductVariantId == r.ProductVariantId).ProductVariant!;
            v.Reserved -= Math.Min(r.Quantity, v.Reserved);
            v.Stock -= Math.Min(r.Quantity, v.Stock);   // the held units physically leave
            r.Status = ReservationStatus.Fulfilled;
            AddStockEvent(order, v, StockEventType.Adjusted, -r.Quantity, $"Shipped with {order.OrderNumber}");
        }

        db.Shipments.Add(new Shipment
        {
            MerchantId = order.MerchantId,
            OrderId = order.Id,
            CourierId = courierId,
            ConsignmentId = consignmentId,
            Status = ShipmentStatus.Booked,
            CodAmount = order.CodAmount,
            DeliveryCharge = deliveryCharge,
        });
        order.DispatchedAt = DateTime.UtcNow;
    }

    private void AddStockEvent(Order order, ProductVariant v, StockEventType type, int change, string note) =>
        db.StockEvents.Add(new StockEvent
        {
            MerchantId = order.MerchantId, ProductVariant = v, OrderId = order.Id,
            Type = type, QuantityChange = change, StockAfter = v.Stock, Note = note, // v.Stock is already updated
        });
}

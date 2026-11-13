using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Tests.Unit;

/// <summary>Feature 5 · Strict State-Machine Order Lifecycle and stock reservation.</summary>
public class OrderLifecycleTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly Services _s;
    public OrderLifecycleTests() => _s = new Services(_t);
    public void Dispose() => _t.Dispose();

    private async Task<ProductVariant> Variant(string sku)
    {
        using var fresh = _t.NewContext();
        return await fresh.ProductVariants.AsNoTracking().FirstAsync(v => v.Sku == sku);
    }

    [Fact]
    public async Task Confirm_reserves_stock_and_cancel_releases_it()
    {
        var order = await _s.NewOrderAsync(sku: "KURTI-RED-S", qty: 3); // stock 10

        await _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Owner1);
        var v = await Variant("KURTI-RED-S");
        Assert.Equal(3, v.Reserved);
        Assert.Equal(7, v.Available);

        await _s.Lifecycle.CancelAsync(order.Id, TestDb.Owner1, "customer changed mind");
        v = await Variant("KURTI-RED-S");
        Assert.Equal(0, v.Reserved);
        Assert.Equal(10, v.Stock);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Cancelled],
            await _s.Db.OrderStatusHistory.Where(h => h.OrderId == order.Id).OrderBy(h => h.Id).Select(h => h.ToStatus).ToListAsync());
    }

    [Fact]
    public async Task A_move_outside_the_table_is_refused()
    {
        var order = await _s.NewOrderAsync();
        var e = await Assert.ThrowsAsync<RuleViolationException>(() => _s.Lifecycle.MarkDeliveredAsync(order.Id, TestDb.Owner1, isOwner: true));
        Assert.Contains("cannot move from Pending to Delivered", e.Message);
    }

    [Fact]
    public async Task Confirm_fails_when_stock_is_short_and_nothing_is_reserved()
    {
        var order = await _s.NewOrderAsync(sku: "KURTI-RED-M", qty: 3); // only 2 in stock

        await Assert.ThrowsAsync<RuleViolationException>(() => _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Owner1));
        Assert.Equal(0, (await Variant("KURTI-RED-M")).Reserved);
    }

    [Fact]
    public async Task Boundary_ordering_exactly_the_available_stock_is_allowed()
    {
        var order = await _s.NewOrderAsync(sku: "KURTI-RED-M", qty: 2); // exactly 2 in stock
        await _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Owner1);
        Assert.Equal(0, (await Variant("KURTI-RED-M")).Available);
    }

    [Fact]
    public async Task Only_the_owner_can_mark_delivered()
    {
        var order = await _s.NewOrderAsync();
        await _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Moderator1);
        await _s.Lifecycle.DispatchAsync(order.Id, 1, "SF-100", 60m, TestDb.Moderator1);

        var e = await Assert.ThrowsAsync<RuleViolationException>(() => _s.Lifecycle.MarkDeliveredAsync(order.Id, TestDb.Moderator1, isOwner: false));
        Assert.Contains("Only the shop owner", e.Message);
    }

    [Fact]
    public async Task Dispatch_needs_a_consignment_id_and_then_stock_leaves()
    {
        var order = await _s.NewOrderAsync(sku: "ABAYA-BLK", qty: 2); // stock 8
        await _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Owner1);

        await Assert.ThrowsAsync<RuleViolationException>(() => _s.Lifecycle.DispatchAsync(order.Id, 1, "  ", 60m, TestDb.Owner1));

        await _s.Lifecycle.DispatchAsync(order.Id, 1, "SF-200", 60m, TestDb.Owner1);
        var v = await Variant("ABAYA-BLK");
        Assert.Equal(6, v.Stock);
        Assert.Equal(0, v.Reserved);
        Assert.Equal(order.CodAmount, (await _s.Db.Shipments.FirstAsync(s => s.OrderId == order.Id)).CodAmount);
    }

    [Fact]
    public void Settled_is_never_offered_as_a_user_step()
    {
        Assert.DoesNotContain(OrderStatus.Settled, OrderLifecycleService.NextStepsFor(OrderStatus.Delivered, isOwner: true));
        Assert.Equal([OrderStatus.Confirmed, OrderStatus.Cancelled], OrderLifecycleService.NextStepsFor(OrderStatus.Pending, isOwner: false));
    }

    [Fact]
    public async Task Two_moderators_saving_the_last_units_at_once_only_one_wins()
    {
        // Both screens load the variant with Version 0, then both try to reserve the last 2 units.
        using var a = _t.NewContext();
        using var b = _t.NewContext();
        var va = await a.ProductVariants.FirstAsync(v => v.Sku == "KURTI-RED-M");
        var vb = await b.ProductVariants.FirstAsync(v => v.Sku == "KURTI-RED-M");

        va.Reserved += 2;
        await a.SaveChangesAsync();            // first save wins and bumps Version to 1

        vb.Reserved += 2;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync()); // still sees Version 0
        Assert.Equal(2, (await Variant("KURTI-RED-M")).Reserved);
    }
}

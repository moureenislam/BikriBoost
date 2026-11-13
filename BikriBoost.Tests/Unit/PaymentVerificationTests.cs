using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Tests.Unit;

/// <summary>Feature 4 · MFS Payment &amp; TrxID Verification. Cases ported from the old ModuleB tests.</summary>
public class PaymentVerificationTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly Services _s;
    public PaymentVerificationTests() => _s = new Services(_t);
    public void Dispose() => _t.Dispose();

    /// <summary>An order (Tk 6,530) where the shop has asked for a Tk 130 advance.</summary>
    private static async Task<Order> OrderNeedingAdvanceAsync(Services s, string phone, string userId = TestDb.Owner1)
    {
        var order = await s.NewOrderAsync(phone: phone, district: "Chattogram", sku: "SAREE-RED-SILK", qty: 2, userId: userId);
        await s.Payments.RequestAdvanceAsync(order.Id, 130m);
        return order;
    }

    private Task<Order> OrderNeedingAdvanceAsync(string phone) => OrderNeedingAdvanceAsync(_s, phone);

    [Fact]
    public async Task Asking_for_an_advance_locks_dispatch_until_the_owner_confirms_it()
    {
        var order = await OrderNeedingAdvanceAsync("01911000908");
        Assert.True(order.IsLockedForAdvance);
        Assert.Equal(130m, order.AdvanceDue);

        await _s.Lifecycle.ConfirmAsync(order.Id, TestDb.Owner1);
        var locked = await Assert.ThrowsAsync<RuleViolationException>(() =>
            _s.Lifecycle.DispatchAsync(order.Id, 1, "SF-300", 130m, TestDb.Owner1));
        Assert.Contains("advance", locked.Message);

        var claim = await _s.Payments.SubmitAsync(order.Id, PaymentProvider.Bkash, "PP12CD34EF", 130m, "01911000908");
        await _s.Payments.ConfirmAsync(claim.Id, TestDb.Owner1, isOwner: true);
        await _s.Lifecycle.DispatchAsync(order.Id, 1, "SF-300", 130m, TestDb.Owner1); // now allowed
        Assert.Equal(6400m, (await _s.Db.Shipments.FirstAsync(sh => sh.OrderId == order.Id)).CodAmount); // 6,530 − 130 advance
    }

    [Theory]
    [InlineData(0)]       // nothing
    [InlineData(-50)]     // negative
    [InlineData(6531)]    // one taka more than the Tk 6,530 total (boundary)
    public async Task The_advance_must_be_between_one_taka_and_the_order_total(int amount)
    {
        var order = await _s.NewOrderAsync(phone: "01911000909", district: "Chattogram", sku: "SAREE-RED-SILK", qty: 2);
        await Assert.ThrowsAsync<RuleViolationException>(() => _s.Payments.RequestAdvanceAsync(order.Id, amount));
    }

    [Fact]
    public async Task Boundary_the_full_order_total_can_be_asked_as_advance()
    {
        var order = await _s.NewOrderAsync(phone: "01911000910", district: "Chattogram", sku: "SAREE-RED-SILK", qty: 2);
        await _s.Payments.RequestAdvanceAsync(order.Id, 6530m);
        var saved = await _s.Db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal(6530m, saved.AdvanceDue);   // the whole amount is now due up front
        Assert.True(saved.IsLockedForAdvance);   // cash on delivery drops only once it is actually paid
    }

    [Fact]
    public async Task A_TrxID_can_be_used_only_once_on_the_whole_platform()
    {
        var a = await OrderNeedingAdvanceAsync("01911000901");
        await _s.Payments.SubmitAsync(a.Id, PaymentProvider.Bkash, "9JK4L2M8QX", 130m, "01911000901");

        _t.Merchant.MerchantId = TestDb.Shop2; // a different shop tries the same TrxID, in lower case
        var s2 = new Services(_t);
        var b = await OrderNeedingAdvanceAsync(s2, "01911000902", TestDb.Owner2);

        var e = await Assert.ThrowsAsync<RuleViolationException>(() =>
            s2.Payments.SubmitAsync(b.Id, PaymentProvider.Bkash, "9jk4l2m8qx", 130m, "01911000902"));
        Assert.Contains("already been used", e.Message);
    }

    [Fact]
    public async Task Boundary_one_poisha_short_is_rejected_and_the_exact_amount_is_accepted()
    {
        var order = await OrderNeedingAdvanceAsync("01911000903");

        var wrong = await Assert.ThrowsAsync<RuleViolationException>(() =>
            _s.Payments.SubmitAsync(order.Id, PaymentProvider.Bkash, "AB12CD34EF", 129.99m, "01911000903"));
        Assert.Contains("Wrong amount", wrong.Message);

        var claim = await _s.Payments.SubmitAsync(order.Id, PaymentProvider.Bkash, "AB12CD34EF", 130.00m, "01911000903");
        Assert.Equal(PaymentClaimStatus.Pending, claim.Status);
    }

    [Theory]
    [InlineData(PaymentProvider.Bkash, "SHORT")]        // too short
    [InlineData(PaymentProvider.Bkash, "9JK4L2M8QX1")]  // bKash is exactly 10
    [InlineData(PaymentProvider.Nagad, "71A2B3C")]      // Nagad needs at least 8
    [InlineData(PaymentProvider.Bkash, "9JK4-2M8QX")]   // symbols are not allowed
    public async Task Malformed_transaction_ids_are_rejected(PaymentProvider provider, string trxId)
    {
        var order = await OrderNeedingAdvanceAsync("01911000904");
        await Assert.ThrowsAsync<RuleViolationException>(() => _s.Payments.SubmitAsync(order.Id, provider, trxId, 130m, "01911000904"));
    }

    [Fact]
    public async Task Only_the_owner_confirms_and_then_the_order_unlocks()
    {
        var order = await OrderNeedingAdvanceAsync("01911000905");
        var claim = await _s.Payments.SubmitAsync(order.Id, PaymentProvider.Nagad, "71A2B3C4", 130m, "01911000905");

        await Assert.ThrowsAsync<RuleViolationException>(() => _s.Payments.ConfirmAsync(claim.Id, TestDb.Moderator1, isOwner: false));

        await _s.Payments.ConfirmAsync(claim.Id, TestDb.Owner1, isOwner: true);
        var saved = await _s.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(130m, saved.AdvancePaid);
        Assert.False(saved.IsLockedForAdvance);

        var twice = await Assert.ThrowsAsync<RuleViolationException>(() => _s.Payments.ConfirmAsync(claim.Id, TestDb.Owner1, isOwner: true));
        Assert.Contains("already been verified", twice.Message);
    }

    [Fact]
    public async Task No_payment_is_taken_when_no_advance_is_due_and_only_one_claim_can_wait()
    {
        var lowRisk = await _s.NewOrderAsync(phone: "01911000906");
        await Assert.ThrowsAsync<RuleViolationException>(() =>
            _s.Payments.SubmitAsync(lowRisk.Id, PaymentProvider.Bkash, "ZZ12CD34EF", 100m, "01911000906"));

        var order = await OrderNeedingAdvanceAsync("01911000907");
        await _s.Payments.SubmitAsync(order.Id, PaymentProvider.Bkash, "QQ12CD34EF", 130m, "01911000907");
        var second = await Assert.ThrowsAsync<RuleViolationException>(() =>
            _s.Payments.SubmitAsync(order.Id, PaymentProvider.Bkash, "RR12CD34EF", 130m, "01911000907"));
        Assert.Contains("already waiting", second.Message);
    }
}

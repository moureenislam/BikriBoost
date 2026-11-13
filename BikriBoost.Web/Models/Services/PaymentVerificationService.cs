using System.Text.RegularExpressions;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Feature 4 · Automated MFS Payment &amp; TrxID Verification (manual send-money path).
///   - The shop can ask for an advance (1 taka up to the order total) before dispatch;
///     the order is then locked for dispatch until the advance is confirmed.
///   - A transaction ID can be used only once per provider on the whole platform
///     (checked here, and enforced by the unique database index on Provider + TrxId).
///   - The TrxID must look right for the provider: bKash 10 letters/digits, Nagad 8–12.
///   - The amount must equal the advance still due, to the poisha.
///   - Only one claim per order can wait for review at a time.
///   - Only the Owner confirms or rejects; a confirmed advance unlocks the order for dispatch.
/// </summary>
public class PaymentVerificationService(AppDbContext db)
{
    private static readonly Dictionary<PaymentProvider, Regex> Formats = new()
    {
        [PaymentProvider.Bkash] = new Regex(@"^[A-Z0-9]{10}$"),   // e.g. 9JK4L2M8QX
        [PaymentProvider.Nagad] = new Regex(@"^[A-Z0-9]{8,12}$"), // e.g. 71A2B3C4
    };

    public static bool IsWellFormed(PaymentProvider provider, string trxId) => Formats[provider].IsMatch(trxId);

    /// <summary>
    /// Asks the customer for an advance before the parcel leaves (e.g. a new customer or an expensive order).
    /// The order stays locked for dispatch until the owner confirms the payment.
    /// </summary>
    public async Task RequestAdvanceAsync(int orderId, decimal amount)
    {
        var order = await db.Orders.Include(o => o.PaymentClaims).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new RuleViolationException("Order not found.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            throw new RuleViolationException($"An advance can only be asked for before dispatch; this order is {order.Status}.");
        if (order.PaymentClaims.Any(p => p.Status == PaymentClaimStatus.Pending))
            throw new RuleViolationException("A payment is already waiting for the owner; confirm or reject it first.");
        if (amount <= 0)
            throw new RuleViolationException("The advance must be more than Tk 0.");
        if (amount > order.Total)
            throw new RuleViolationException($"The advance cannot be more than the order total (Tk {order.Total:N0}).");
        if (amount < order.AdvancePaid)
            throw new RuleViolationException($"Tk {order.AdvancePaid:N0} is already paid; the advance cannot be lower than that.");

        order.AdvanceRequired = amount;
        order.IsLockedForAdvance = order.AdvancePaid < amount;
        await db.SaveChangesAsync();
    }

    public async Task<PaymentClaim> SubmitAsync(int orderId, PaymentProvider provider, string trxId, decimal amount, string senderPhone)
    {
        trxId = (trxId ?? "").Trim().ToUpperInvariant();
        if (!Enum.IsDefined(provider)) throw new RuleViolationException("Choose bKash or Nagad.");
        if (!IsWellFormed(provider, trxId))
            throw new RuleViolationException($"\"{trxId}\" does not look like a {provider} transaction ID.");
        var phone = PhoneNumber.Normalize(senderPhone)
            ?? throw new RuleViolationException("The sender number must be a valid Bangladeshi mobile number.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new RuleViolationException("Order not found.");
        if (order.AdvanceDue <= 0)
            throw new RuleViolationException("No advance is due on this order.");
        if (await db.PaymentClaims.IgnoreQueryFilters().AnyAsync(p => p.Provider == provider && p.TrxId == trxId))
            throw new RuleViolationException($"Transaction ID {trxId} has already been used on BikriBoost.");
        if (amount != order.AdvanceDue)
            throw new RuleViolationException($"Wrong amount: Tk {order.AdvanceDue:N2} is due, Tk {amount:N2} was entered.");
        if (await db.PaymentClaims.AnyAsync(p => p.OrderId == orderId && p.Status == PaymentClaimStatus.Pending))
            throw new RuleViolationException("A payment for this order is already waiting for the owner to confirm.");

        var claim = new PaymentClaim
        {
            MerchantId = order.MerchantId,
            OrderId = order.Id,
            Provider = provider,
            TrxId = trxId,
            Amount = amount,
            SenderPhone = phone,
            Source = PaymentClaimSource.ManualTrx,
            Status = PaymentClaimStatus.Pending,
        };
        db.PaymentClaims.Add(claim);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two people submitted the same TrxID at the same moment: the unique index stopped the second one.
            throw new RuleViolationException($"Transaction ID {trxId} has already been used on BikriBoost.");
        }
        return claim;
    }

    public async Task ConfirmAsync(int claimId, string userId, bool isOwner)
    {
        var claim = await PendingClaimForOwnerAsync(claimId, isOwner, "confirm");
        claim.Status = PaymentClaimStatus.Verified;
        claim.ReviewedByUserId = userId;
        claim.ReviewedAt = DateTime.UtcNow;

        var order = claim.Order!;
        order.AdvancePaid += claim.Amount;
        if (order.AdvancePaid >= order.AdvanceRequired) order.IsLockedForAdvance = false;
        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(int claimId, string reason, string userId, bool isOwner)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new RuleViolationException("Give a reason for rejecting the payment.");
        var claim = await PendingClaimForOwnerAsync(claimId, isOwner, "reject");
        claim.Status = PaymentClaimStatus.Rejected;
        claim.RejectReason = reason.Trim();
        claim.ReviewedByUserId = userId;
        claim.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<PaymentClaim> PendingClaimForOwnerAsync(int claimId, bool isOwner, string verb)
    {
        if (!isOwner) throw new RuleViolationException($"Only the shop owner, who receives the money, can {verb} a payment.");
        var claim = await db.PaymentClaims.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == claimId)
            ?? throw new RuleViolationException("Payment not found.");
        if (claim.Status != PaymentClaimStatus.Pending)
            throw new RuleViolationException($"This payment has already been {claim.Status.ToString().ToLowerInvariant()}.");
        return claim;
    }
}

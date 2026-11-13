using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static BikriBoost.Web.Models.Data.ReferenceData.RoleNames;

namespace BikriBoost.Web.Controllers;

/// <summary>Feature 4 · ask for an advance, submit a bKash/Nagad TrxID, and the owner's confirmation queue.</summary>
public class PaymentsController(AppDbContext db, PaymentVerificationService payments) : AppController
{
    [HttpPost, Authorize(Roles = $"{Owner},{Moderator}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAdvance(int orderId, decimal amount)
    {
        await TryRunAsync(() => payments.RequestAdvanceAsync(orderId, amount),
            $"Advance of Tk {amount:N0} requested. The order cannot be dispatched until it is paid and confirmed.");
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    [HttpPost, Authorize(Roles = $"{Owner},{Moderator}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int orderId, PaymentProvider provider, string trxId, decimal amount, string senderPhone)
    {
        await TryRunAsync(() => payments.SubmitAsync(orderId, provider, trxId, amount, senderPhone),
            "TrxID submitted. It is waiting for the owner to confirm the money arrived.");
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    public async Task<IActionResult> Review()
    {
        var claims = await db.PaymentClaims.Include(p => p.Order)
            .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync();
        return View(new PaymentReviewViewModel
        {
            Pending = claims.Where(c => c.Status == PaymentClaimStatus.Pending).ToList(),
            Recent = claims.Where(c => c.Status != PaymentClaimStatus.Pending).Take(30).ToList(),
            IsOwner = IsOwner,
        });
    }

    [HttpPost, Authorize(Roles = Owner), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        await TryRunAsync(() => payments.ConfirmAsync(id, UserId, IsOwner), "Payment confirmed; the advance now counts.");
        return RedirectToAction(nameof(Review));
    }

    [HttpPost, Authorize(Roles = Owner), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        await TryRunAsync(() => payments.RejectAsync(id, reason, UserId, IsOwner), "Payment rejected.");
        return RedirectToAction(nameof(Review));
    }
}

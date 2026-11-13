using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BikriBoost.Web.Controllers;

/// <summary>
/// Base for signed-in pages. Controllers stay thin: check the role, call ONE Model-layer service,
/// then redirect or show a view. Broken business rules come back as RuleViolationException
/// and are shown to the user as a red message; nothing is saved.
/// </summary>
[Authorize]
public abstract class AppController : Controller
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    protected bool IsOwner => User.IsInRole(ReferenceData.RoleNames.Owner);

    /// <summary>Runs a service call, turning rule violations and stock clashes into messages.</summary>
    protected async Task<bool> TryRunAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            TempData["Success"] = successMessage;
            return true;
        }
        catch (RuleViolationException e)
        {
            TempData["Error"] = e.Message;
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Someone else changed the same stock at that moment. Nothing was saved; please try again.";
        }
        return false;
    }
}

using System.Data;
using System.Security.Claims;
using BikriBoost.Web.Models.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

public sealed class BusinessRuleException(string message) : Exception(message);
public static class Rules
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new BusinessRuleException(message);
    }
    public static string Text(string? value, int maximum, string label)
    {
        var text = value?.Trim() ?? "";
        Require(text.Length > 0 && text.Length <= maximum, $"{label} must contain 1–{maximum} characters.");
        return text;
    }
}
// Every mutation is atomic, including related counters and state transitions.
public sealed class MarketingUnitOfWork(AppDbContext db)
{
    public async Task<T> Run<T>(Func<Task<T>> operation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await operation();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }
}
public sealed class MarketingInputValidation : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid && context.Controller is MarketingController)
            context.Result = new BadRequestObjectResult(new
            {
                error = "One or more fields are invalid. Check numbers, dates and required values."
            });
    }
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
public sealed class MarketingErrors : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is BusinessRuleException or DbUpdateConcurrencyException)
        {
            var message = context.Exception is BusinessRuleException ? context.Exception.Message : "This record changed. Refresh and try again.";
            context.Result = new BadRequestObjectResult(new
            {
                error = message
            });
            context.ExceptionHandled = true;
        }
        return Task.CompletedTask;
    }
}
public abstract class MarketingController : Controller
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
public static class BusinessTime
{
    // Forms display Bangladesh time explicitly; storage and background jobs use UTC.
    public static DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Unspecified).AddHours(-6);
    public static DateTime Local(DateTime utc) => utc.AddHours(6);
}

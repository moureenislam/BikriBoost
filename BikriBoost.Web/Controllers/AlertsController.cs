using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class AlertsController(AppDbContext db, FatigueDetector service) : MarketingController
{
    public async Task<IActionResult> Index() => View(new AlertDashboard(await db.FatigueAlerts.OrderByDescending(a => a.WeekStart).ToListAsync(), await db.CreativeWeeklyMetrics.OrderByDescending(m => m.WeekStart).ThenBy(m => m.CreativeKey).ToListAsync(), await db.Campaigns.ToListAsync(), await db.Merchants.SingleAsync(m => m.Id == db.CurrentMerchantId)));
    [HttpPost]
    public async Task<IActionResult> Metric(string creative, int? campaignId, DateOnly week, decimal spend, int impressions, int clicks, int orders)
    {
        await service.SaveMetric(creative, campaignId, week, spend, impressions, clicks, orders);
        await service.Evaluate();
        return Ok();
    }
    [HttpPost] public async Task<IActionResult> Evaluate() => Ok(new { created = await service.Evaluate() });
    [HttpPost]
    public async Task<IActionResult> Status(int id, FatigueAlertStatus status)
    {
        await service.SetStatus(id, status);
        return Ok();
    }
    [HttpPost, Authorize(Roles = "Owner")]
    public async Task<IActionResult> Settings(decimal costRise, decimal conversionDrop)
    {
        await service.Settings(costRise, conversionDrop);
        await service.Evaluate();
        return Ok();
    }
}

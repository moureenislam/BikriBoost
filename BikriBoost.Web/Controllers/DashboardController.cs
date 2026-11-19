using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class DashboardController(AppDbContext db, RoiCalculator calculator) : MarketingController
{
    private static (DateOnly, DateOnly) Range(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(BusinessTime.Local(DateTime.UtcNow));
        return (from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
    }
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? campaignId)
    {
        var (f, t) = Range(from, to);
        return View(new DashboardModel(await calculator.Report(f, t, campaignId), await db.Campaigns.ToListAsync(), await db.AdSpends.Where(a => a.Date >= f && a.Date <= t && (!campaignId.HasValue || a.CampaignId == campaignId)).OrderByDescending(a => a.Date).ToListAsync(), campaignId));
    }
    [HttpPost]
    public async Task<IActionResult> Spend(DateOnly date, int? campaignId, SocialPlatform platform, string? creative, decimal amount)
    {
        await calculator.AddSpend(date, campaignId, platform, creative, amount);
        return Ok();
    }
    [HttpPost, RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? upload)
    {
        Rules.Require(upload != null && upload.Length is > 0 and <= 1024 * 1024, "Choose a CSV up to 1 MB.");
        using var stream = upload!.OpenReadStream();
        return Ok(new
        {
            inserted = await calculator.Import(stream)
        });
    }
    public async Task<IActionResult> Export(string format, DateOnly? from, DateOnly? to, int? campaignId)
    {
        var (f, t) = Range(from, to);
        var r = await calculator.Report(f, t, campaignId);
        return format == "pdf" ? File(RoiCalculator.Pdf(r), "application/pdf", "bikriboost-roi.pdf") : File(RoiCalculator.Csv(r), "text/csv; charset=utf-8", "bikriboost-roi.csv");
    }
    public IActionResult Template() => File(System.Text.Encoding.UTF8.GetBytes("Date,CampaignId,Platform,CreativeKey,Amount\r\n"), "text/csv", "ad-spend-template.csv");
}

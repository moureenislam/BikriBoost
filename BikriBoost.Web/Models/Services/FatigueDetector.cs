using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Models.Services;

public sealed record FatigueSignal(decimal BaselineCost, decimal CurrentCost, decimal BaselineConversion, decimal CurrentConversion);
public sealed class FatigueDetector(AppDbContext db, MarketingUnitOfWork work, TimeProvider time)
{
    public static DateOnly Monday(DateOnly day) => day.AddDays(-((int)day.DayOfWeek + 6) % 7);
    public static FatigueSignal? Detect(IEnumerable<CreativeWeeklyMetric> source, DateOnly currentWeek, decimal costRise, decimal conversionDrop)
    {
        var weeks = source.Where(w => w.WeekStart < currentWeek).OrderByDescending(w => w.WeekStart).Take(4).OrderBy(w => w.WeekStart).ToArray();
        if (weeks.Length != 4 || weeks[3].WeekStart != currentWeek.AddDays(-7))
            return null;
        for (int i = 1; i < 4; i++)
        if (weeks[i].WeekStart != weeks[i - 1].WeekStart.AddDays(7))
            return null;
        var orders = weeks.Take(2).Sum(w => w.Orders);
        var clicks = weeks.Take(2).Sum(w => w.Clicks);
        var spend = weeks.Take(2).Sum(w => w.Spend);
        if (orders <= 0 || clicks <= 0 || spend <= 0)
            return null;
        var baselineCost = spend / orders;
        var baselineConversion = orders / (decimal)clicks;
        bool Burned(CreativeWeeklyMetric w) => w.Clicks > 0 && w.Spend > 0 && (w.Orders == 0 || w.Spend / w.Orders >= baselineCost * (1 + costRise / 100m)) && w.Orders / (decimal)w.Clicks <= baselineConversion * (1 - conversionDrop / 100m);
        if (!Burned(weeks[2]) || !Burned(weeks[3]))
            return null;
        // -1 represents undefined cost/order when there are no orders; the UI renders “No orders”.
        return new(baselineCost, weeks[3].Orders == 0 ? -1 : weeks[3].Spend / weeks[3].Orders, baselineConversion, weeks[3].Orders / (decimal)weeks[3].Clicks);
    }
    public Task<int> SaveMetric(string creative, int? campaignId, DateOnly week, decimal spend, int impressions, int clicks, int orders) => work.Run(async () =>
    {
        creative = Rules.Text(creative, 100, "Creative key").ToLowerInvariant();
        var current = Monday(DateOnly.FromDateTime(BusinessTime.Local(time.GetUtcNow().UtcDateTime)));
        Rules.Require(week != default && week == Monday(week) && week < current, "Use the Monday of a completed week.");
        Rules.Require(spend >= 0 && spend <= 1_000_000_000 && impressions >= 0 && clicks >= 0 && orders >= 0 && orders <= clicks && clicks <= impressions, "Use non-negative values with orders ≤ clicks ≤ impressions.");
        Rules.Require(!campaignId.HasValue || await db.Campaigns.AnyAsync(c => c.Id == campaignId), "Campaign not found.");
        Rules.Require(!await db.CreativeWeeklyMetrics.AnyAsync(m => m.CreativeKey == creative && m.CampaignId != campaignId), "A creative key must stay in the same campaign. Use a new key for another campaign.");
        var m = await db.CreativeWeeklyMetrics.SingleOrDefaultAsync(m => m.CreativeKey == creative && m.WeekStart == week) ?? new CreativeWeeklyMetric { CreativeKey = creative, WeekStart = week };
        if (m.Id == 0)
            db.CreativeWeeklyMetrics.Add(m);
        m.CampaignId = campaignId;
        m.Spend = spend;
        m.Impressions = impressions;
        m.Clicks = clicks;
        m.Orders = orders;
        return m.Id;
    });
    public Task<int> Evaluate() => work.Run(async () =>
    {
        var merchant = await db.Merchants.SingleAsync(m => m.Id == db.CurrentMerchantId);
        var current = Monday(DateOnly.FromDateTime(BusinessTime.Local(time.GetUtcNow().UtcDateTime)));
        var metrics = await db.CreativeWeeklyMetrics.Where(m => m.WeekStart >= current.AddDays(-28) && m.WeekStart < current).ToListAsync();
        var existing = await db.FatigueAlerts.Where(a => a.WeekStart == current.AddDays(-7)).ToListAsync();
        int created = 0;
        foreach (var group in metrics.GroupBy(m => m.CreativeKey))
        {
            var signal = Detect(group, current, merchant.FatigueCostRisePercent, merchant.FatigueConversionDropPercent);
            var alert = existing.SingleOrDefault(a => a.CreativeKey == group.Key);
            if (signal == null)
            {
                if (alert != null)
                    alert.Status = FatigueAlertStatus.Resolved;
                continue;
            }
            if (alert == null)
            {
                alert = new FatigueAlert { CreativeKey = group.Key, WeekStart = current.AddDays(-7) };
                db.FatigueAlerts.Add(alert);
                created++;
            }
            alert.BaselineCostPerOrder = signal.BaselineCost;
            alert.CurrentCostPerOrder = signal.CurrentCost;
            alert.BaselineConversionRate = signal.BaselineConversion;
            alert.CurrentConversionRate = signal.CurrentConversion;
        }
        return created;
    });
    public Task<int> SetStatus(int id, FatigueAlertStatus status) => work.Run(async () => { Rules.Require(status is FatigueAlertStatus.Acknowledged or FatigueAlertStatus.Resolved, "Choose Acknowledged or Resolved."); var a = await db.FatigueAlerts.SingleOrDefaultAsync(a => a.Id == id) ?? throw new BusinessRuleException("Alert not found."); a.Status = status; return id; });
    public Task<int> Settings(decimal costRise, decimal conversionDrop) => work.Run(async () => { Rules.Require(costRise is > 0 and <= 500 && conversionDrop is > 0 and <= 100, "Cost rise must be 0–500% and conversion drop 0–100%, both greater than zero."); var m = await db.Merchants.SingleAsync(m => m.Id == db.CurrentMerchantId); m.FatigueCostRisePercent = costRise; m.FatigueConversionDropPercent = conversionDrop; return m.Id; });
}

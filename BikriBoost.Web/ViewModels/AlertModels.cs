using BikriBoost.Web.Models.Entities;
namespace BikriBoost.Web.ViewModels;

public sealed record AlertDashboard(List<FatigueAlert> Alerts, List<CreativeWeeklyMetric> Metrics, List<Campaign> Campaigns, Merchant Merchant);

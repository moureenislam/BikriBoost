using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
namespace BikriBoost.Web.ViewModels;

public sealed record DashboardModel(RoiReport Report, List<Campaign> Campaigns, List<AdSpend> Spends, int? CampaignId);

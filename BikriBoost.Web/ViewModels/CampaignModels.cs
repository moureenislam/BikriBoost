using BikriBoost.Web.Models.Entities;
namespace BikriBoost.Web.ViewModels;

public sealed record CampaignIndex(List<Campaign> Campaigns);
public sealed record CampaignDetail(Campaign Campaign, List<Attribution> Attributions, List<Order> PendingOrders);
public sealed record CampaignLanding(string Campaign, string? Creative);

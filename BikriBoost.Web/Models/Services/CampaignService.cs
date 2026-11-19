using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Models.Services;

public sealed class CampaignService(AppDbContext db, MarketingUnitOfWork work, TimeProvider time)
{
    public Task<int> Create(string name, decimal? budget, DateTime? starts, DateTime? ends) => work.Run(async () =>
    {
        Rules.Require(budget == null || budget >= 0, "Budget cannot be negative.");
        Rules.Require(starts == null || ends == null || ends >= starts, "End date must follow the start date.");
        var c = new Campaign { Name = Rules.Text(name, 150, "Campaign name"), Budget = budget, StartsAt = starts.HasValue ? BusinessTime.ToUtc(starts.Value) : null, EndsAt = ends.HasValue ? BusinessTime.ToUtc(ends.Value) : null };
        db.Campaigns.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    });
    public Task<int> Coupon(int campaignId, string code, CouponType type, decimal value, decimal? minimum, DateTime? expiry, int? totalLimit, int? phoneLimit) => work.Run(async () =>
    {
        Rules.Require(await db.Campaigns.AnyAsync(c => c.Id == campaignId), "Campaign not found.");
        code = Rules.Text(code, 32, "Code").ToUpperInvariant();
        Rules.Require(Regex.IsMatch(code, "^[A-Z0-9_-]+$"), "Coupon codes may contain letters, numbers, underscores and hyphens.");
        Rules.Require(!await db.Coupons.AnyAsync(c => c.Code == code), "This coupon code already exists.");
        ValidateCouponDefinition(type, value, minimum, totalLimit, phoneLimit);
        Rules.Require(expiry == null || BusinessTime.ToUtc(expiry.Value) > time.GetUtcNow().UtcDateTime, "Coupon expiry must be in the future.");
        var c = new Coupon { CampaignId = campaignId, Code = code, Type = type, Value = value, MinOrderAmount = minimum, ExpiresAt = expiry.HasValue ? BusinessTime.ToUtc(expiry.Value) : null, TotalLimit = totalLimit, PerPhoneLimit = phoneLimit };
        db.Coupons.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    });
    public static void ValidateCouponDefinition(CouponType type, decimal value, decimal? minimum, int? totalLimit, int? phoneLimit)
    {
        Rules.Require(Enum.IsDefined(type) && value > 0 && (type != CouponType.Percent || value <= 100), "Discount must be positive; percentage discounts cannot exceed 100%.");
        Rules.Require(minimum == null || minimum >= 0, "Minimum order value cannot be negative.");
        Rules.Require((totalLimit == null || totalLimit > 0) && (phoneLimit == null || phoneLimit > 0), "Usage limits must be positive or blank for unlimited.");
    }
    public static decimal Discount(Coupon coupon, decimal subtotal, DateTime utc, int usesForPhone)
    {
        Rules.Require(coupon.IsActive && (!coupon.ExpiresAt.HasValue || coupon.ExpiresAt > utc), "Coupon is inactive or expired.");
        Rules.Require(subtotal >= 0 && subtotal >= (coupon.MinOrderAmount ?? 0), "Order does not meet the coupon minimum.");
        Rules.Require(coupon.TotalLimit == null || coupon.TimesUsed < coupon.TotalLimit, "Coupon usage limit reached.");
        Rules.Require(coupon.PerPhoneLimit == null || usesForPhone < coupon.PerPhoneLimit, "This phone has reached its coupon limit.");
        return decimal.Round(Math.Min(subtotal, coupon.Type == CouponType.Percent ? subtotal * coupon.Value / 100m : coupon.Value), 2, MidpointRounding.AwayFromZero);
    }
    public static string Phone(string value)
    {
        var p = Regex.Replace(value, "[^0-9]", "");
        if (p.StartsWith("880"))
            p = p[2..];
        Rules.Require(Regex.IsMatch(p, "^01[3-9][0-9]{8}$"), "Order needs a valid Bangladeshi mobile number.");
        return p;
    }
    public Task<int> CreateLink(int campaignId, string source, string medium, string creative) => work.Run(async () =>
    {
        var c = await db.Campaigns.SingleOrDefaultAsync(c => c.Id == campaignId) ?? throw new BusinessRuleException("Campaign not found.");
        var link = new TrackedLink { CampaignId = campaignId, Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(), DestinationUrl = "/Campaigns/Landing", UtmSource = Rules.Text(source, 100, "Source"), UtmMedium = Rules.Text(medium, 100, "Medium"), UtmCampaign = c.Name[..Math.Min(100, c.Name.Length)], UtmContent = Rules.Text(creative, 100, "Creative key") };
        db.TrackedLinks.Add(link);
        await db.SaveChangesAsync();
        return link.Id;
    });
    public Task<int> Attribute(int orderId, string? couponCode, int? linkId) => work.Run(async () =>
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId) ?? throw new BusinessRuleException("Order not found.");
        Rules.Require(order.Status == OrderStatus.Pending, "Only pending orders can receive a promotion or attribution.");
        Rules.Require(!await db.Attributions.AnyAsync(a => a.OrderId == orderId) && order.CouponId == null, "This order already has attribution. It cannot be redeemed again.");
        Coupon? coupon = null;
        TrackedLink? link = null;
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var code = couponCode.Trim().ToUpperInvariant();
            coupon = await db.Coupons.Include(c => c.Campaign).SingleOrDefaultAsync(c => c.Code == code) ?? throw new BusinessRuleException("Coupon not found.");
            var now = time.GetUtcNow().UtcDateTime;
            Rules.Require(coupon.Campaign == null || ((!coupon.Campaign.StartsAt.HasValue || coupon.Campaign.StartsAt <= now) && (!coupon.Campaign.EndsAt.HasValue || coupon.Campaign.EndsAt > now)), "Coupon campaign has not started or has ended.");
            Rules.Require(order.Discount == 0, "This order already has a discount; promotions cannot be stacked.");
            var phone = Phone(order.ShippingPhone);
            var phones = await db.Orders.Where(o => o.CouponId == coupon.Id).Select(o => o.ShippingPhone).ToListAsync();
            var uses = phones.Count(p => Phone(p) == phone);
            order.Discount = Discount(coupon, order.Subtotal, now, uses);
            order.Total = order.Subtotal - order.Discount + order.DeliveryCharge;
            order.CouponId = coupon.Id;
            coupon.TimesUsed++;
        }
        if (linkId.HasValue)
            link = await db.TrackedLinks.SingleOrDefaultAsync(l => l.Id == linkId) ?? throw new BusinessRuleException("Tracked link not found in your workspace.");
        Rules.Require(coupon != null || link != null, "Enter a coupon or select a tracked link.");
        Rules.Require(coupon == null || link == null || coupon.CampaignId == link.CampaignId, "Coupon and link must belong to the same campaign.");
        db.Attributions.Add(new Attribution { OrderId = orderId, CouponId = coupon?.Id, TrackedLinkId = link?.Id, CampaignId = coupon?.CampaignId ?? link?.CampaignId, Source = coupon != null ? AttributionSource.Coupon : AttributionSource.Utm });
        return orderId;
    });
    public Task<int> ToggleCoupon(int id) => work.Run(async () => { var c = await db.Coupons.SingleOrDefaultAsync(c => c.Id == id) ?? throw new BusinessRuleException("Coupon not found."); c.IsActive = !c.IsActive; return id; });
}

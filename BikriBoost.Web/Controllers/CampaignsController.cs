using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class CampaignsController(AppDbContext db, CampaignService service, IDataProtectionProvider protection) : MarketingController
{
    private IDataProtector Protector => protection.CreateProtector("BikriBoost.Attribution.v1");
    public async Task<IActionResult> Index() => View(new CampaignIndex(await db.Campaigns.Include(c => c.Coupons).Include(c => c.TrackedLinks).OrderByDescending(c => c.CreatedAt).ToListAsync()));
    public async Task<IActionResult> Details(int id)
    {
        var c = await db.Campaigns.Include(c => c.Coupons).Include(c => c.TrackedLinks).SingleOrDefaultAsync(c => c.Id == id);
        if (c == null)
            return NotFound();
        return View(new CampaignDetail(c, await db.Attributions.Include(a => a.Order).Where(a => a.CampaignId == id).ToListAsync(), await db.Orders.Where(o => o.Status == OrderStatus.Pending && o.CouponId == null && !db.Attributions.Any(a => a.OrderId == o.Id)).ToListAsync()));
    }
    [HttpPost] public async Task<IActionResult> Create(string name, decimal? budget, DateTime? starts, DateTime? ends) => RedirectToAction("Details", new { id = await service.Create(name, budget, starts, ends) });
    [HttpPost]
    public async Task<IActionResult> Coupon(int campaignId, string code, CouponType type, decimal value, decimal? minimum, DateTime? expiry, int? totalLimit, int? phoneLimit)
    {
        await service.Coupon(campaignId, code, type, value, minimum, expiry, totalLimit, phoneLimit);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Link(int campaignId, string source, string medium, string creative)
    {
        await service.CreateLink(campaignId, source, medium, creative);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Attribute(int orderId, string? couponCode, int? linkId)
    {
        if (linkId == null && Request.Cookies.TryGetValue("bb_attribution", out var cookie))
            try
            {
                var value = Protector.Unprotect(cookie).Split(':');
                if (value.Length == 3 && int.Parse(value[0]) == db.CurrentMerchantId && long.Parse(value[2]) > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    linkId = int.Parse(value[1]);
            }
            catch (System.Security.Cryptography.CryptographicException) { }
            catch (FormatException) { }
        await service.Attribute(orderId, couponCode, linkId);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        await service.ToggleCoupon(id);
        return Ok();
    }
    [AllowAnonymous, HttpGet("/r/{code}")]
    public async Task<IActionResult> RedirectLink(string code)
    {
        var link = await PublicLink(code);
        if (link == null)
            return NotFound();
        await db.TrackedLinks.IgnoreQueryFilters().Where(l => l.Id == link.Id && l.MerchantId == link.MerchantId).ExecuteUpdateAsync(s => s.SetProperty(l => l.Clicks, l => l.Clicks + 1));
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        Response.Cookies.Append("bb_attribution", Protector.Protect($"{link.MerchantId}:{link.Id}:{expires.ToUnixTimeSeconds()}"), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps, Expires = expires, IsEssential = true });
        var url = QueryHelpers.AddQueryString("/Campaigns/Landing", new Dictionary<string, string?> { { "code", link.Code }, { "utm_source", link.UtmSource }, { "utm_medium", link.UtmMedium }, { "utm_campaign", link.UtmCampaign }, { "utm_content", link.UtmContent } });
        return LocalRedirect(url);
    }
    private async Task<TrackedLink?> PublicLink(string code)
    {
        if (code.Length != 24)
            return null;
        var l = await db.TrackedLinks.IgnoreQueryFilters().SingleOrDefaultAsync(l => l.Code == code);
        if (l == null || !await db.Merchants.AnyAsync(m => m.Id == l.MerchantId && m.IsActive))
            return null;
        return l;
    }
    [AllowAnonymous]
    public async Task<IActionResult> Landing(string code)
    {
        var l = await PublicLink(code);
        if (l == null)
            return NotFound();
        return View(new CampaignLanding(l.UtmCampaign ?? "Campaign", l.UtmContent));
    }
}

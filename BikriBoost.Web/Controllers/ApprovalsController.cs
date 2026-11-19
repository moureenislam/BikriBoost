using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class ApprovalsController(AppDbContext db, ApprovalService service) : MarketingController
{
    public async Task<IActionResult> Index() => View(new ApprovalIndex(await db.Posts.Include(p => p.Annotations).OrderByDescending(p => p.CreatedAt).ToListAsync(), await Assets(), await db.ProductVariants.ToListAsync()));
    public async Task<IActionResult> Review(int id) => View(new ApprovalReview(await service.Get(id), await Assets(), await db.ProductVariants.ToListAsync()));
    private Task<List<Asset>> Assets() => db.Assets.Where(a => a.CheckResult == BrandCheckResult.Pass && (a.Type == AssetType.Image || a.Type == AssetType.Logo)).ToListAsync();
    [HttpPost] public async Task<IActionResult> Save(int? id, string caption, int? assetId, int[] variantIds) => RedirectToAction("Review", new { id = await service.SaveDraft(id, caption, assetId, variantIds, UserId) });
    [HttpPost]
    public async Task<IActionResult> Submit(int id)
    {
        await service.Submit(id, UserId);
        return Ok();
    }
    [HttpPost, Authorize(Roles = "Owner")]
    public async Task<IActionResult> Decide(int id, bool approve, string? comment)
    {
        await service.Review(id, approve, comment, UserId);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Annotate(int postId, int mediaId, decimal x, decimal y, string comment)
    {
        await service.Annotate(postId, mediaId, x, y, comment, UserId);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Resolve(int id)
    {
        await service.Resolve(id);
        return Ok();
    }
}

using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class AssetsController(AppDbContext db, AssetService service) : MarketingController
{
    public async Task<IActionResult> Index() => View(new AssetLibrary(await db.Assets.OrderByDescending(a => a.CreatedAt).ToListAsync(), await db.BrandKits.SingleOrDefaultAsync(k => k.IsActive)));
    [HttpPost, RequestSizeLimit(9 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? upload, string name, AssetType type, int? previousId)
    {
        Rules.Require(upload != null, "Choose a file.");
        await service.Upload(upload!, name, type, previousId, UserId);
        return Ok();
    }
    public async Task<IActionResult> File(int id)
    {
        var a = await db.Assets.SingleOrDefaultAsync(a => a.Id == id);
        if (a == null || !System.IO.File.Exists(service.PathFor(a)))
            return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return a.Type == AssetType.Font ? PhysicalFile(service.PathFor(a), a.ContentType, a.Name + Path.GetExtension(a.FilePath)) : PhysicalFile(service.PathFor(a), a.ContentType);
    }
    [HttpPost, Authorize(Roles = "Owner")]
    public async Task<IActionResult> SaveKit(string name, string palette, string? primaryFont, string? secondaryFont, int minWidth, int minHeight, decimal tolerance, int? logoId)
    {
        await service.SaveKit(name, palette, primaryFont, secondaryFont, minWidth, minHeight, tolerance, logoId);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Recheck(int id)
    {
        await service.Recheck(id);
        return Ok();
    }
}

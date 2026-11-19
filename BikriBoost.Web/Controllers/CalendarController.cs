using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Controllers;

[Authorize(Roles = "Owner,Marketer")]
public class CalendarController(AppDbContext db, SchedulingService service) : MarketingController
{
    public async Task<IActionResult> Index() => View(new CalendarModel(await db.ScheduledPosts.Include(s => s.Post).OrderBy(s => s.ScheduledFor).ToListAsync(), await db.Posts.Where(p => p.Status == PostStatus.Approved || p.Status == PostStatus.Held || p.Status == PostStatus.Scheduled).ToListAsync()));
    public async Task<IActionResult> Feed() => Json((await db.ScheduledPosts.Include(s => s.Post).Where(s => s.Status != ScheduledPostStatus.Cancelled).ToListAsync()).Select(s => new { id = s.Id, postId = s.PostId, title = s.Post!.Caption, at = BusinessTime.Local(s.ScheduledFor).ToString("yyyy-MM-ddTHH:mm"), status = s.Status.ToString() }));
    [HttpPost]
    public async Task<IActionResult> Schedule(int postId, DateTime localTime)
    {
        await service.Schedule(postId, BusinessTime.ToUtc(localTime));
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        await service.Cancel(id);
        return Ok();
    }
    [HttpPost]
    public async Task<IActionResult> RunDue()
    {
        return Ok(new
        {
            published = await service.PublishDue()
        });
    }
}

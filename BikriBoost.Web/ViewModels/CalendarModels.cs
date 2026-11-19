using BikriBoost.Web.Models.Entities;
namespace BikriBoost.Web.ViewModels;

public sealed record CalendarModel(List<ScheduledPost> Schedules, List<Post> Posts);

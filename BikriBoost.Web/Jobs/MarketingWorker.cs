using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;
namespace BikriBoost.Web.Jobs;

public sealed class JobMerchant(int id) : ICurrentMerchant
{
    public int? MerchantId => id;
}
// Durable schedules live in the database. A restart catches up due schedules on the next tick.
public sealed class MarketingWorker(IServiceScopeFactory scopes, ILogger<MarketingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var root = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await root.Merchants.Where(m => m.IsActive).Select(m => m.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    await using var db = new AppDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>(), new JobMerchant(id));
                    var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
                    await new SchedulingService(db, new MarketingUnitOfWork(db), scope.ServiceProvider.GetRequiredService<IPublisher>(), clock).PublishDue();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Marketing background tick failed; persisted work will retry."); }
        }
    }
}

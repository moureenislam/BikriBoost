using System.Net;
using BikriBoost.Web.Jobs;
using BikriBoost.Web.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Tests.Integration;

public class TeamIntegrationTests
{
    [Theory]
    [InlineData("/Orders")]
    [InlineData("/Orders/Capture")]
    [InlineData("/Payments/Review")]
    public async Task ExistingTeamPagesRemainAvailableWithMarketingNavigation(string path)
    {
        using var factory = new MarketingFactory();
        await factory.Seed();
        using var client = factory.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Post approvals", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void MigrationSnapshotMatchesTheCombinedTeamModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=localhost;Database=model_check;User=root;", new MariaDbServerVersion(new Version(10, 4, 32)))
            .Options;
        using var db = new AppDbContext(options, new JobMerchant(1));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

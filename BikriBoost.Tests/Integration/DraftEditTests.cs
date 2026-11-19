using BikriBoost.Web.Models.Services;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Tests.Integration;

public class DraftEditTests
{
    [Fact]
    public async Task RevisingCaptionPreservesSkuTagsWithoutDuplicates()
    {
        using var factory = new MarketingFactory();
        await factory.Seed();
        await using var db = factory.Db();
        var service = new ApprovalService(db, new MarketingUnitOfWork(db), TimeProvider.System);
        var id = await service.SaveDraft(null, "Original", null, [1], "test-user");
        await service.SaveDraft(id, "Revised", null, [1], "test-user");
        Assert.Equal("Revised", (await db.Posts.SingleAsync()).Caption);
        Assert.Single(await db.PostTaggedVariants.ToListAsync());
    }
}

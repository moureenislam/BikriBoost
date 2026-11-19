using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
namespace BikriBoost.Tests.Unit;

public class Feature19Tests
{
    [Fact] public void UnapprovedPostsAreBlocked() => Assert.NotNull(SchedulingService.BlockReason(new Post()));
    [Theory]
    [InlineData(2, 1, 1, true)]
    [InlineData(3, 1, 1, false)]
    public void AvailableStockMustExceedSafetyThreshold(int stock, int reserved, int threshold, bool blocked)
    {
        var p = new Post { ApprovalRequests = [new() { Status = ApprovalStatus.Approved }], TaggedVariants = [new() { ProductVariant = new() { Stock = stock, Reserved = reserved, SafetyThreshold = threshold } }] };
        Assert.Equal(blocked, SchedulingService.BlockReason(p) != null);
    }
    [Fact] public async Task PublisherIdentifiesSimulation() => Assert.StartsWith("simulated-", await new SimulatedPublisher().Publish(new Post { Id = 42 }, SocialPlatform.Facebook));
}

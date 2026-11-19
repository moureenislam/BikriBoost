using BikriBoost.Web.Models.Services;
namespace BikriBoost.Tests.Unit;

public class Feature18Tests
{
    [Theory][InlineData(0, 0)][InlineData(100, 100)][InlineData(50, 25)] public void MarkersAcceptImageBoundaries(decimal x, decimal y) => ApprovalService.ValidateCoordinates(x, y);
    [Theory][InlineData(-1, 50)][InlineData(50, 101)] public void MarkersRejectOutsideImage(decimal x, decimal y) => Assert.Throws<BusinessRuleException>(() => ApprovalService.ValidateCoordinates(x, y));
}

using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
namespace BikriBoost.Tests.Unit;

public class Feature23Tests
{
    private static readonly DateOnly Current = new(2026, 11, 16);
    private static List<CreativeWeeklyMetric> Weeks() => Enumerable.Range(0, 4).Select(i => new CreativeWeeklyMetric { WeekStart = Current.AddDays(-28 + 7 * i), Spend = i < 2 ? 1000 : 1200, Clicks = 100, Orders = i < 2 ? 10 : 8 }).ToList();
    [Fact] public void TwoBadWeeksTriggerAtThreshold() => Assert.NotNull(FatigueDetector.Detect(Weeks(), Current, 20, 20));
    [Fact]
    public void OneBadWeekDoesNotTrigger()
    {
        var w = Weeks();
        w[2].Orders = 10;
        w[2].Spend = 1000;
        Assert.Null(FatigueDetector.Detect(w, Current, 20, 20));
    }
    [Fact]
    public void MissingWeekDoesNotTrigger()
    {
        var w = Weeks();
        w[1].WeekStart = w[1].WeekStart.AddDays(-7);
        Assert.Null(FatigueDetector.Detect(w, Current, 20, 20));
    }
    [Fact]
    public void ZeroOrdersDoesNotDivideByZero()
    {
        var w = Weeks();
        w[3].Orders = 0;
        Assert.Equal(-1, FatigueDetector.Detect(w, Current, 20, 20)!.CurrentCost);
    }
    [Fact]
    public void CurrentPartialWeekIsExcluded()
    {
        var w = Weeks();
        w[3].WeekStart = Current;
        Assert.Null(FatigueDetector.Detect(w, Current, 20, 20));
    }
    [Fact]
    public void EmptyBaselineDoesNotInventSignal()
    {
        var w = Weeks();
        w[0].Orders = w[1].Orders = 0;
        Assert.Null(FatigueDetector.Detect(w, Current, 20, 20));
    }
}

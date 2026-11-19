using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
namespace BikriBoost.Tests.Unit;

public class Feature22Tests
{
    [Fact]
    public void OnlySettledRevenueCountsAndReturnLossIsNotDoubled()
    {
        var date = new DateOnly(2026, 11, 1);
        var utc = new DateTime(2026, 11, 1);
        var orders = new[] { new Order { Status = OrderStatus.Settled, SettledAt = utc, Total = 1000, Items = [new() { Quantity = 2, UnitCost = 100 }], Shipment = new() { Id = 1, DeliveryCharge = 60 } }, new Order { Status = OrderStatus.Delivered, Total = 9000 }, new Order { Status = OrderStatus.Returned, LogisticsLoss = 50, ReturnCases = [new() { CreatedAt = utc, LogisticsLoss = 50 }] } };
        var r = RoiCalculator.Calculate(orders, [], [new() { Date = date, Amount = 100 }], date, date);
        Assert.Equal(1000, r.Revenue);
        Assert.Equal(590, r.NetProfit);
        Assert.Equal(590, r.RoiPercent);
        Assert.Equal(50, r.ReturnLoss);
    }
    [Fact]
    public void ZeroSpendHasNoRoi()
    {
        var d = new DateOnly(2026, 11, 1);
        Assert.Null(RoiCalculator.Calculate([], [], [], d, d).RoiPercent);
    }
    [Fact]
    public void DateFilterUsesBangladeshBoundary()
    {
        var d = new DateOnly(2026, 11, 2);
        var r = RoiCalculator.Calculate([new() { Status = OrderStatus.Settled, SettledAt = new DateTime(2026, 11, 1, 18, 0, 0), Total = 42 }], [], [], d, d);
        Assert.Equal(42, r.Revenue);
    }
    [Fact]
    public void CsvEscapesFormulaNames()
    {
        var d = new DateOnly(2026, 11, 1);
        var r = new RoiReport(d, d, "=unsafe", 0, 0, 0, 0, 0, 0);
        Assert.Contains("'=unsafe", System.Text.Encoding.UTF8.GetString(RoiCalculator.Csv(r)));
    }
    [Fact]
    public void PdfIsARealDocument()
    {
        var d = new DateOnly(2026, 11, 1);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(RoiCalculator.Pdf(new(d, d, "Demo", 1, 100, 10, 5, 0, 20))));
    }
}

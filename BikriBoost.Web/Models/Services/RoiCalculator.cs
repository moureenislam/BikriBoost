using System.Globalization;
using System.Text;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using SkiaSharp;
namespace BikriBoost.Web.Models.Services;

public sealed record RoiReport(DateOnly From, DateOnly To, string Campaign, int SettledOrders, decimal Revenue, decimal ProductCost, decimal CourierFees, decimal ReturnLoss, decimal AdSpend)
{
    public decimal NetProfit => Revenue - ProductCost - CourierFees - ReturnLoss - AdSpend;
    public decimal? RoiPercent => AdSpend == 0 ? null : decimal.Round(NetProfit / AdSpend * 100, 2);
    public decimal? Roas => AdSpend == 0 ? null : decimal.Round(Revenue / AdSpend, 2);
}
public sealed class RoiCalculator(AppDbContext db, MarketingUnitOfWork work)
{
    public async Task<RoiReport> Report(DateOnly from, DateOnly to, int? campaignId)
    {
        Rules.Require(to >= from && to.DayNumber - from.DayNumber <= 3660, "Choose a valid date range of at most ten years.");
        var query = db.Orders.Include(o => o.Items).Include(o => o.ReturnCases).Include(o => o.Shipment).AsQueryable();
        var campaign = campaignId.HasValue ? await db.Campaigns.SingleOrDefaultAsync(c => c.Id == campaignId) : null;
        Rules.Require(!campaignId.HasValue || campaign != null, "Campaign not found.");
        if (campaignId.HasValue)
            query = query.Where(o => db.Attributions.Any(a => a.OrderId == o.Id && a.CampaignId == campaignId));
        var orders = await query.ToListAsync();
        var fees = await db.SettlementLines.Include(l => l.SettlementBatch).Where(l => l.IsMatched && l.SettlementBatch!.Status == SettlementBatchStatus.Applied).ToListAsync();
        var spends = await db.AdSpends.Where(a => a.Date >= from && a.Date <= to && (!campaignId.HasValue || a.CampaignId == campaignId)).ToListAsync();
        return Calculate(orders, fees, spends, from, to, campaign?.Name ?? "All campaigns");
    }
    public static RoiReport Calculate(IEnumerable<Order> source, IEnumerable<SettlementLine> fees, IEnumerable<AdSpend> spends, DateOnly from, DateOnly to, string campaign = "All campaigns")
    {
        bool InRange(DateTime dt)
        {
            var d = DateOnly.FromDateTime(BusinessTime.Local(dt));
            return d >= from && d <= to;
        }
        var all = source.ToList();
        var settled = all.Where(o => o.Status == OrderStatus.Settled && o.SettledAt.HasValue && InRange(o.SettledAt.Value)).ToList();
        decimal courier = 0;
        foreach (var o in settled)
        {
            var rows = fees.Where(l => l.IsMatched && l.ShipmentId == o.Shipment?.Id && l.ShipmentId.HasValue).ToList();
            courier += rows.Count > 0 ? rows.Sum(l => l.DeliveryFee + l.CodFee) : o.Shipment?.DeliveryCharge ?? 0;
        }
        // ReturnCase is authoritative where present; Order.LogisticsLoss is only a legacy fallback.
        decimal loss = 0;
        foreach (var o in all)
        {
            if (o.ReturnCases.Any())
                loss += o.ReturnCases.Where(r => InRange(r.ReceivedAt ?? r.CreatedAt)).Sum(r => r.LogisticsLoss);
            else if (InRange(o.SettledAt ?? o.CancelledAt ?? o.CreatedAt))
                loss += o.LogisticsLoss;
        }
        return new(from, to, campaign, settled.Count, settled.Sum(o => o.Total), settled.Sum(o => o.Items.Sum(i => i.Quantity * i.UnitCost)), courier, loss, spends.Where(s => s.Date >= from && s.Date <= to).Sum(s => s.Amount));
    }
    public Task<int> AddSpend(DateOnly date, int? campaignId, SocialPlatform platform, string? creative, decimal amount, AdSpendSource source = AdSpendSource.Manual) => work.Run(async () =>
    {
        await ValidateSpend(date, campaignId, platform, creative, amount);
        var row = new AdSpend { Date = date, CampaignId = campaignId, Platform = platform, CreativeKey = creative?.Trim(), Amount = amount, Source = source };
        db.AdSpends.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    });
    private async Task ValidateSpend(DateOnly date, int? campaignId, SocialPlatform platform, string? creative, decimal amount)
    {
        Rules.Require(date != default && date <= DateOnly.FromDateTime(BusinessTime.Local(DateTime.UtcNow)), "Spend date must be today or earlier.");
        Rules.Require(amount >= 0 && amount <= 1_000_000_000, "Spend must be between 0 and 1,000,000,000 BDT.");
        Rules.Require(Enum.IsDefined(platform), "Choose a valid platform.");
        Rules.Require((creative?.Length ?? 0) <= 100, "Creative key must be at most 100 characters.");
        Rules.Require(!campaignId.HasValue || await db.Campaigns.AnyAsync(c => c.Id == campaignId), "Campaign not found.");
    }
    public async Task<int> Import(Stream stream)
    {
        using var parser = new TextFieldParser(stream, Encoding.UTF8);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        var headers = parser.ReadFields();
        Rules.Require(headers != null && headers.SequenceEqual(new[] { "Date", "CampaignId", "Platform", "CreativeKey", "Amount" }), "CSV header must be Date,CampaignId,Platform,CreativeKey,Amount.");
        var rows = new List<AdSpend>();
        try
        {
            while (!parser.EndOfData)
            {
                var f = parser.ReadFields();
                Rules.Require(f?.Length == 5, "Each CSV row needs five fields.");
                Rules.Require(rows.Count < 1000, "Import at most 1,000 rows at a time.");
                Rules.Require(DateOnly.TryParseExact(f![0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date), "CSV dates must use yyyy-MM-dd.");
                int? campaignId = null;
                if (!string.IsNullOrWhiteSpace(f[1]))
                {
                    Rules.Require(int.TryParse(f[1], out var id), "Invalid campaign ID.");
                    campaignId = id;
                }
                Rules.Require(Enum.TryParse<SocialPlatform>(f[2], true, out var platform) && Enum.IsDefined(platform), "Unknown platform.");
                Rules.Require(decimal.TryParse(f[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount), "Invalid spend amount.");
                await ValidateSpend(date, campaignId, platform, f[3], amount);
                rows.Add(new()
                {
                    Date = date,
                    CampaignId = campaignId,
                    Platform = platform,
                    CreativeKey = f[3].Trim(),
                    Amount = amount,
                    Source = AdSpendSource.CsvImport
                });
            }
        }
        catch (MalformedLineException) { throw new BusinessRuleException("Malformed CSV. Check quoted fields and commas."); }
        Rules.Require(rows.Count > 0, "CSV has no data rows.");
        return await work.Run(async () => { var existing = await db.AdSpends.Where(a => a.Source == AdSpendSource.CsvImport).ToListAsync(); var inserted = 0; foreach (var row in rows) { if (existing.Any(e => e.Date == row.Date && e.CampaignId == row.CampaignId && e.Platform == row.Platform && e.CreativeKey == row.CreativeKey && e.Amount == row.Amount)) continue; db.AdSpends.Add(row); existing.Add(row); inserted++; } return inserted; });
    }
    public static byte[] Csv(RoiReport r)
    {
        string Cell(string s) => "\"" + ((s.Length > 0 && "=+-@".Contains(s[0])) ? "'" : "") + s.Replace("\"", "\"\"") + "\"";
        var lines = new[] { "Campaign,From,To,SettledOrders,Revenue,ProductCost,CourierFees,ReturnLoss,AdSpend,NetProfit,RoiPercent", string.Join(',', new[] { Cell(r.Campaign), r.From.ToString("yyyy-MM-dd"), r.To.ToString("yyyy-MM-dd"), r.SettledOrders.ToString(), r.Revenue.ToString(CultureInfo.InvariantCulture), r.ProductCost.ToString(CultureInfo.InvariantCulture), r.CourierFees.ToString(CultureInfo.InvariantCulture), r.ReturnLoss.ToString(CultureInfo.InvariantCulture), r.AdSpend.ToString(CultureInfo.InvariantCulture), r.NetProfit.ToString(CultureInfo.InvariantCulture), r.RoiPercent?.ToString(CultureInfo.InvariantCulture) ?? "" }) };
        return Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
    }
    public static byte[] Pdf(RoiReport r)
    {
        using var stream = new MemoryStream();
        using (var doc = SKDocument.CreatePdf(stream))
        {
            var canvas = doc.BeginPage(595, 842);
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColor.Parse("#173A32"), IsAntialias = true };
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 13);
            using var title = new SKFont(SKTypeface.FromFamilyName("Arial"), 25);
            canvas.DrawText("BikriBoost | Realized ROI", 40, 60, title, paint);
            canvas.DrawText($"{r.From:yyyy-MM-dd} to {r.To:yyyy-MM-dd}", 40, 90, font, paint);
            canvas.DrawText(r.Campaign[..Math.Min(60, r.Campaign.Length)], 40, 115, font, paint);
            var rows = new[] { ("Settled orders", r.SettledOrders.ToString()), ("Revenue (BDT)", r.Revenue.ToString("N2")), ("Product cost", r.ProductCost.ToString("N2")), ("Courier fees", r.CourierFees.ToString("N2")), ("Return losses", r.ReturnLoss.ToString("N2")), ("Advertising spend", r.AdSpend.ToString("N2")), ("Net profit", r.NetProfit.ToString("N2")), ("ROI", r.RoiPercent.HasValue ? r.RoiPercent + "%" : "N/A - zero ad spend") };
            float y = 175;
            foreach (var (label, value) in rows)
            {
                canvas.DrawText(label, 40, y, font, paint);
                canvas.DrawText(value, 360, y, font, paint);
                y += 38;
            }
            canvas.DrawText("Revenue includes settled orders only; dates use Bangladesh time.", 40, y + 35, font, paint);
            doc.EndPage();
            doc.Close();
        }
        return stream.ToArray();
    }
}

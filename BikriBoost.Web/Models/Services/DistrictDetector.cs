using System.Text.RegularExpressions;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Finds the district (and thana, when known) mentioned in an address, in English or Bangla.
/// Used by OrderTextParser (Feature 2). Districts and thanas come from the reference tables.
/// </summary>
public class DistrictDetector(AppDbContext db)
{
    /// <summary>Spellings people actually type.</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["chittagong"] = "Chattogram", ["ctg"] = "Chattogram", ["comilla"] = "Cumilla",
        ["barisal"] = "Barishal", ["bogra"] = "Bogura", ["jessore"] = "Jashore",
        ["coxs bazar"] = "Cox's Bazar", ["cox bazar"] = "Cox's Bazar",
    };

    /// <summary>Areas inside Dhaka city that are not in the thana table.</summary>
    private static readonly string[] DhakaAreas =
    [
        "mirpur", "dhanmondi", "gulshan", "banani", "uttara", "mohammadpur", "motijheel", "badda", "rampura",
        "bashundhara", "farmgate", "tejgaon", "khilgaon", "malibagh", "mohakhali", "lalbagh", "jatrabari",
        "shyamoli", "kallyanpur", "banasree", "wari", "old dhaka", "pallabi", "cantonment", "baridhara",
    ];

    /// <summary>Areas around Dhaka; those that are districts themselves map to that district.</summary>
    private static readonly string[] SuburbAreas =
        ["savar", "ashulia", "keraniganj", "tongi", "gazipur", "narayanganj", "demra", "dohar", "nawabganj"];

    private List<District>? _districts;
    private List<Thana>? _thanas;

    public record Result(District? District, Thana? Thana);

    /// <summary>Loads the district and thana lists once per request.</summary>
    public async Task LoadAsync()
    {
        _districts ??= await db.Districts.AsNoTracking().ToListAsync();
        _thanas ??= await db.Thanas.AsNoTracking().ToListAsync();
    }

    public async Task<Result> DetectAsync(string text)
    {
        await LoadAsync();
        return Detect(text);
    }

    /// <summary>Synchronous lookup; call <see cref="LoadAsync"/> first.</summary>
    public Result Detect(string text)
    {
        if (_districts is null || _thanas is null)
            throw new InvalidOperationException("Call LoadAsync before Detect.");

        var district = FindDistrict(text);
        var thana = FindThana(text, district);
        district ??= thana is null ? null : _districts.First(d => d.Id == thana.DistrictId);
        return new Result(district, thana);
    }

    private District? FindDistrict(string text)
    {
        var lower = text.ToLowerInvariant();
        District? ByName(string en) => _districts!.FirstOrDefault(d => d.NameEn == en);

        foreach (var (alias, en) in Aliases)
            if (lower.Contains(alias)) return ByName(en);

        foreach (var area in SuburbAreas)
            if (HasWord(lower, area))
                return _districts!.FirstOrDefault(d => d.NameEn.Equals(area, StringComparison.OrdinalIgnoreCase))
                       ?? ByName("Dhaka");

        foreach (var d in _districts!)
            if (HasWord(lower, d.NameEn.ToLowerInvariant()) || text.Contains(d.NameBn))
                return d;

        foreach (var area in DhakaAreas)
            if (lower.Contains(area)) return ByName("Dhaka");

        return null;
    }

    private Thana? FindThana(string text, District? district)
    {
        var lower = text.ToLowerInvariant();
        return _thanas!
            .Where(t => district is null || t.DistrictId == district.Id)
            .FirstOrDefault(t => HasWord(lower, t.NameEn.ToLowerInvariant()) || text.Contains(t.NameBn));
    }

    private static bool HasWord(string lowerText, string word) =>
        Regex.IsMatch(lowerText, @"\b" + Regex.Escape(word) + @"\b");
}

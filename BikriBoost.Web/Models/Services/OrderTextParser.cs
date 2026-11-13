using System.Text.RegularExpressions;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Feature 2 · In-Chat Order Parsing &amp; Quick Draft.
/// Reads a customer's chat message and pulls out a draft order. Rule-based, no AI:
///   - phone: 01[3-9] + 8 digits (+88, spaces, dashes and Bangla digits allowed)
///   - district/thana: looked up in English and Bangla
///   - items: matched against this shop's SKUs and product names, with the quantity nearby
/// Missing or unclear fields are listed in <see cref="ParsedOrder.Missing"/>, never guessed.
/// </summary>
public partial class OrderTextParser(AppDbContext db, DistrictDetector districts)
{
    private static readonly Dictionary<string, string[]> Labels = new()
    {
        ["name"] = ["name", "nam", "নাম"],
        ["phone"] = ["phone", "mobile", "number", "contact", "ফোন", "মোবাইল", "নম্বর"],
        ["address"] = ["address", "addr", "location", "ঠিকানা"],
        ["items"] = ["item", "items", "product", "products", "order", "পণ্য", "অর্ডার"],
    };

    /// <summary>Greetings people start chats with; never part of a name.</summary>
    [GeneratedRegex(@"^(?:(?:hi|hello|hey|salam|assalamu\s*alaikum|assalamualaikum|asslamualaikum|apu|apa|bhai|vai|vaiya|bhaiya|dear|sir|madam)\b[\s,!.]*)+", RegexOptions.IgnoreCase)]
    private static partial Regex Greeting();

    /// <summary>"ami Sharmin", "my name is Sharmin" ...</summary>
    [GeneratedRegex(@"^(?:my name is|my name|i am|i'm|im|this is|ami|amar nam|name)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Intro();

    [GeneratedRegex(@"(?:\+?88[\s-]?)?0\s*1\s*[3-9](?:[\s-]*\d){8}")]
    private static partial Regex PhoneInText();

    [GeneratedRegex(@"\b(house|road|rd|block|sector|flat|village|thana|upazila|para|lane|avenue|basha|bari|bazar|more|r/a)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AddressWords();

    private static readonly string[] FillerWords = ["ta", "ti", "pcs", "pc", "ar", "and", "nibo", "lagbe", "chai"];

    public async Task<ParsedOrder> ParseAsync(string message)
    {
        message = PhoneNumber.AsciiDigits(message).Trim();
        await districts.LoadAsync();
        var labelled = LabelledFields(message);

        labelled.TryGetValue("phone", out var phoneRaw);
        var phone = PhoneNumber.FindIn(phoneRaw ?? message);

        var items = await MatchProductsAsync(labelled.GetValueOrDefault("items") ?? message);

        var name = labelled.GetValueOrDefault("name") ?? GuessName(message);
        var productWords = items.SelectMany(i => Words(i.ProductName, minLength: 1)).ToHashSet();
        var address = labelled.GetValueOrDefault("address") ?? GuessAddress(message, productWords);
        var place = districts.Detect(address ?? message);

        var draft = new ParsedOrder
        {
            Name = name?.Trim(' ', ',', '.', '-'),
            Phone = phone,
            Address = address?.Trim(' ', ',', '.', '-'),
            DistrictId = place.District?.Id,
            DistrictName = place.District?.NameEn,
            ThanaId = place.Thana?.Id,
            ThanaName = place.Thana?.NameEn,
            Items = items,
            UnmatchedItemsText = items.Count == 0 ? labelled.GetValueOrDefault("items") : null,
        };
        draft.Missing = MissingFields(draft, phoneRaw ?? message);
        return draft;
    }

    /// <summary>Which fields must be fixed before the draft can become an order.</summary>
    public static List<string> MissingFields(ParsedOrder d, string phoneSource = "")
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(d.Name)) missing.Add("name");
        if (!PhoneNumber.IsValid(d.Phone))
            missing.Add(Regex.IsMatch(phoneSource, @"\d{5,}") ? "phone (not a valid Bangladeshi mobile number)" : "phone");
        if (string.IsNullOrWhiteSpace(d.Address)) missing.Add("address");
        if (d.DistrictId is null) missing.Add("district");
        if (d.Items.Count == 0) missing.Add("items");
        foreach (var item in d.Items.Where(i => i.VariantUnclear))
            missing.Add($"size/colour for {item.ProductName}");
        return missing;
    }

    private static Dictionary<string, string> LabelledFields(string message)
    {
        var found = new Dictionary<string, string>();
        foreach (var line in Regex.Split(message, @"\r?\n"))
        {
            var matched = false;
            foreach (var (field, labels) in Labels)
            {
                foreach (var label in labels)
                {
                    var m = Regex.Match(line, @"^\s*" + Regex.Escape(label) + @"\s*[:：=\-]\s*(.+)$", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    found.TryAdd(field, m.Groups[1].Value.Trim());
                    matched = true;
                    break;
                }
                if (matched) break;
            }
        }
        return found;
    }

    private string? GuessName(string message)
    {
        string[] patterns =
        [
            @"(?:my name is|i am|i'm|this is|\bami|amar nam)\s+([A-Za-z][A-Za-z.\s]{1,40}?)(?=[,.\n]|\s+\d|\s+from|$)",
            @"(?:আমি|আমার নাম)\s+([\p{IsBengali}\s]{2,30}?)(?=[,।\n]|$)",
        ];
        foreach (var pattern in patterns)
        {
            var m = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();
        }

        // Otherwise: a short chunk of only letters before the phone number, e.g. "Hi Sharmin, 017…".
        foreach (var raw in Regex.Split(message, @"[,\n।]|(?<=\.)\s"))
        {
            if (PhoneInText().IsMatch(raw)) break;
            var chunk = Intro().Replace(Greeting().Replace(raw.Trim(), ""), "").Trim(' ', '.', '!');
            if (Regex.IsMatch(chunk, @"^\p{L}[\p{L}.]*(?:\s+\p{L}[\p{L}.]*){0,3}$") && !LooksLikeAddress(chunk))
                return chunk;
        }
        return null;
    }

    private string? GuessAddress(string message, HashSet<string> productWords)
    {
        bool OnlyProductWords(string chunk) => productWords.Count > 0
            && Words(chunk, minLength: 1).Where(w => !FillerWords.Contains(w)).All(productWords.Contains);

        foreach (var part in Regex.Split(message, @"\r?\n|(?<=\.)\s"))
        {
            var chunks = PhoneInText().Replace(part, "").Split(',').Select(c =>
            {
                c = Greeting().Replace(c.Trim(), "").Trim();
                // "i am Nabila from Banani road 4" → "Banani road 4"
                return Intro().IsMatch(c) ? Regex.Replace(c, @"^.*?\bfrom\s+", "", RegexOptions.IgnoreCase).Trim() : c;
            }).ToList();

            // The address starts at the first chunk that looks like one; the name and greeting before it are dropped.
            for (var i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] == "" || !LooksLikeAddress(chunks[i])) continue;

                var rest = chunks.Skip(i).Where(c => c != ""
                    && !Regex.IsMatch(c, @"\d+\s*(?:ta|ti|pcs|pc|টা|টি)\b|\bx\s*\d", RegexOptions.IgnoreCase) // drop "kurti 2 ta"
                    && !OnlyProductWords(c));                                                                // drop "abaya"
                var address = string.Join(", ", rest).Trim(' ', ',', '.');
                return address == "" ? null : address;
            }
        }
        return null;
    }

    private bool LooksLikeAddress(string text) =>
        AddressWords().IsMatch(text) || districts.Detect(text).District is not null;

    /// <summary>
    /// Matches the shop's products in the text and reads the quantity next to each one.
    /// "red kurti" matches "Red cotton kurti": enough words of the name, or its unique last word.
    /// </summary>
    private async Task<List<ParsedItem>> MatchProductsAsync(string text)
    {
        var lower = text.ToLowerInvariant();
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Variants)
            .ToListAsync();
        var nounCount = products.GroupBy(p => Words(p.Name).LastOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Count());

        var best = new Dictionary<string, (Product Product, ProductVariant? Variant, double Score, int End)>();
        foreach (var product in products)
        {
            var name = product.Name.ToLowerInvariant();
            var tokens = Words(product.Name);
            var exact = lower.IndexOf(name, StringComparison.Ordinal);
            var skuVariant = product.Variants.FirstOrDefault(v => v.Sku != "" && lower.Contains(v.Sku.ToLowerInvariant()));

            var hits = new Dictionary<string, int>(); // word → character offset just after it
            foreach (var token in tokens)
            {
                var m = Regex.Match(lower, @"(?<!\p{L})" + Regex.Escape(token) + @"(?!\p{L})");
                if (m.Success) hits.TryAdd(token, m.Index + token.Length);
            }
            var noun = tokens.LastOrDefault();
            var enoughWords = tokens.Count > 0 && hits.Count >= Math.Max(2, (int)Math.Ceiling(tokens.Count / 2.0));
            var uniqueNoun = noun is not null && hits.ContainsKey(noun) && nounCount.GetValueOrDefault(noun) == 1;

            (double Score, int End) match;
            if (exact >= 0) match = (100, exact + name.Length);
            else if (skuVariant is not null)
            {
                var at = lower.IndexOf(skuVariant.Sku.ToLowerInvariant(), StringComparison.Ordinal);
                match = (95, at + skuVariant.Sku.Length);
            }
            else if (enoughWords || uniqueNoun) match = ((double)hits.Count / Math.Max(1, tokens.Count) * 90, hits.Values.Max());
            else continue;

            // Two products sharing a word ("kurti"): keep the better match.
            var key = noun ?? product.Id.ToString();
            if (!best.TryGetValue(key, out var current) || match.Score > current.Score)
                best[key] = (product, skuVariant, match.Score, match.End);
        }

        return best.Values.OrderBy(b => b.End).Select(b =>
        {
            var variant = b.Variant ?? PickVariant(b.Product, lower);
            var activeVariants = b.Product.Variants.OrderBy(v => v.Id).ToList();
            var chosen = variant ?? activeVariants.FirstOrDefault();
            return new ParsedItem
            {
                ProductName = b.Product.Name,
                ProductVariantId = chosen?.Id,
                VariantLabel = chosen is null ? "" : VariantLabel(chosen),
                // Several sizes/colours and the message did not say which: flag it instead of guessing.
                VariantUnclear = variant is null && activeVariants.Count > 1,
                Quantity = ReadQuantity(lower, b.End),
                UnitPrice = chosen?.Price ?? b.Product.BasePrice,
            };
        }).ToList();
    }

    /// <summary>
    /// The variant whose size, colour or material is mentioned in the text, if exactly one is.
    /// Only attributes that differ between the variants count: if every kurti is red, "red" picks none of them.
    /// </summary>
    private static ProductVariant? PickVariant(Product product, string lower)
    {
        if (product.Variants.Count == 1) return product.Variants.First();

        Func<ProductVariant, string?>[] attributes = [v => v.Size, v => v.Color, v => v.Material];
        var distinguishing = attributes
            .Where(attr => product.Variants.Select(attr).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .ToList();

        var mentioned = product.Variants
            .Where(v => distinguishing.Any(attr => Mentions(lower, attr(v))))
            .ToList();
        return mentioned.Count == 1 ? mentioned[0] : null;
    }

    private static bool Mentions(string lower, string? word) =>
        !string.IsNullOrWhiteSpace(word)
        && Regex.IsMatch(lower, @"(?<![\p{L}\p{N}])" + Regex.Escape(word.ToLowerInvariant()) + @"(?![\p{L}\p{N}])");

    private static int ReadQuantity(string lower, int end)
    {
        var after = lower.Substring(end, Math.Min(20, lower.Length - end));
        var start = Math.Max(0, end - 40);
        var before = lower[start..end];

        var m = Regex.Match(after, @"^\s*(?:x|×|\*|qty|-)?\s*(\d{1,3})\s*(?:ta|ti|pcs|pc|piece|টা|টি)?");
        if (m.Success) return Math.Max(1, int.Parse(m.Groups[1].Value));

        m = Regex.Match(before, @"(\d{1,3})\s*(?:x|×|ta|ti|pcs|pc|টা|টি)\s+[\p{L}\s-]*$"); // "2 ta red kurti"
        return m.Success ? Math.Max(1, int.Parse(m.Groups[1].Value)) : 1;
    }

    public static string VariantLabel(ProductVariant v) =>
        string.Join(" / ", new[] { v.Color, v.Size, v.Material }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static List<string> Words(string s, int minLength = 3) =>
        Regex.Split(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length >= minLength).ToList();
}

/// <summary>The parser's result: a draft the moderator checks and corrects before saving.</summary>
public class ParsedOrder
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public int? ThanaId { get; set; }
    public string? ThanaName { get; set; }
    public List<ParsedItem> Items { get; set; } = [];
    public string? UnmatchedItemsText { get; set; }
    public List<string> Missing { get; set; } = [];
}

public class ParsedItem
{
    public string ProductName { get; set; } = "";
    public int? ProductVariantId { get; set; }
    public string VariantLabel { get; set; } = "";
    public bool VariantUnclear { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

using BikriBoost.Web.Models.Services;

namespace BikriBoost.Tests.Unit;

/// <summary>Feature 2 · In-Chat Order Parsing. Cases ported from the old Laravel ModuleA tests.</summary>
public class OrderTextParserTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly Services _s;
    public OrderTextParserTests() => _s = new Services(_t);
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Reads_a_labelled_message()
    {
        var d = await _s.Parser.ParseAsync("Name: Sharmin Akter\nPhone: 01712-345678\nAddress: House 12, Road 5, Mirpur 10, Dhaka\nItem: Red kurti x2, size M");

        Assert.Equal("Sharmin Akter", d.Name);
        Assert.Equal("01712345678", d.Phone);
        Assert.Equal("Dhaka", d.DistrictName);
        Assert.Equal("Mirpur", d.ThanaName);
        var item = Assert.Single(d.Items);
        Assert.Equal(await _s.VariantId("KURTI-RED-M"), item.ProductVariantId);
        Assert.Equal(2, item.Quantity);
        Assert.Empty(d.Missing);
    }

    [Fact]
    public async Task Reads_free_text_with_Bangla_digits_and_ta_quantity()
    {
        var d = await _s.Parser.ParseAsync("ami Rumana, ০১৬৭৩৩২২১১০, Agrabad, Chattogram. black abaya 2 ta nibo");

        Assert.Equal("Rumana", d.Name);
        Assert.Equal("01673322110", d.Phone);
        Assert.Equal("Chattogram", d.DistrictName);
        Assert.Equal(2, Assert.Single(d.Items).Quantity);
    }

    [Fact]
    public async Task Skips_greetings_and_reads_two_items_with_quantities_before_and_after()
    {
        var d = await _s.Parser.ParseAsync("Assalamualaikum apu, ami Lamia, 01799887766, Mohammadpur Town Hall. black abaya 1 ta ar 2 ta kids frock");

        Assert.Equal("Lamia", d.Name);
        Assert.Equal("Mohammadpur Town Hall", d.Address);
        Assert.Equal([("Black georgette abaya", 1), ("Kids party frock", 2)], d.Items.Select(i => (i.ProductName, i.Quantity)));
    }

    [Fact]
    public async Task Unclear_size_is_flagged_not_guessed()
    {
        var d = await _s.Parser.ParseAsync("Hi Sharmin, 01325678902, Mirpur 10. red kurti 2 ta");

        Assert.Equal("Sharmin", d.Name);
        Assert.Equal("Mirpur 10", d.Address);
        var item = Assert.Single(d.Items);
        Assert.True(item.VariantUnclear);
        Assert.Equal(2, item.Quantity);
        Assert.Contains("size/colour for Red cotton kurti", d.Missing);
    }

    [Fact]
    public async Task Invalid_phone_and_missing_fields_are_listed()
    {
        var d = await _s.Parser.ParseAsync("Name: Rina\nPhone: 01212345678");

        Assert.Null(d.Phone);
        Assert.Contains("phone (not a valid Bangladeshi mobile number)", d.Missing);
        Assert.Contains("address", d.Missing);
        Assert.Contains("items", d.Missing);
    }

    [Fact]
    public async Task A_word_shared_by_two_products_matches_neither()
    {
        // "red" is in both "Red cotton kurti" and "Red silk saree": not enough to pick one.
        var d = await _s.Parser.ParseAsync("Rina, 01712345678, Mirpur. red 2 ta");
        Assert.Empty(d.Items);
    }

    [Theory]
    [InlineData("01712345678", "01712345678")]   // normal
    [InlineData("+880 1712-345678", "01712345678")] // country code, spaces, dash
    [InlineData("০১৭১২৩৪৫৬৭৮", "01712345678")]   // Bangla digits
    [InlineData("01312345678", "01312345678")]   // boundary: lowest operator digit 3
    [InlineData("01912345678", "01912345678")]   // boundary: highest operator digit 9
    [InlineData("01212345678", null)]            // operator digit 2 does not exist
    [InlineData("0171234567", null)]             // 10 digits: one short
    [InlineData("017123456789", null)]           // 12 digits: one too many
    public void Phone_numbers_follow_the_Bangladeshi_pattern(string input, string? expected) =>
        Assert.Equal(expected, PhoneNumber.Normalize(input));
}

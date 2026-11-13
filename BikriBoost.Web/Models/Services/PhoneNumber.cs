using System.Text;
using System.Text.RegularExpressions;

namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Bangladeshi mobile numbers: 01[3-9] followed by 8 digits (11 digits in total).
/// "+88" / "88" country codes, spaces, dashes and Bangla digits are accepted as input.
/// </summary>
public static partial class PhoneNumber
{
    [GeneratedRegex(@"^01[3-9]\d{8}$")]
    private static partial Regex ValidPattern();

    // Optional +88, then 0 1 [3-9] and 8 more digits, allowing spaces/dashes between digits.
    [GeneratedRegex(@"(?:\+?88[\s-]?)?(0\s*1\s*[3-9](?:[\s-]*\d){8})(?!\d)")]
    private static partial Regex InTextPattern();

    /// <summary>Converts Bangla digits (০–৯) to ASCII digits (0–9).</summary>
    public static string AsciiDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch is >= '০' and <= '৯' ? (char)('0' + (ch - '০')) : ch);
        return sb.ToString();
    }

    /// <summary>Strips everything but digits and the 88 country code. Null when not a valid BD mobile.</summary>
    public static string? Normalize(string? raw)
    {
        if (raw is null) return null;
        var digits = Regex.Replace(AsciiDigits(raw), @"\D", "");
        if (digits.StartsWith("880")) digits = digits[2..];
        return IsValid(digits) ? digits : null;
    }

    public static bool IsValid(string? phone) => phone is not null && ValidPattern().IsMatch(phone);

    /// <summary>The first valid BD mobile number anywhere in free text, or null.</summary>
    public static string? FindIn(string text)
    {
        var m = InTextPattern().Match(AsciiDigits(text));
        return m.Success ? Normalize(m.Groups[1].Value) : null;
    }

    /// <summary>01712345678 → 017****5678, for screens that should not show the full number.</summary>
    public static string Mask(string phone) => phone.Length >= 7 ? phone[..3] + "****" + phone[^4..] : phone;
}

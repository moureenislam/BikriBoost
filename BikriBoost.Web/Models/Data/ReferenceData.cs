using BikriBoost.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace BikriBoost.Web.Models.Data;

/// <summary>
/// Fixed reference rows seeded through the migration (HasData), so every member's
/// database has identical roles, districts, couriers and platform rules.
/// IDs and concurrency stamps are constants so migrations stay stable.
/// </summary>
public static class ReferenceData
{
    public static class RoleNames
    {
        public const string Owner = "Owner";
        public const string Moderator = "Moderator";
        public const string WarehouseClerk = "WarehouseClerk";
        public const string Marketer = "Marketer";
    }

    public static readonly IdentityRole[] Roles =
    [
        Role("6f1c2a10-0001-4000-8000-000000000001", RoleNames.Owner),
        Role("6f1c2a10-0002-4000-8000-000000000002", RoleNames.Moderator),
        Role("6f1c2a10-0003-4000-8000-000000000003", RoleNames.WarehouseClerk),
        Role("6f1c2a10-0004-4000-8000-000000000004", RoleNames.Marketer),
    ];

    private static IdentityRole Role(string id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };

    // All 64 districts of Bangladesh, grouped by division.
    private static readonly (string Division, string En, string Bn)[] DistrictRows =
    [
        ("Dhaka", "Dhaka", "ঢাকা"),
        ("Dhaka", "Faridpur", "ফরিদপুর"),
        ("Dhaka", "Gazipur", "গাজীপুর"),
        ("Dhaka", "Gopalganj", "গোপালগঞ্জ"),
        ("Dhaka", "Kishoreganj", "কিশোরগঞ্জ"),
        ("Dhaka", "Madaripur", "মাদারীপুর"),
        ("Dhaka", "Manikganj", "মানিকগঞ্জ"),
        ("Dhaka", "Munshiganj", "মুন্সীগঞ্জ"),
        ("Dhaka", "Narayanganj", "নারায়ণগঞ্জ"),
        ("Dhaka", "Narsingdi", "নরসিংদী"),
        ("Dhaka", "Rajbari", "রাজবাড়ী"),
        ("Dhaka", "Shariatpur", "শরীয়তপুর"),
        ("Dhaka", "Tangail", "টাঙ্গাইল"),

        ("Chattogram", "Bandarban", "বান্দরবান"),
        ("Chattogram", "Brahmanbaria", "ব্রাহ্মণবাড়িয়া"),
        ("Chattogram", "Chandpur", "চাঁদপুর"),
        ("Chattogram", "Chattogram", "চট্টগ্রাম"),
        ("Chattogram", "Cumilla", "কুমিল্লা"),
        ("Chattogram", "Cox's Bazar", "কক্সবাজার"),
        ("Chattogram", "Feni", "ফেনী"),
        ("Chattogram", "Khagrachhari", "খাগড়াছড়ি"),
        ("Chattogram", "Lakshmipur", "লক্ষ্মীপুর"),
        ("Chattogram", "Noakhali", "নোয়াখালী"),
        ("Chattogram", "Rangamati", "রাঙ্গামাটি"),

        ("Rajshahi", "Bogura", "বগুড়া"),
        ("Rajshahi", "Chapainawabganj", "চাঁপাইনবাবগঞ্জ"),
        ("Rajshahi", "Joypurhat", "জয়পুরহাট"),
        ("Rajshahi", "Naogaon", "নওগাঁ"),
        ("Rajshahi", "Natore", "নাটোর"),
        ("Rajshahi", "Pabna", "পাবনা"),
        ("Rajshahi", "Rajshahi", "রাজশাহী"),
        ("Rajshahi", "Sirajganj", "সিরাজগঞ্জ"),

        ("Khulna", "Bagerhat", "বাগেরহাট"),
        ("Khulna", "Chuadanga", "চুয়াডাঙ্গা"),
        ("Khulna", "Jashore", "যশোর"),
        ("Khulna", "Jhenaidah", "ঝিনাইদহ"),
        ("Khulna", "Khulna", "খুলনা"),
        ("Khulna", "Kushtia", "কুষ্টিয়া"),
        ("Khulna", "Magura", "মাগুরা"),
        ("Khulna", "Meherpur", "মেহেরপুর"),
        ("Khulna", "Narail", "নড়াইল"),
        ("Khulna", "Satkhira", "সাতক্ষীরা"),

        ("Barishal", "Barguna", "বরগুনা"),
        ("Barishal", "Barishal", "বরিশাল"),
        ("Barishal", "Bhola", "ভোলা"),
        ("Barishal", "Jhalokati", "ঝালকাঠি"),
        ("Barishal", "Patuakhali", "পটুয়াখালী"),
        ("Barishal", "Pirojpur", "পিরোজপুর"),

        ("Sylhet", "Habiganj", "হবিগঞ্জ"),
        ("Sylhet", "Moulvibazar", "মৌলভীবাজার"),
        ("Sylhet", "Sunamganj", "সুনামগঞ্জ"),
        ("Sylhet", "Sylhet", "সিলেট"),

        ("Rangpur", "Dinajpur", "দিনাজপুর"),
        ("Rangpur", "Gaibandha", "গাইবান্ধা"),
        ("Rangpur", "Kurigram", "কুড়িগ্রাম"),
        ("Rangpur", "Lalmonirhat", "লালমনিরহাট"),
        ("Rangpur", "Nilphamari", "নীলফামারী"),
        ("Rangpur", "Panchagarh", "পঞ্চগড়"),
        ("Rangpur", "Rangpur", "রংপুর"),
        ("Rangpur", "Thakurgaon", "ঠাকুরগাঁও"),

        ("Mymensingh", "Jamalpur", "জামালপুর"),
        ("Mymensingh", "Mymensingh", "ময়মনসিংহ"),
        ("Mymensingh", "Netrokona", "নেত্রকোনা"),
        ("Mymensingh", "Sherpur", "শেরপুর"),
    ];

    public static readonly District[] Districts = DistrictRows
        .Select((d, i) => new District { Id = i + 1, Division = d.Division, NameEn = d.En, NameBn = d.Bn })
        .ToArray();

    private static int DistrictId(string nameEn) => Districts.Single(d => d.NameEn == nameEn).Id;

    // Dhaka metropolitan thanas plus nearby upazilas, enough for the demo and parser tests.
    // Add other districts' thanas in a later migration as the parser needs them.
    private static readonly (string District, string En, string Bn)[] ThanaRows =
    [
        ("Dhaka", "Badda", "বাড্ডা"),
        ("Dhaka", "Dhanmondi", "ধানমন্ডি"),
        ("Dhaka", "Gulshan", "গুলশান"),
        ("Dhaka", "Khilgaon", "খিলগাঁও"),
        ("Dhaka", "Lalbagh", "লালবাগ"),
        ("Dhaka", "Mirpur", "মিরপুর"),
        ("Dhaka", "Mohammadpur", "মোহাম্মদপুর"),
        ("Dhaka", "Motijheel", "মতিঝিল"),
        ("Dhaka", "Pallabi", "পল্লবী"),
        ("Dhaka", "Ramna", "রমনা"),
        ("Dhaka", "Tejgaon", "তেজগাঁও"),
        ("Dhaka", "Uttara", "উত্তরা"),
        ("Dhaka", "Keraniganj", "কেরানীগঞ্জ"),
        ("Dhaka", "Savar", "সাভার"),
    ];

    public static readonly Thana[] Thanas = ThanaRows
        .Select((t, i) => new Thana { Id = i + 1, DistrictId = DistrictId(t.District), NameEn = t.En, NameBn = t.Bn })
        .ToArray();

    public static readonly Courier[] Couriers =
    [
        new() { Id = 1, Code = "pathao", Name = "Pathao Courier", HasWebhook = true, IsActive = true },
        new() { Id = 2, Code = "steadfast", Name = "Steadfast Courier", HasWebhook = true, IsActive = true },
        new() { Id = 3, Code = "redx", Name = "RedX", HasWebhook = false, IsActive = true },
    ];

    // Starting limits; they are settings, so the team can adjust them without code changes.
    public static readonly PlatformRule[] PlatformRules =
    [
        new() { Id = 1, Platform = SocialPlatform.Facebook, MaxCaptionLength = 63206, MaxHashtags = 30,
                MaxMediaCount = 10, MinAspectRatio = 0.800m, MaxAspectRatio = 1.910m },
        new() { Id = 2, Platform = SocialPlatform.Instagram, MaxCaptionLength = 2200, MaxHashtags = 30,
                MaxMediaCount = 10, MinAspectRatio = 0.800m, MaxAspectRatio = 1.910m },
    ];
}

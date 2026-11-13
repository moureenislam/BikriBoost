using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Tests;

/// <summary>Pretends a user of the given shop is signed in. Change MerchantId to "switch shops".</summary>
public sealed class FakeMerchant(int? merchantId) : ICurrentMerchant
{
    public int? MerchantId { get; set; } = merchantId;
}

/// <summary>
/// A fresh in-memory SQLite database per test, built from the same AppDbContext model as MariaDB
/// (including the reference data: roles, districts, couriers). Two shops, each with the demo catalogue.
/// </summary>
public sealed class TestDb : IDisposable
{
    public const string Owner1 = "owner-1", Moderator1 = "moderator-1", Owner2 = "owner-2";
    public const int Shop1 = 1, Shop2 = 2, Shop3 = 3;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    public FakeMerchant Merchant { get; } = new(Shop1);

    public TestDb()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();

        db.Merchants.AddRange(
            new Merchant { Id = Shop1, Name = "Shop One", Slug = "one" },
            new Merchant { Id = Shop2, Name = "Shop Two", Slug = "two" },
            new Merchant { Id = Shop3, Name = "Shop Three", Slug = "three" });
        db.Users.AddRange(
            new AppUser { Id = Owner1, UserName = "o1", MerchantId = Shop1, FullName = "Owner One" },
            new AppUser { Id = Moderator1, UserName = "m1", MerchantId = Shop1, FullName = "Mod One" },
            new AppUser { Id = Owner2, UserName = "o2", MerchantId = Shop2, FullName = "Owner Two" });
        foreach (var shop in new[] { Shop1, Shop2 })
        {
            db.Products.AddRange(
                Product(shop, "Red cotton kurti", 1000m, ("KURTI-RED-S", "Red", "S", 10), ("KURTI-RED-M", "Red", "M", 2), ("KURTI-RED-L", "Red", "L", 5)),
                Product(shop, "Black georgette abaya", 1800m, ("ABAYA-BLK", "Black", null, 8)),
                Product(shop, "Kids party frock", 950m, ("FROCK-KIDS", null, null, 6)),
                Product(shop, "Red silk saree", 3200m, ("SAREE-RED-SILK", "Red", null, 3)));
        }
        db.SaveChanges();
    }

    private static Product Product(int shop, string name, decimal price, params (string Sku, string? Color, string? Size, int Stock)[] variants)
    {
        var p = new Product { MerchantId = shop, Name = name, BasePrice = price };
        foreach (var v in variants)
            p.Variants.Add(new ProductVariant { MerchantId = shop, Sku = v.Sku, Color = v.Color, Size = v.Size, Price = price, CostPrice = price / 2, Stock = v.Stock });
        return p;
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, Merchant);

    public void Dispose() => _connection.Dispose();
}

/// <summary>All Model-layer services wired to one context, the way ASP.NET's DI does it per request.</summary>
public sealed class Services
{
    public AppDbContext Db { get; }
    public OrderService Orders { get; }
    public OrderTextParser Parser { get; }
    public OrderLifecycleService Lifecycle { get; }

    public Services(TestDb t)
    {
        Db = t.NewContext();
        Orders = new OrderService(Db, t.Merchant);
        Parser = new OrderTextParser(Db, new DistrictDetector(Db));
        Lifecycle = new OrderLifecycleService(Db);
    }

    public async Task<int> VariantId(string sku) => (await Db.ProductVariants.FirstAsync(v => v.Sku == sku)).Id;

    /// <summary>Creates a Pending order through OrderService, exactly as the Draft page does.</summary>
    public async Task<Order> NewOrderAsync(string phone = "01712345678", string district = "Dhaka",
        string sku = "KURTI-RED-S", int qty = 1, string userId = TestDb.Owner1)
    {
        var districtId = (await Db.Districts.FirstAsync(d => d.NameEn == district)).Id;
        return await Orders.CreateAsync(new NewOrderInput
        {
            Name = "Test Customer", Phone = phone, Address = "House 1, Road 2", DistrictId = districtId,
            Items = [new NewOrderLine { ProductVariantId = await VariantId(sku), Quantity = qty }],
        }, userId);
    }
}

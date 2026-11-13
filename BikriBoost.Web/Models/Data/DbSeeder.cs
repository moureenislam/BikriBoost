using BikriBoost.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static BikriBoost.Web.Models.Data.ReferenceData;

namespace BikriBoost.Web.Models.Data;

/// <summary>
/// Loads shared demo data in Development. Reference data (roles, districts, couriers)
/// comes from the migration; this adds the demo shop, its logins and products.
/// Each part checks whether it already exists, so it is safe to run on every start.
/// Nobody is signed in while seeding, so every query uses IgnoreQueryFilters and sets MerchantId itself.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Demo@12345";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var demo = await EnsureMerchantAsync(db, "Demo Shop", "demo-shop", "01711000000");
        await EnsureStaffAsync(users, demo);
        await EnsureProductsAsync(db, demo);
    }

    private static async Task<Merchant> EnsureMerchantAsync(AppDbContext db, string name, string slug, string phone)
    {
        var merchant = await db.Merchants.FirstOrDefaultAsync(m => m.Slug == slug);
        if (merchant is not null) return merchant;
        merchant = new Merchant { Name = name, Slug = slug, Phone = phone };
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();
        return merchant;
    }

    private static async Task EnsureStaffAsync(UserManager<AppUser> users, Merchant merchant)
    {
        (string Email, string Name, string Role)[] staff =
        [
            ("owner@demo.bikriboost.test", "Demo Owner", RoleNames.Owner),
            ("moderator@demo.bikriboost.test", "Demo Moderator", RoleNames.Moderator),
            ("warehouse@demo.bikriboost.test", "Demo Warehouse Clerk", RoleNames.WarehouseClerk),
            ("marketer@demo.bikriboost.test", "Demo Marketer", RoleNames.Marketer),
        ];

        foreach (var (email, name, role) in staff)
        {
            if (await users.FindByEmailAsync(email) is not null) continue;
            var user = new AppUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = name, MerchantId = merchant.Id,
            };
            var created = await users.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    $"Seeding {email} failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            await users.AddToRoleAsync(user, role);
        }
    }

    private static async Task EnsureProductsAsync(AppDbContext db, Merchant merchant)
    {
        if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.MerchantId == merchant.Id)) return;

        Product P(string name, string category, decimal price, params (string Sku, string? Color, string? Size, int Stock, decimal Cost)[] variants)
        {
            var product = new Product { MerchantId = merchant.Id, Name = name, Category = category, BasePrice = price };
            foreach (var v in variants)
                product.Variants.Add(new ProductVariant
                {
                    MerchantId = merchant.Id, Sku = v.Sku, Color = v.Color, Size = v.Size,
                    Price = price, CostPrice = v.Cost, Stock = v.Stock,
                });
            return product;
        }

        db.Products.AddRange(
            P("Red cotton kurti", "Kurti", 1000m,
                ("KURTI-RED-S", "Red", "S", 10, 550m), ("KURTI-RED-M", "Red", "M", 2, 550m), ("KURTI-RED-L", "Red", "L", 5, 550m)),
            P("Black georgette abaya", "Abaya", 1800m, ("ABAYA-BLK", "Black", null, 8, 1000m)),
            P("Kids party frock", "Kids", 950m, ("FROCK-KIDS", null, null, 6, 500m)),
            P("Red silk saree", "Saree", 3200m, ("SAREE-RED-SILK", "Red", null, 3, 2100m)),
            P("Mango pickle 500g", "Food", 350m, ("ACHAR-AAM-500", null, "500g", 20, 180m)));
        await db.SaveChangesAsync();
    }
}

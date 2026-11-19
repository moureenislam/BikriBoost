using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BikriBoost.Web.Jobs;
using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace BikriBoost.Tests.Integration;

public sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());
        var merchant = Request.Headers["X-Test-Merchant"].FirstOrDefault() ?? "1";
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Name, "Test"), new Claim(ClaimTypes.Role, role!), new Claim("merchant_id", merchant) }, "Test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
}
public sealed class MarketingFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public MarketingFactory()
    {
        connection.Open();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(MarketingWorker)).ToArray())
                services.Remove(descriptor);
            services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test"; }).AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            services.AddLogging(o => o.SetMinimumLevel(LogLevel.Error));
        });
    }
    public AppDbContext Db(int merchant = 1) => new(Services.GetRequiredService<DbContextOptions<AppDbContext>>(), new JobMerchant(merchant));
    public async Task Seed()
    {
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
        db.Merchants.AddRange(new Merchant { Id = 1, Name = "Shop One", Slug = "one" }, new Merchant { Id = 2, Name = "Shop Two", Slug = "two" });
        db.Users.Add(new AppUser { Id = "test-user", UserName = "tester", Email = "test@test.local", MerchantId = 1 });
        db.ProductVariants.Add(new ProductVariant { Id = 1, Sku = "TEST", Stock = 5, Price = 100, CostPrice = 20, Product = new() { Name = "Test product" } });
        db.Customers.Add(new Customer { Id = 1, Name = "Test customer", Phone = "01711000000" });
        db.Campaigns.AddRange(new Campaign { Id = 1, Name = "Campaign one" }, new Campaign { Id = 2, MerchantId = 2, Name = "Private campaign" });
        db.Orders.Add(new Order { Id = 1, OrderNumber = "TEST-1", CustomerId = 1, DistrictId = 1, ShippingPhone = "01711000000", Subtotal = 500, Total = 550, DeliveryCharge = 50, Items = [new() { ProductVariantId = 1, Quantity = 5, UnitCost = 20, UnitPrice = 100, LineTotal = 500 }] });
        await db.SaveChangesAsync();
    }
    public HttpClient Client(string? role = "Owner", int merchant = 1)
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null)
            c.DefaultRequestHeaders.Add("X-Test-Role", role);
        c.DefaultRequestHeaders.Add("X-Test-Merchant", merchant.ToString());
        return c;
    }
    public static async Task<FormUrlEncodedContent> Form(HttpClient client, string page, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Anti-forgery token missing");
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return new(values);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            connection.Dispose();
    }
}

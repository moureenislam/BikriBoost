using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database: MariaDB through Pomelo, or SQLite for the local demo.
var cs = builder.Configuration.GetConnectionString("Default");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentMerchant, HttpCurrentMerchant>();
var localDemo = builder.Environment.IsDevelopment() && builder.Configuration["Database:Provider"] == "Sqlite";
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (localDemo)
        o.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "marketing-demo.db")}");
    else
        o.UseMySql(cs ?? throw new InvalidOperationException("Connection string Default is missing."), new MariaDbServerVersion(new Version(10, 4, 32)));
});

// Identity: password hashing, lockout, and the four existing roles.
builder.Services
    .AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/AccessDenied"; });
builder.Services.AddScoped<DistrictDetector>();
builder.Services.AddScoped<OrderTextParser>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<OrderLifecycleService>();
builder.Services.AddScoped<PaymentVerificationService>();
builder.Services.AddScoped<MarketingUnitOfWork>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddSingleton<BrandComplianceChecker>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddSingleton<IPublisher, SimulatedPublisher>();
builder.Services.AddHostedService<BikriBoost.Web.Jobs.MarketingWorker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<MarketingErrors>();
    o.Filters.Add<MarketingInputValidation>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    if (localDemo)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }
    await DbSeeder.SeedAsync(app.Services);
    if (builder.Configuration.GetValue("Demo:SeedMarketing", localDemo))
        await MarketingDemoSeeder.Seed(app.Services, app.Environment);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program
{
}

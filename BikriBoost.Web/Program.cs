using BikriBoost.Web.Models.Data;
using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database: MariaDB 10.4 (XAMPP) through EF Core 9 + Pomelo ──
var cs = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentMerchant, HttpCurrentMerchant>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(cs, new MariaDbServerVersion(new Version(10, 4, 32))));

// ── Identity: password hashing, lockout, four roles ──
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

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});

// ── Model-layer services (business rules) ──

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
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

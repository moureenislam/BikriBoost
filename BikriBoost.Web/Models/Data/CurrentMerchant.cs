using System.Security.Claims;
using BikriBoost.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BikriBoost.Web.Models.Data;

public interface ICurrentMerchant
{
    /// <summary>The signed-in user's merchant, or null (anonymous, design time, background jobs).</summary>
    int? MerchantId { get; }
}

public sealed class HttpCurrentMerchant(IHttpContextAccessor accessor) : ICurrentMerchant
{
    public const string ClaimType = "merchant_id";

    public int? MerchantId =>
        int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimType), out var id) ? id : null;
}

/// <summary>Adds the merchant_id claim at sign-in so ICurrentMerchant can read it on every request.</summary>
public sealed class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MerchantId is int merchantId)
            identity.AddClaim(new Claim(HttpCurrentMerchant.ClaimType, merchantId.ToString()));
        return identity;
    }
}

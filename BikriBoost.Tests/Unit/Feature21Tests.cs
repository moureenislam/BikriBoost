using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.Models.Services;
namespace BikriBoost.Tests.Unit;

public class Feature21Tests
{
    [Fact] public void DiscountCannotExceedSubtotal() => Assert.Equal(100, CampaignService.Discount(new Coupon { Type = CouponType.Fixed, Value = 500 }, 100, DateTime.UtcNow, 0));
    [Fact]
    public void ExactExpiryIsInvalid()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<BusinessRuleException>(() => CampaignService.Discount(new Coupon { ExpiresAt = now }, 100, now, 0));
    }
    [Fact] public void PerPhoneLimitIsEnforced() => Assert.Throws<BusinessRuleException>(() => CampaignService.Discount(new Coupon { PerPhoneLimit = 1 }, 100, DateTime.UtcNow, 1));
    [Fact] public void TotalLimitIsEnforced() => Assert.Throws<BusinessRuleException>(() => CampaignService.Discount(new Coupon { TotalLimit = 1, TimesUsed = 1 }, 100, DateTime.UtcNow, 0));
    [Fact] public void PercentageRoundsMoney() => Assert.Equal(33.33m, CampaignService.Discount(new Coupon { Type = CouponType.Percent, Value = 10 }, 333.33m, DateTime.UtcNow, 0));
    [Fact] public void PercentOver100IsInvalid() => Assert.Throws<BusinessRuleException>(() => CampaignService.ValidateCouponDefinition(CouponType.Percent, 101, null, null, null));
    [Fact] public void LocalAndInternationalPhonesMatch() => Assert.Equal(CampaignService.Phone("01711000000"), CampaignService.Phone("+880 1711-000000"));
}

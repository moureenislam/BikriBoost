using BikriBoost.Web.Models.Entities;
namespace BikriBoost.Web.ViewModels;

public sealed record ApprovalIndex(List<Post> Posts, List<Asset> Assets, List<ProductVariant> Variants);
public sealed record ApprovalReview(Post Post, List<Asset> Assets, List<ProductVariant> Variants);

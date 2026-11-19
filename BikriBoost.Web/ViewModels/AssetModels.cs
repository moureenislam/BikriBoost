using BikriBoost.Web.Models.Entities;
namespace BikriBoost.Web.ViewModels;

public sealed record AssetLibrary(List<Asset> Assets, BrandKit? Kit);

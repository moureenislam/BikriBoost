namespace BikriBoost.Web.Models.Entities;

/// <summary>
/// Every business table carries MerchantId. AppDbContext adds a global query filter
/// for each type that implements this, so one shop never sees another shop's rows.
/// </summary>
public interface ITenantEntity
{
    int MerchantId { get; set; }
}

/// <summary>
/// Optimistic concurrency token. AppDbContext increments Version on every update,
/// so two moderators saving the same row at once makes the second save fail.
/// </summary>
public interface IVersioned
{
    int Version { get; set; }
}

public abstract class TenantEntity : ITenantEntity
{
    public int Id { get; set; }
    public int MerchantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.Linq.Expressions;
using BikriBoost.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentMerchant currentMerchant)
    : IdentityDbContext<AppUser>(options)
{
    /// <summary>Read by the global query filters; EF Core re-evaluates it for every query.</summary>
    public int? CurrentMerchantId => currentMerchant.MerchantId;

    // Tenancy and staff
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();
    public DbSet<ModeratorMetric> ModeratorMetrics => Set<ModeratorMetric>();

    // Geography
    public DbSet<District> Districts => Set<District>();
    public DbSet<Thana> Thanas => Set<Thana>();

    // Customers and conversations
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<CommentThread> CommentThreads => Set<CommentThread>();

    // Catalog and stock
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockEvent> StockEvents => Set<StockEvent>();

    // Orders and payments
    public DbSet<OrderDraft> OrderDrafts => Set<OrderDraft>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<PaymentLink> PaymentLinks => Set<PaymentLink>();
    public DbSet<PaymentClaim> PaymentClaims => Set<PaymentClaim>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<BlockedIp> BlockedIps => Set<BlockedIp>();
    public DbSet<DeviceFingerprint> DeviceFingerprints => Set<DeviceFingerprint>();
    public DbSet<FraudReport> FraudReports => Set<FraudReport>();

    // Logistics
    public DbSet<Courier> Couriers => Set<Courier>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<TrackingToken> TrackingTokens => Set<TrackingToken>();
    public DbSet<DeliveryStat> DeliveryStats => Set<DeliveryStat>();
    public DbSet<SettlementBatch> SettlementBatches => Set<SettlementBatch>();
    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();
    public DbSet<Discrepancy> Discrepancies => Set<Discrepancy>();
    public DbSet<ReturnCase> ReturnCases => Set<ReturnCase>();
    public DbSet<QcInspection> QcInspections => Set<QcInspection>();

    // Marketing
    public DbSet<PlatformRule> PlatformRules => Set<PlatformRule>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostTarget> PostTargets => Set<PostTarget>();
    public DbSet<PostTaggedVariant> PostTaggedVariants => Set<PostTaggedVariant>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Annotation> Annotations => Set<Annotation>();
    public DbSet<ScheduledPost> ScheduledPosts => Set<ScheduledPost>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<BrandKit> BrandKits => Set<BrandKit>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<TrackedLink> TrackedLinks => Set<TrackedLink>();
    public DbSet<Attribution> Attributions => Set<Attribution>();
    public DbSet<AdSpend> AdSpends => Set<AdSpend>();
    public DbSet<CreativeWeeklyMetric> CreativeWeeklyMetrics => Set<CreativeWeeklyMetric>();
    public DbSet<FatigueAlert> FatigueAlerts => Set<FatigueAlert>();
    public DbSet<ReviewInvite> ReviewInvites => Set<ReviewInvite>();
    public DbSet<Review> Reviews => Set<Review>();

    // Audit
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    // AI coach
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<CoachPlan> CoachPlans => Set<CoachPlan>();
    public DbSet<RoadmapItem> RoadmapItems => Set<RoadmapItem>();
    public DbSet<CoachChatMessage> CoachChatMessages => Set<CoachChatMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money is always decimal(18,2) unless a property says otherwise with [Precision].
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // utf8mb4 so Bangla text in names, addresses and chat messages is stored correctly.
        b.HasCharSet("utf8mb4");

        // ── Relationships EF Core cannot infer on its own ──
        b.Entity<ReturnCase>(e =>
        {
            e.HasOne(r => r.Order).WithMany(o => o.ReturnCases).HasForeignKey(r => r.OrderId);
            e.HasOne(r => r.ExchangeOrder).WithMany().HasForeignKey(r => r.ExchangeOrderId);
        });
        b.Entity<FraudReport>()
            .HasOne<Order>().WithMany().HasForeignKey(f => f.OrderId);
        b.Entity<AuditEvent>()
            .HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId);

        // ── Multi-tenancy: MerchantId foreign key + global query filter on every tenant table ──
        foreach (var type in b.Model.GetEntityTypes().Select(t => t.ClrType).ToList())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(type)) continue;

            b.Entity(type).HasOne(typeof(Merchant)).WithMany()
                .HasForeignKey(nameof(ITenantEntity.MerchantId));

            GetType().GetMethod(nameof(ApplyTenantFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(type)
                .Invoke(this, [b]);
        }

        // ── Enums as readable strings ──
        foreach (var property in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (!clr.IsEnum) continue;
            property.SetProviderClrType(typeof(string));
            property.SetMaxLength(32);
        }

        // ── Delete rules: business data is never deleted by cascade, except true child rows ──
        var cascadeChildren = new HashSet<(Type Child, Type Parent)>
        {
            (typeof(OrderItem), typeof(Order)),
            (typeof(OrderStatusHistory), typeof(Order)),
            (typeof(InvoiceLine), typeof(Invoice)),
            (typeof(ProductVariant), typeof(Product)),
            (typeof(Payslip), typeof(PayrollRun)),
            (typeof(PostMedia), typeof(Post)),
            (typeof(PostTarget), typeof(Post)),
            (typeof(PostTaggedVariant), typeof(Post)),
            (typeof(SettlementLine), typeof(SettlementBatch)),
            (typeof(Thana), typeof(District)),
            (typeof(CoachPlan), typeof(BusinessProfile)),
            (typeof(RoadmapItem), typeof(CoachPlan)),
            (typeof(CoachChatMessage), typeof(CoachPlan)),
        };
        var ourNamespace = typeof(Merchant).Namespace;
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            var child = fk.DeclaringEntityType.ClrType;
            var parent = fk.PrincipalEntityType.ClrType;
            if (child.Namespace != ourNamespace) continue; // leave Identity's own tables alone

            fk.DeleteBehavior = cascadeChildren.Contains((child, parent))
                ? DeleteBehavior.Cascade
                : fk.IsRequired ? DeleteBehavior.Restrict : DeleteBehavior.SetNull;
        }

        // ── Reference data that every copy of the database needs ──
        b.Entity<IdentityRole>().HasData(ReferenceData.Roles);
        b.Entity<District>().HasData(ReferenceData.Districts);
        b.Entity<Thana>().HasData(ReferenceData.Thanas);
        b.Entity<Courier>().HasData(ReferenceData.Couriers);
        b.Entity<PlatformRule>().HasData(ReferenceData.PlatformRules);
    }

    private void ApplyTenantFilter<T>(ModelBuilder b) where T : class, ITenantEntity =>
        b.Entity<T>().HasQueryFilter((Expression<Func<T, bool>>)(e => e.MerchantId == CurrentMerchantId));

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyRowRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        ApplyRowRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// <summary>Stamps MerchantId on new tenant rows and bumps the concurrency Version on updates.</summary>
    private void ApplyRowRules()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is ITenantEntity t
                && t.MerchantId == 0 && CurrentMerchantId is int merchantId)
                t.MerchantId = merchantId;

            if (entry.State == EntityState.Modified && entry.Entity is IVersioned v)
                v.Version++;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Tenancy and staff (Features 7, 12, 13, 14, 15) ─────────────────────────

[Index(nameof(Slug), IsUnique = true)]
public class Merchant
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Slug { get; set; } = "";
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Merchant settings used by business rules
    public int LateThresholdMinutes { get; set; } = 15;           // 12
    public int HalfDayThresholdMinutes { get; set; } = 240;       // 12
    public int PaymentLinkValidHours { get; set; } = 24;          // 3
    public int HighRiskScore { get; set; } = 60;                  // 6
    public int CodRefusalLimit { get; set; } = 3;                 // 7
    public int MinParcelsForRecommendation { get; set; } = 20;    // 8
    public int TrackingLinkDaysAfterDelivery { get; set; } = 14;  // 10
    public decimal FatigueCostRisePercent { get; set; } = 20m;    // 23
    public decimal FatigueConversionDropPercent { get; set; } = 20m; // 23

    public ICollection<AppUser> Users { get; set; } = [];
}

public class AppUser : IdentityUser
{
    /// <summary>Null only for platform-level accounts and AI-coach-only users.</summary>
    public int? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    [MaxLength(150)] public string FullName { get; set; } = "";
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Shift : TenantEntity
{
    [MaxLength(60)] public string Name { get; set; } = "";
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

[Index(nameof(UserId), nameof(Date), IsUnique = true)]
public class AttendanceRecord : TenantEntity
{
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DateOnly Date { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public AttendanceStatus Status { get; set; }
    public int MinutesLate { get; set; }
    public int OvertimeMinutes { get; set; }

    // Only the owner may correct a record; the correction is also audit-logged (15)
    public string? CorrectedByUserId { get; set; }
    public AppUser? CorrectedByUser { get; set; }
    [MaxLength(300)] public string? CorrectionReason { get; set; }
}

public class SalaryStructure : TenantEntity
{
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    public decimal MonthlyBasePay { get; set; }
    public decimal OvertimeRatePerHour { get; set; }
    public int WorkingDaysPerMonth { get; set; } = 26;
    [Precision(5, 2)] public decimal CommissionPercent { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(MerchantId), nameof(PeriodYear), nameof(PeriodMonth))]
public class PayrollRun : TenantEntity
{
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public PayrollRunStatus Status { get; set; }
    public string? ApprovedByUserId { get; set; }
    public AppUser? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }

    /// <summary>An approved run is locked; a correction creates a new run that supersedes it.</summary>
    public int? SupersedesRunId { get; set; }
    public PayrollRun? SupersedesRun { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = [];
}

[Index(nameof(PayrollRunId), nameof(UserId), IsUnique = true)]
public class Payslip : TenantEntity
{
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }

    public decimal BasePay { get; set; }
    public int DaysWorked { get; set; }
    public int UnpaidAbsences { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Deductions { get; set; }
    public decimal CommissionTotal { get; set; }
    public decimal NetPay { get; set; }
}

/// <summary>Credited only when an attributed order reaches Settled (Feature 9 triggers it).</summary>
[Index(nameof(OrderId), nameof(UserId), IsUnique = true)]
public class CommissionEntry : TenantEntity
{
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public decimal Amount { get; set; }
    public CommissionStatus Status { get; set; }
    public DateTime CreditedAt { get; set; } = DateTime.UtcNow;
    public int? PayslipId { get; set; }
    public Payslip? Payslip { get; set; }
}

[Index(nameof(UserId), nameof(Date), IsUnique = true)]
public class ModeratorMetric : TenantEntity
{
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    public DateOnly Date { get; set; }
    public int MessagesHandled { get; set; }
    public int AvgResponseSeconds { get; set; }
    public int OrdersConfirmed { get; set; }
    public int OrdersSettled { get; set; }
}

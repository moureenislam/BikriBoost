using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BikriBoost.Web.Models.Entities;

// ── Geography (shared reference data, not tenant-scoped) ───────────────────

public class District
{
    public int Id { get; set; }
    [MaxLength(60)] public string NameEn { get; set; } = "";
    [MaxLength(60)] public string NameBn { get; set; } = "";
    [MaxLength(30)] public string Division { get; set; } = "";
    public ICollection<Thana> Thanas { get; set; } = [];
}

[Index(nameof(DistrictId), nameof(NameEn), IsUnique = true)]
public class Thana
{
    public int Id { get; set; }
    public int DistrictId { get; set; }
    public District? District { get; set; }
    [MaxLength(60)] public string NameEn { get; set; } = "";
    [MaxLength(60)] public string NameBn { get; set; } = "";
}

// ── Customers and conversations (Features 1, 2) ───────────────────────────

[Index(nameof(MerchantId), nameof(Phone))]
[Index(nameof(PhoneHmac))]
public class Customer : TenantEntity
{
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(20)] public string? Phone { get; set; }
    /// <summary>HMAC-SHA256 of the phone with a platform secret, used to match the fraud registry (7).</summary>
    [MaxLength(64)] public string? PhoneHmac { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public int? DistrictId { get; set; }
    public District? District { get; set; }
    public int? ThanaId { get; set; }
    public Thana? Thana { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }

    public ICollection<Conversation> Conversations { get; set; } = [];
}

/// <summary>A connected Page, Instagram account or WhatsApp number. Access tokens live in user-secrets, not here.</summary>
[Index(nameof(MerchantId), nameof(Type), nameof(ExternalId), IsUnique = true)]
public class Channel : TenantEntity
{
    public ChannelType Type { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(100)] public string ExternalId { get; set; } = "";
    public bool IsFake { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(ChannelId), nameof(ExternalThreadId), IsUnique = true)]
[Index(nameof(MerchantId), nameof(Status), nameof(LastMessageAt))]
public class Conversation : TenantEntity
{
    public int ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(150)] public string ExternalThreadId { get; set; } = "";
    public ConversationStatus Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    /// <summary>The m.me "ref" parameter Messenger passes on first contact, used for attribution (21).</summary>
    [MaxLength(64)] public string? EntryRef { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}

[Index(nameof(ConversationId), nameof(SentAt))]
[Index(nameof(MerchantId), nameof(ExternalMessageId), IsUnique = true)]
public class Message : TenantEntity
{
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public MessageDirection Direction { get; set; }
    public string Body { get; set; } = "";
    /// <summary>Unique per merchant so a repeated webhook delivery is ignored.</summary>
    [MaxLength(150)] public string? ExternalMessageId { get; set; }
    [MaxLength(500)] public string? AttachmentUrl { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set on outbound replies; response time (14) is measured from these timestamps.</summary>
    public string? SentByUserId { get; set; }
    public AppUser? SentByUser { get; set; }
    public DateTime? ReadAt { get; set; }
}

[Index(nameof(ChannelId), nameof(ExternalCommentId), IsUnique = true)]
public class CommentThread : TenantEntity
{
    public int ChannelId { get; set; }
    public Channel? Channel { get; set; }
    [MaxLength(150)] public string ExternalPostId { get; set; } = "";
    [MaxLength(150)] public string ExternalCommentId { get; set; } = "";
    [MaxLength(150)] public string AuthorName { get; set; } = "";
    [MaxLength(100)] public string? AuthorExternalId { get; set; }
    public string Body { get; set; } = "";
    public DateTime PostedAt { get; set; }
    public bool IsHidden { get; set; }
    /// <summary>Set when the comment is turned into a private chat via Messenger's private reply.</summary>
    public int? ConvertedConversationId { get; set; }
    public Conversation? ConvertedConversation { get; set; }
}

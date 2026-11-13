namespace BikriBoost.Web.Models.Entities;

// Enums are stored as strings (see AppDbContext) so rows stay readable in phpMyAdmin.

// Tenancy and staff
public enum AttendanceStatus { Present, Late, HalfDay, Absent, Leave }
public enum PayrollRunStatus { Draft, Approved, Superseded }
public enum CommissionStatus { Credited, Paid }

// Customers and conversations
public enum ChannelType { Messenger, Instagram, WhatsApp, FacebookComments }
public enum ConversationStatus { Open, Pending, Closed }
public enum MessageDirection { Inbound, Outbound }

// Catalog and stock
public enum ReservationStatus { Active, Released, Fulfilled }
public enum StockEventType { Reserved, Released, Restocked, Adjusted, LockedOut, Unlocked }

// Orders and payments
public enum OrderStatus { Pending, Confirmed, Dispatched, Delivered, Settled, Cancelled, Returned }
public enum PaymentMethod { Cod, Bkash, Nagad }
public enum PaymentProvider { Bkash, Nagad }
public enum InvoiceStatus { Draft, Sent, Paid, Cancelled }
public enum PaymentLinkStatus { Active, Paid, Expired, Cancelled }
public enum PaymentClaimSource { GatewayCallback, ManualTrx }
public enum PaymentClaimStatus { Pending, Verified, Rejected }
public enum RiskLevel { Low, Medium, High }
public enum FraudReason { CodRefused, FakeOrder, PaymentFraud }
public enum FraudReportStatus { Active, Disputed, Withdrawn }

// Logistics
public enum ShipmentStatus { Booked, PickedUp, InTransit, Delivered, Returned, Cancelled }
public enum ShipmentEventSource { Webhook, Polling, Manual }
public enum SettlementBatchStatus { Uploaded, Reviewed, Applied }
public enum DiscrepancyType { MissingParcel, ShortPayment, FeeMismatch, UnknownConsignment, ShortCollected, AlreadySettled }
public enum ReturnType { Return, Exchange }
public enum ReturnStatus { Opened, Received, Inspected, Closed }
public enum QcGrade { Pass, Damaged, Rejected }

// Marketing
public enum SocialPlatform { Facebook, Instagram }
public enum PostStatus { Draft, PendingApproval, Approved, Scheduled, Published, Held, Failed }
public enum ApprovalStatus { Draft, Pending, Approved, ChangesRequested }
public enum ScheduledPostStatus { Scheduled, Published, Held, Failed, Cancelled }
public enum MediaType { Image, Video }
public enum AssetType { Image, Video, Logo, Font, Other }
public enum BrandCheckResult { NotChecked, Pass, Fail }
public enum CouponType { Percent, Fixed }
public enum AttributionSource { Coupon, MessengerRef, Utm }
public enum AdSpendSource { Manual, CsvImport, Api }
public enum FatigueAlertStatus { Open, Acknowledged, Resolved }
public enum ReviewStatus { Pending, Approved, Rejected }

// Audit
public enum AuditAction { Create, Update, Delete, Read }

// AI coach
public enum ReadinessVerdict { StartNow, StartSmaller, NeedFirst }
public enum ChatRole { User, Assistant }

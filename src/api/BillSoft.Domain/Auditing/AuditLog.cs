using BillSoft.Domain.Common;

namespace BillSoft.Domain.Auditing;

public sealed class AuditLog : BaseEntity
{
    public Guid AuditLogId { get; set; } = CreateId();

    public Guid? RestaurantId { get; set; }

    public Guid? BranchId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? OldValueJson { get; set; }

    public string? NewValueJson { get; set; }

    public string? Reason { get; set; }

    public string? RestaurantNameSnapshot { get; set; }

    public string? BranchNameSnapshot { get; set; }

    public string? UserNameSnapshot { get; set; }

    public string? UserMobileSnapshot { get; set; }

    public string? DeviceId { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}

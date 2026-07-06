using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed class RefreshToken : BaseEntity
{
    public Guid RefreshTokenId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid? BranchId { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    public string CreatedByIp { get; set; } = string.Empty;

    public string? ReplacedByTokenHash { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string? ActiveRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset LastActivityAt { get; set; } = UtcNow();
}

using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed class UserRole : BaseEntity
{
    public Guid UserRoleId { get; set; } = CreateId();

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = UtcNow();
}

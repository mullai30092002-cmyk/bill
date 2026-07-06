using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed class RolePermission : BaseEntity
{
    public Guid RolePermissionId { get; set; } = CreateId();

    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}

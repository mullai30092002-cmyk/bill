using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed class Permission : BaseEntity
{
    public Guid PermissionId { get; set; } = CreateId();

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Module { get; set; } = string.Empty;
}

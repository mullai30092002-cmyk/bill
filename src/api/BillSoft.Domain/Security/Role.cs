using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed class Role : BaseEntity
{
    public Guid RoleId { get; set; } = CreateId();

    public Guid? RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}

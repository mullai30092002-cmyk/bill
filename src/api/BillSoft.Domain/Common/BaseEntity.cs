namespace BillSoft.Domain.Common;

public abstract class BaseEntity
{
    protected static Guid CreateId() => Guid.NewGuid();

    protected static DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    protected static DateTimeOffset ResolveUpdatedAt(DateTimeOffset? updatedAt = null)
    {
        return updatedAt ?? DateTimeOffset.UtcNow;
    }
}

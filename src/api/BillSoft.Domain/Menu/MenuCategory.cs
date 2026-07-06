using BillSoft.Domain.Common;

namespace BillSoft.Domain.Menu;

public sealed class MenuCategory : BaseEntity
{
    public Guid MenuCategoryId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public MenuCategoryStatus Status { get; set; } = MenuCategoryStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public void UpdateProfile(string name, int displayOrder, DateTimeOffset? updatedAt = null)
    {
        Name = name.Trim();
        DisplayOrder = displayOrder;
        MarkUpdated(updatedAt);
    }

    public void Activate(DateTimeOffset? updatedAt = null)
    {
        Status = MenuCategoryStatus.Active;
        MarkUpdated(updatedAt);
    }

    public void Deactivate(DateTimeOffset? updatedAt = null)
    {
        Status = MenuCategoryStatus.Inactive;
        MarkUpdated(updatedAt);
    }

    public void MarkUpdated(DateTimeOffset? updatedAt = null)
    {
        UpdatedAt = ResolveUpdatedAt(updatedAt);
    }
}

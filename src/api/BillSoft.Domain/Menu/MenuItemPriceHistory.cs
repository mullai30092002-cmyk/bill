namespace BillSoft.Domain.Menu;

public sealed class MenuItemPriceHistory
{
    public Guid MenuItemPriceHistoryId { get; set; } = Guid.NewGuid();

    public Guid MenuItemId { get; set; }

    public Guid RestaurantId { get; set; }

    public decimal OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Reason { get; set; }
}

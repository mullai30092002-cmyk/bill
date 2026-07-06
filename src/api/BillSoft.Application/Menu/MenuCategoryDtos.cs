namespace BillSoft.Application.Menu;

public sealed record MenuCategoryListResponse(
    IReadOnlyCollection<MenuCategoryDetail> Items);

public sealed record MenuCategoryDetail(
    Guid MenuCategoryId,
    Guid RestaurantId,
    string Name,
    int DisplayOrder,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateMenuCategoryRequest(
    string? Name,
    int DisplayOrder);

public sealed record UpdateMenuCategoryRequest(
    string? Name,
    int DisplayOrder);

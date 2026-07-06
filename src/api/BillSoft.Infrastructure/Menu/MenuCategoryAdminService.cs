using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Menu;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Menu;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Menu;

public sealed class MenuCategoryAdminService : IMenuCategoryAdminService
{
    private readonly BillSoftDbContext _context;

    public MenuCategoryAdminService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MenuCategoryListResponse> ListAsync(AuthUserContext currentUser, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);

        var categories = await _context.MenuCategories
            .AsNoTracking()
            .Where(category => category.RestaurantId == restaurantId)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return new MenuCategoryListResponse(categories.Select(ToDetail).ToArray());
    }

    public async Task<MenuCategoryDetail> GetAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var category = await LoadCategoryAsync(restaurantId, categoryId, cancellationToken);
        return ToDetail(category);
    }

    public async Task<MenuCategoryDetail> CreateAsync(AuthUserContext currentUser, CreateMenuCategoryRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var normalizedName = MenuServiceSupport.NormalizeRequiredText(request.Name, "Name is required.");
        await EnsureUniqueCategoryNameAsync(restaurantId, null, normalizedName, cancellationToken);

        var category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = normalizedName,
            DisplayOrder = request.DisplayOrder,
            Status = MenuCategoryStatus.Active
        };

        var detail = ToDetail(category);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.MenuCategories.Add(category);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            category: category,
            action: "MenuCategory.Created",
            reason: "Menu category created.",
            entityId: category.MenuCategoryId.ToString(),
            oldValueJson: null,
            newValueJson: MenuServiceSupport.Serialize(detail),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<MenuCategoryDetail> UpdateAsync(AuthUserContext currentUser, Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var category = await LoadTrackedCategoryAsync(restaurantId, categoryId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var before = ToDetail(category);
        var normalizedName = MenuServiceSupport.NormalizeRequiredText(request.Name, "Name is required.");

        await EnsureUniqueCategoryNameAsync(restaurantId, category.MenuCategoryId, normalizedName, cancellationToken);

        category.UpdateProfile(normalizedName, request.DisplayOrder, DateTimeOffset.UtcNow);
        var detail = ToDetail(category);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            category: category,
            action: "MenuCategory.Updated",
            reason: "Menu category updated.",
            entityId: category.MenuCategoryId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<MenuCategoryDetail> ActivateAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var category = await LoadTrackedCategoryAsync(restaurantId, categoryId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var before = ToDetail(category);

        category.Activate(DateTimeOffset.UtcNow);
        var detail = ToDetail(category);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            category: category,
            action: "MenuCategory.Activated",
            reason: "Menu category activated.",
            entityId: category.MenuCategoryId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<MenuCategoryDetail> DeactivateAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var category = await LoadTrackedCategoryAsync(restaurantId, categoryId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);

        if (await HasActiveMenuItemsAsync(restaurantId, category.MenuCategoryId, cancellationToken))
        {
            throw new InvalidOperationException("Category cannot be deactivated while active menu items exist.");
        }

        var before = ToDetail(category);

        category.Deactivate(DateTimeOffset.UtcNow);
        var detail = ToDetail(category);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            category: category,
            action: "MenuCategory.Deactivated",
            reason: "Menu category deactivated.",
            entityId: category.MenuCategoryId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    private async Task<MenuCategory> LoadCategoryAsync(Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _context.MenuCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.MenuCategoryId == categoryId && entity.RestaurantId == restaurantId, cancellationToken);

        if (category is null)
        {
            throw new KeyNotFoundException("Menu category not found.");
        }

        return category;
    }

    private async Task<MenuCategory> LoadTrackedCategoryAsync(Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _context.MenuCategories
            .SingleOrDefaultAsync(entity => entity.MenuCategoryId == categoryId && entity.RestaurantId == restaurantId, cancellationToken);

        if (category is null)
        {
            throw new KeyNotFoundException("Menu category not found.");
        }

        return category;
    }

    private async Task EnsureUniqueCategoryNameAsync(
        Guid restaurantId,
        Guid? categoryId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var candidateNames = await _context.MenuCategories
            .AsNoTracking()
            .Where(category => category.RestaurantId == restaurantId && category.MenuCategoryId != categoryId)
            .Select(category => category.Name)
            .ToListAsync(cancellationToken);

        if (candidateNames.Any(name => string.Equals(name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Category name already exists in this restaurant.");
        }
    }

    private async Task<bool> HasActiveMenuItemsAsync(Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        return await _context.MenuItems.AsNoTracking()
            .AnyAsync(item =>
                item.RestaurantId == restaurantId &&
                item.MenuCategoryId == categoryId &&
                item.Status == MenuItemStatus.Active,
                cancellationToken);
    }

    private static void ValidateRequest(CreateMenuCategoryRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static void ValidateRequest(UpdateMenuCategoryRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static MenuCategoryDetail ToDetail(MenuCategory category)
    {
        return new MenuCategoryDetail(
            category.MenuCategoryId,
            category.RestaurantId,
            category.Name,
            category.DisplayOrder,
            category.Status.ToString(),
            category.CreatedAt,
            category.UpdatedAt);
    }

    private void AddAudit(
        AuthUserContext actor,
        MenuServiceSupport.RestaurantSnapshot restaurant,
        MenuCategory category,
        string action,
        string reason,
        string entityId,
        string? oldValueJson,
        string? newValueJson,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = null,
            UserId = MenuServiceSupport.ResolveActorUserId(actor),
            Action = action,
            EntityType = "MenuCategory",
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }
}

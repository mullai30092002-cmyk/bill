using BillSoft.Application.Auth;
using BillSoft.Application.Menu;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Menu;

public sealed class MenuItemAdminService : IMenuItemAdminService
{
    private const string PriceHistoryReason = "Price updated from menu admin";

    private readonly BillSoftDbContext _context;

    public MenuItemAdminService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MenuItemListResponse> ListAsync(AuthUserContext currentUser, MenuItemListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var status = ResolveStatus(query.Status);
        var availability = ResolveAvailability(query.Availability);
        var search = MenuServiceSupport.NormalizeSearch(query.Search);
        var branchId = currentUser.BranchId;

        var itemsQuery =
            from item in _context.MenuItems.AsNoTracking()
            join category in _context.MenuCategories.AsNoTracking() on item.MenuCategoryId equals category.MenuCategoryId
            where item.RestaurantId == restaurantId && category.RestaurantId == restaurantId
            select new { item, category };

        if (query.MenuCategoryId.HasValue)
        {
            itemsQuery = itemsQuery.Where(entry => entry.item.MenuCategoryId == query.MenuCategoryId.Value);
        }

        if (status.HasValue)
        {
            itemsQuery = itemsQuery.Where(entry => entry.item.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = MenuServiceSupport.EscapeLikePattern(search);
            itemsQuery = itemsQuery.Where(entry =>
                EF.Functions.Like(entry.item.Name, $"%{searchPattern}%", "\\") ||
                (entry.item.Description != null && EF.Functions.Like(entry.item.Description, $"%{searchPattern}%", "\\")) ||
                (entry.item.Sku != null && EF.Functions.Like(entry.item.Sku, $"%{searchPattern}%", "\\")) ||
                EF.Functions.Like(entry.category.Name, $"%{searchPattern}%", "\\"));
        }

        if (availability.HasValue)
        {
            itemsQuery = availability.Value switch
            {
                MenuItemAvailabilityFilter.EatIn => itemsQuery.Where(entry => entry.item.IsAvailableForEatIn),
                MenuItemAvailabilityFilter.Parcel => itemsQuery.Where(entry => entry.item.IsAvailableForParcel),
                MenuItemAvailabilityFilter.All => itemsQuery,
                _ => itemsQuery
            };
        }

        var itemRows = await itemsQuery
            .OrderBy(entry => entry.category.DisplayOrder)
            .ThenBy(entry => entry.category.Name)
            .ThenBy(entry => entry.item.Name)
            .Select(entry => new
            {
                entry.item.MenuItemId,
                entry.item.RestaurantId,
                entry.item.MenuCategoryId,
                CategoryName = entry.category.Name,
                entry.item.Name,
                entry.item.Description,
                entry.item.Sku,
                entry.item.BasePrice,
                entry.item.TaxRate,
                entry.item.IsVegetarian,
                entry.item.IsAvailableForEatIn,
                entry.item.IsAvailableForParcel,
                entry.item.InventoryDeductionMode,
                entry.item.Status,
                entry.item.CreatedAt,
                entry.item.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);

        var stockMappingMap = await LoadStockMappingMapAsync(restaurantId, branchId, itemRows.Select(item => item.MenuItemId).ToArray(), cancellationToken);
        var items = itemRows.Select(entry =>
        {
            stockMappingMap.TryGetValue(entry.MenuItemId, out var stockMapping);
            return new MenuItemDetail(
                entry.MenuItemId,
                entry.RestaurantId,
                entry.MenuCategoryId,
                entry.CategoryName,
                entry.Name,
                entry.Description,
                entry.Sku,
                entry.BasePrice,
                entry.TaxRate,
                entry.IsVegetarian,
                entry.IsAvailableForEatIn,
                entry.IsAvailableForParcel,
                entry.InventoryDeductionMode.ToString(),
                stockMapping?.InventoryItemId,
                stockMapping?.InventoryItemName,
                entry.Status.ToString(),
                entry.CreatedAt,
                entry.UpdatedAt);
        }).ToArray();

        return new MenuItemListResponse(items);
    }

    public async Task<MenuItemDetail> GetAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var branchId = currentUser.BranchId;
        var item = await LoadItemAsync(restaurantId, itemId, cancellationToken);
        var category = await LoadCategoryAsync(restaurantId, item.MenuCategoryId, cancellationToken);
        var stockMapping = branchId.HasValue
            ? await LoadStockMappingAsync(restaurantId, branchId.Value, item.MenuItemId, cancellationToken)
            : null;
        return ToDetail(item, category.Name, stockMapping);
    }

    public async Task<MenuItemDetail> CreateAsync(AuthUserContext currentUser, CreateMenuItemRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var menuCategoryId = request.MenuCategoryId ?? throw new InvalidOperationException("Menu category is required.");
        var category = await LoadActiveCategoryAsync(restaurantId, menuCategoryId, cancellationToken);
        var normalizedName = MenuServiceSupport.NormalizeRequiredText(request.Name, "Name is required.");
        var normalizedSku = NormalizeSku(request.Sku);
        var inventoryDeductionMode = ResolveInventoryDeductionMode(request.InventoryDeductionMode);
        var branchId = currentUser.BranchId;

        ValidatePrice(request.BasePrice, request.TaxRate);
        ValidateAvailability(request.IsAvailableForEatIn, request.IsAvailableForParcel);

        await EnsureUniqueItemNameAsync(restaurantId, category.MenuCategoryId, null, normalizedName, cancellationToken);
        await EnsureUniqueSkuAsync(restaurantId, null, normalizedSku, cancellationToken);

        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            MenuCategoryId = category.MenuCategoryId,
            Name = normalizedName,
            Description = MenuServiceSupport.NormalizeOptionalText(request.Description),
            Sku = normalizedSku,
            BasePrice = request.BasePrice,
            TaxRate = request.TaxRate,
            IsVegetarian = request.IsVegetarian,
            IsAvailableForEatIn = request.IsAvailableForEatIn,
            IsAvailableForParcel = request.IsAvailableForParcel,
            InventoryDeductionMode = inventoryDeductionMode,
            Status = MenuItemStatus.Active
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.MenuItems.Add(item);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: null,
            action: "MenuItem.Created",
            reason: "Menu item created.",
            entityId: item.MenuItemId.ToString(),
            oldValueJson: null,
            newValueJson: MenuServiceSupport.Serialize(new
            {
                item.MenuItemId,
                item.Name,
                InventoryDeductionMode = inventoryDeductionMode.ToString()
            }),
            createdAt: now);

        await PersistStockMappingAsync(
            currentUser,
            restaurantId,
            branchId,
            item,
            request.StockInventoryItemId,
            inventoryDeductionMode,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(currentUser, item.MenuItemId, cancellationToken);
    }

    public async Task<MenuItemDetail> UpdateAsync(AuthUserContext currentUser, Guid itemId, UpdateMenuItemRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var item = await LoadTrackedItemAsync(restaurantId, itemId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var beforeCategory = await LoadCategoryAsync(restaurantId, item.MenuCategoryId, cancellationToken);
        var stockMappingBefore = currentUser.BranchId.HasValue
            ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
            : null;
        var before = ToDetail(item, beforeCategory.Name, stockMappingBefore);
        var targetCategoryId = request.MenuCategoryId ?? throw new InvalidOperationException("Menu category is required.");
        var targetCategory = await LoadCategoryAsync(restaurantId, targetCategoryId, cancellationToken);
        var normalizedName = MenuServiceSupport.NormalizeRequiredText(request.Name, "Name is required.");
        var normalizedSku = NormalizeSku(request.Sku);
        var inventoryDeductionMode = ResolveInventoryDeductionMode(request.InventoryDeductionMode);
        var branchId = currentUser.BranchId;

        ValidatePrice(request.BasePrice, request.TaxRate);
        ValidateAvailability(request.IsAvailableForEatIn, request.IsAvailableForParcel);

        if (item.Status == MenuItemStatus.Active)
        {
            EnsureCategoryActive(targetCategory);
        }

        await EnsureUniqueItemNameAsync(restaurantId, targetCategory.MenuCategoryId, item.MenuItemId, normalizedName, cancellationToken);
        await EnsureUniqueSkuAsync(restaurantId, item.MenuItemId, normalizedSku, cancellationToken);

        var priceChanged = item.BasePrice != request.BasePrice;

        item.UpdateProfile(
            targetCategory.MenuCategoryId,
            normalizedName,
            request.Description,
            normalizedSku,
            request.BasePrice,
            request.TaxRate,
            request.IsVegetarian,
            request.IsAvailableForEatIn,
            request.IsAvailableForParcel,
            inventoryDeductionMode,
            DateTimeOffset.UtcNow);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await PersistStockMappingAsync(
            currentUser,
            restaurantId,
            branchId,
            item,
            request.StockInventoryItemId,
            inventoryDeductionMode,
            cancellationToken);

        var after = ToDetail(
            item,
            targetCategory.Name,
            currentUser.BranchId.HasValue
                ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
                : null);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: null,
            action: "MenuItem.Updated",
            reason: "Menu item updated.",
            entityId: item.MenuItemId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(after),
            createdAt: DateTimeOffset.UtcNow);

        if (priceChanged)
        {
            _context.MenuItemPriceHistory.Add(new MenuItemPriceHistory
            {
                MenuItemId = item.MenuItemId,
                RestaurantId = restaurantId,
                OldPrice = before.BasePrice,
                NewPrice = after.BasePrice,
                ChangedByUserId = MenuServiceSupport.ResolveActorUserId(currentUser),
                ChangedAt = DateTimeOffset.UtcNow,
                Reason = PriceHistoryReason
            });

            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: null,
                action: "MenuItem.PriceChanged",
                reason: PriceHistoryReason,
                entityId: item.MenuItemId.ToString(),
                oldValueJson: MenuServiceSupport.Serialize(new { BasePrice = before.BasePrice }),
                newValueJson: MenuServiceSupport.Serialize(new { BasePrice = after.BasePrice }),
                createdAt: DateTimeOffset.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<MenuItemDetail> ActivateAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var item = await LoadTrackedItemAsync(restaurantId, itemId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var category = await LoadCategoryAsync(restaurantId, item.MenuCategoryId, cancellationToken);
        var before = ToDetail(
            item,
            category.Name,
            currentUser.BranchId.HasValue
                ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
                : null);

        item.Activate(DateTimeOffset.UtcNow);
        var stockMapping = currentUser.BranchId.HasValue
            ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
            : null;
        var after = ToDetail(item, category.Name, stockMapping);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: null,
            action: "MenuItem.Activated",
            reason: "Menu item activated.",
            entityId: item.MenuItemId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(after),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(currentUser, item.MenuItemId, cancellationToken);
    }

    public async Task<MenuItemDetail> DeactivateAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var item = await LoadTrackedItemAsync(restaurantId, itemId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var category = await LoadCategoryAsync(restaurantId, item.MenuCategoryId, cancellationToken);
        var before = ToDetail(
            item,
            category.Name,
            currentUser.BranchId.HasValue
                ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
                : null);

        item.Deactivate(DateTimeOffset.UtcNow);
        var stockMapping = currentUser.BranchId.HasValue
            ? await LoadStockMappingAsync(restaurantId, currentUser.BranchId.Value, item.MenuItemId, cancellationToken)
            : null;
        var after = ToDetail(item, category.Name, stockMapping);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: null,
            action: "MenuItem.Deactivated",
            reason: "Menu item deactivated.",
            entityId: item.MenuItemId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(after),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<MenuItemPriceHistoryResponse> GetPriceHistoryAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        _ = await LoadItemAsync(restaurantId, itemId, cancellationToken);

        var items = await _context.MenuItemPriceHistory
            .AsNoTracking()
            .Where(history => history.RestaurantId == restaurantId && history.MenuItemId == itemId)
            .Select(history => new MenuItemPriceHistoryItem(
                history.MenuItemPriceHistoryId,
                history.MenuItemId,
                history.OldPrice,
                history.NewPrice,
                history.ChangedByUserId,
                history.ChangedAt,
                history.Reason))
            .ToListAsync(cancellationToken);

        return new MenuItemPriceHistoryResponse(items.OrderByDescending(item => item.ChangedAt).ToArray());
    }

    public async Task<MenuItemRecipeResponse> GetRecipeAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var branchId = RequireBranchScope(currentUser);
        var item = await LoadItemAsync(restaurantId, itemId, cancellationToken);
        _ = await LoadBranchAsync(restaurantId, branchId, cancellationToken);

        var ingredients = await LoadRecipeIngredientsAsync(restaurantId, branchId, itemId, cancellationToken);

        return new MenuItemRecipeResponse(item.MenuItemId, item.Name, restaurantId, branchId, ingredients);
    }

    public async Task<MenuItemRecipeResponse> UpdateRecipeAsync(AuthUserContext currentUser, Guid itemId, UpdateMenuItemRecipeRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var branchId = RequireBranchScope(currentUser);
        var item = await LoadTrackedItemAsync(restaurantId, itemId, cancellationToken);
        var restaurant = await MenuServiceSupport.LoadRestaurantAsync(_context, restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchId, cancellationToken);
        ValidateRequest(request);

        var normalizedIngredients = NormalizeIngredients(request);
        var requestedInventoryItemIds = normalizedIngredients
            .Select(ingredient => ingredient.InventoryItemId)
            .Distinct()
            .ToArray();

        var inventoryItems = requestedInventoryItemIds.Length == 0
            ? []
            : await _context.InventoryItems
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchId &&
                    requestedInventoryItemIds.Contains(entity.InventoryItemId))
                .ToListAsync(cancellationToken);

        if (inventoryItems.Count != requestedInventoryItemIds.Length)
        {
            throw new InvalidOperationException("Inventory item must belong to the current restaurant and branch.");
        }

        var before = new MenuItemRecipeResponse(
            item.MenuItemId,
            item.Name,
            restaurantId,
            branchId,
            await LoadRecipeIngredientsAsync(restaurantId, branchId, itemId, cancellationToken));

        var now = DateTimeOffset.UtcNow;
        var recipeRows = normalizedIngredients.Select(ingredient => new MenuItemRecipeIngredient
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            MenuItemId = item.MenuItemId,
            InventoryItemId = ingredient.InventoryItemId,
            QuantityRequired = ingredient.QuantityRequired,
            CreatedAtUtc = now
        }).ToArray();

        var after = new MenuItemRecipeResponse(
            item.MenuItemId,
            item.Name,
            restaurantId,
            branchId,
            recipeRows
                .Select((recipeRow, index) => new MenuItemRecipeIngredientDetail(
                    recipeRow.MenuItemRecipeIngredientId,
                    recipeRow.InventoryItemId,
                    inventoryItems.Single(entity => entity.InventoryItemId == recipeRow.InventoryItemId).Name,
                    recipeRow.QuantityRequired,
                    recipeRow.CreatedAtUtc,
                    recipeRow.UpdatedAtUtc))
                .OrderBy(ingredient => ingredient.InventoryItemName)
                .ToArray());

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var existingRows = await _context.MenuItemRecipeIngredients
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.MenuItemId == item.MenuItemId)
            .ToListAsync(cancellationToken);

        if (existingRows.Count > 0)
        {
            _context.MenuItemRecipeIngredients.RemoveRange(existingRows);
        }

        _context.MenuItemRecipeIngredients.AddRange(recipeRows);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "MenuItem.RecipeUpdated",
            reason: "Menu item recipe updated.",
            entityId: item.MenuItemId.ToString(),
            oldValueJson: MenuServiceSupport.Serialize(before),
            newValueJson: MenuServiceSupport.Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    private async Task<MenuItem> LoadItemAsync(Guid restaurantId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _context.MenuItems
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.MenuItemId == itemId && entity.RestaurantId == restaurantId, cancellationToken);

        if (item is null)
        {
            throw new KeyNotFoundException("Menu item not found.");
        }

        return item;
    }

    private async Task<Branch> LoadBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return branch;
    }

    private async Task<MenuItem> LoadTrackedItemAsync(Guid restaurantId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _context.MenuItems
            .SingleOrDefaultAsync(entity => entity.MenuItemId == itemId && entity.RestaurantId == restaurantId, cancellationToken);

        if (item is null)
        {
            throw new KeyNotFoundException("Menu item not found.");
        }

        return item;
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

    private async Task<MenuCategory> LoadActiveCategoryAsync(Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _context.MenuCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.MenuCategoryId == categoryId &&
                entity.RestaurantId == restaurantId &&
                entity.Status == MenuCategoryStatus.Active,
                cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Menu category must exist and be active within the current restaurant.");
        }

        return category;
    }

    private async Task<MenuItemStockItemSnapshot?> LoadStockMappingAsync(
        Guid restaurantId,
        Guid branchId,
        Guid menuItemId,
        CancellationToken cancellationToken)
    {
        var mapping = await (
            from stockItem in _context.MenuItemStockItems.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on stockItem.InventoryItemId equals inventory.InventoryItemId
            where stockItem.RestaurantId == restaurantId &&
                  stockItem.BranchId == branchId &&
                  stockItem.MenuItemId == menuItemId &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId
            select new MenuItemStockItemSnapshot(
                stockItem.MenuItemStockItemId,
                stockItem.InventoryItemId,
                inventory.Name,
                stockItem.CreatedAtUtc,
                stockItem.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return mapping;
    }

    private async Task<Dictionary<Guid, MenuItemStockItemSnapshot>> LoadStockMappingMapAsync(
        Guid restaurantId,
        Guid? branchId,
        IReadOnlyCollection<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        if (!branchId.HasValue || menuItemIds.Count == 0)
        {
            return new Dictionary<Guid, MenuItemStockItemSnapshot>();
        }

        var mappings = await (
            from stockItem in _context.MenuItemStockItems.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on stockItem.InventoryItemId equals inventory.InventoryItemId
            where stockItem.RestaurantId == restaurantId &&
                  stockItem.BranchId == branchId.Value &&
                  menuItemIds.Contains(stockItem.MenuItemId) &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId.Value
            select new
            {
                stockItem.MenuItemId,
                Snapshot = new MenuItemStockItemSnapshot(
                    stockItem.MenuItemStockItemId,
                    stockItem.InventoryItemId,
                    inventory.Name,
                    stockItem.CreatedAtUtc,
                    stockItem.UpdatedAtUtc)
            })
            .ToListAsync(cancellationToken);

        return mappings.ToDictionary(entry => entry.MenuItemId, entry => entry.Snapshot);
    }

    private async Task PersistStockMappingAsync(
        AuthUserContext currentUser,
        Guid restaurantId,
        Guid? branchId,
        MenuItem item,
        Guid? stockInventoryItemId,
        MenuItemInventoryDeductionMode deductionMode,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var requiresStockItem = deductionMode is MenuItemInventoryDeductionMode.BatchPrepared or MenuItemInventoryDeductionMode.DirectStockItem;

        var existingMapping = branchId.HasValue
            ? await _context.MenuItemStockItems
                .SingleOrDefaultAsync(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchId.Value &&
                    entity.MenuItemId == item.MenuItemId,
                    cancellationToken)
            : null;

        if (!requiresStockItem)
        {
            if (existingMapping is not null)
            {
                _context.MenuItemStockItems.Remove(existingMapping);
            }

            return;
        }

        if (!branchId.HasValue)
        {
            throw new InvalidOperationException("Branch is required for batch prepared or direct stock item menu configuration.");
        }

        if (!stockInventoryItemId.HasValue || stockInventoryItemId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Stock inventory item is required for batch prepared or direct stock item menu configuration.");
        }

        var inventoryItem = await _context.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId.Value &&
                entity.InventoryItemId == stockInventoryItemId.Value,
                cancellationToken);

        if (inventoryItem is null)
        {
            throw new InvalidOperationException("Stock inventory item must belong to the current restaurant and branch.");
        }

        if (existingMapping is null)
        {
            existingMapping = new MenuItemStockItem
            {
                RestaurantId = restaurantId,
                BranchId = branchId.Value,
                MenuItemId = item.MenuItemId,
                InventoryItemId = stockInventoryItemId.Value,
                CreatedAtUtc = now
            };
            _context.MenuItemStockItems.Add(existingMapping);
        }
        else
        {
            existingMapping.InventoryItemId = stockInventoryItemId.Value;
            existingMapping.MarkUpdated(now);
        }
    }

    private static MenuItemDetail ToDetail(MenuItem item, string categoryName, MenuItemStockItemSnapshot? stockMapping)
    {
        return new MenuItemDetail(
            item.MenuItemId,
            item.RestaurantId,
            item.MenuCategoryId,
            categoryName,
            item.Name,
            item.Description,
            item.Sku,
            item.BasePrice,
            item.TaxRate,
            item.IsVegetarian,
            item.IsAvailableForEatIn,
            item.IsAvailableForParcel,
            item.InventoryDeductionMode.ToString(),
            stockMapping?.InventoryItemId,
            stockMapping?.InventoryItemName,
            item.Status.ToString(),
            item.CreatedAt,
            item.UpdatedAt);
    }

    private async Task EnsureUniqueItemNameAsync(
        Guid restaurantId,
        Guid? menuCategoryId,
        Guid? menuItemId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var candidateNames = await _context.MenuItems
            .AsNoTracking()
            .Where(item =>
                item.RestaurantId == restaurantId &&
                item.MenuCategoryId == menuCategoryId &&
                item.MenuItemId != menuItemId)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);

        if (candidateNames.Any(name => string.Equals(name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Item name already exists in this category.");
        }
    }

    private async Task EnsureUniqueSkuAsync(
        Guid restaurantId,
        Guid? menuItemId,
        string? normalizedSku,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedSku))
        {
            return;
        }

        var candidateSkus = await _context.MenuItems
            .AsNoTracking()
            .Where(item =>
                item.RestaurantId == restaurantId &&
                item.MenuItemId != menuItemId &&
                item.Sku != null)
            .Select(item => item.Sku!)
            .ToListAsync(cancellationToken);

        if (candidateSkus.Any(sku => string.Equals(sku.Trim(), normalizedSku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("SKU already exists in this restaurant.");
        }
    }

    private static void ValidateRequest(CreateMenuItemRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.MenuCategoryId is null)
        {
            throw new InvalidOperationException("Menu category is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static void ValidateRequest(UpdateMenuItemRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.MenuCategoryId is null)
        {
            throw new InvalidOperationException("Menu category is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static void ValidateRequest(UpdateMenuItemRecipeRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static IReadOnlyCollection<MenuItemRecipeIngredientRequest> NormalizeIngredients(UpdateMenuItemRecipeRequest request)
    {
        var ingredients = request.Ingredients ?? Array.Empty<MenuItemRecipeIngredientRequest>();

        var normalized = new List<MenuItemRecipeIngredientRequest>(ingredients.Count);
        var seenInventoryItemIds = new HashSet<Guid>();

        foreach (var ingredient in ingredients)
        {
            if (ingredient.InventoryItemId == Guid.Empty)
            {
                throw new InvalidOperationException("Inventory item is required.");
            }

            if (ingredient.QuantityRequired <= 0)
            {
                throw new InvalidOperationException("Quantity required must be greater than zero.");
            }

            if (!seenInventoryItemIds.Add(ingredient.InventoryItemId))
            {
                throw new InvalidOperationException("Duplicate recipe ingredient mappings are not allowed.");
            }

            normalized.Add(new MenuItemRecipeIngredientRequest(ingredient.InventoryItemId, ingredient.QuantityRequired));
        }

        return normalized;
    }

    private static void ValidatePrice(decimal basePrice, decimal taxRate)
    {
        if (basePrice < 0)
        {
            throw new InvalidOperationException("Base price must be greater than or equal to zero.");
        }

        if (taxRate < 0 || taxRate > 100)
        {
            throw new InvalidOperationException("Tax rate must be between 0 and 100.");
        }
    }

    private static void ValidateAvailability(bool isAvailableForEatIn, bool isAvailableForParcel)
    {
        if (!isAvailableForEatIn && !isAvailableForParcel)
        {
            throw new InvalidOperationException("At least one availability flag must be true.");
        }
    }

    private static string? NormalizeSku(string? sku)
    {
        return MenuServiceSupport.NormalizeOptionalText(sku);
    }

    private static MenuItemStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<MenuItemStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<MenuItemStatus>("Status filter"));
        }

        return parsed;
    }

    private static MenuItemAvailabilityFilter? ResolveAvailability(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
        {
            return null;
        }

        if (!Enum.TryParse<MenuItemAvailabilityFilter>(availability, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<MenuItemAvailabilityFilter>("Availability filter"));
        }

        return parsed;
    }

    private static void EnsureCategoryActive(MenuCategory category)
    {
        if (category.Status != MenuCategoryStatus.Active)
        {
            throw new InvalidOperationException("Menu category must be active within the current restaurant.");
        }
    }

    private static string BuildAllowedValuesMessage<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return $"{label} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";
    }

    private static Guid RequireBranchScope(AuthUserContext currentUser)
    {
        if (!currentUser.BranchId.HasValue)
        {
            throw new InvalidOperationException("Branch is required.");
        }

        return currentUser.BranchId.Value;
    }

    private async Task<IReadOnlyCollection<MenuItemRecipeIngredientDetail>> LoadRecipeIngredientsAsync(
        Guid restaurantId,
        Guid branchId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var ingredients = await (
            from recipe in _context.MenuItemRecipeIngredients.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on recipe.InventoryItemId equals inventory.InventoryItemId
            where recipe.RestaurantId == restaurantId &&
                  recipe.BranchId == branchId &&
                  recipe.MenuItemId == itemId &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId
            orderby inventory.Name
            select new MenuItemRecipeIngredientDetail(
                recipe.MenuItemRecipeIngredientId,
                recipe.InventoryItemId,
                inventory.Name,
                recipe.QuantityRequired,
                recipe.CreatedAtUtc,
                recipe.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return ingredients;
    }

    private static MenuItemInventoryDeductionMode ResolveInventoryDeductionMode(string? inventoryDeductionMode)
    {
        if (string.IsNullOrWhiteSpace(inventoryDeductionMode))
        {
            return MenuItemInventoryDeductionMode.RecipeOnServe;
        }

        if (!Enum.TryParse<MenuItemInventoryDeductionMode>(inventoryDeductionMode, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<MenuItemInventoryDeductionMode>("Inventory deduction mode"));
        }

        return parsed;
    }

    private void AddAudit(
        AuthUserContext actor,
        MenuServiceSupport.RestaurantSnapshot restaurant,
        Branch? branch,
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
            BranchId = branch?.BranchId,
            UserId = MenuServiceSupport.ResolveActorUserId(actor),
            Action = action,
            EntityType = "MenuItem",
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private sealed record MenuItemStockItemSnapshot(
        Guid MenuItemStockItemId,
        Guid InventoryItemId,
        string InventoryItemName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}

using BillSoft.Application.Auth;
using BillSoft.Application.Setup;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Setup;

public sealed class SetupChecklistService : ISetupChecklistService
{
    private const string Complete = "Complete";
    private const string Missing = "Missing";
    private const string Warning = "Warning";
    private const string Required = "Required";
    private const string Recommended = "Recommended";
    private const string Optional = "Optional";

    private readonly BillSoftDbContext _context;

    public SetupChecklistService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SetupChecklistResponse> GetSetupChecklistAsync(
        AuthUserContext currentUser,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == currentUser.RestaurantId, cancellationToken)
            ?? throw new KeyNotFoundException("Restaurant not found.");

        var selectedBranch = await ResolveBranchScopeAsync(currentUser, branchId, cancellationToken);
        var branchScopeId = selectedBranch?.BranchId ?? branchId ?? currentUser.BranchId;

        var activeBranchCount = await _context.Branches
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == currentUser.RestaurantId && entity.Status == BranchStatus.Active, cancellationToken);

        var activeUserCount = await _context.Users
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == currentUser.RestaurantId && entity.Status == UserStatus.Active, cancellationToken);

        var activeCategoryCount = await _context.MenuCategories
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == currentUser.RestaurantId && entity.Status == MenuCategoryStatus.Active, cancellationToken);

        var activeMenuItemCount = await _context.MenuItems
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == currentUser.RestaurantId && entity.Status == MenuItemStatus.Active, cancellationToken);

        var activeInventoryCount = branchScopeId.HasValue
            ? await _context.InventoryItems.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                entity.IsActive, cancellationToken)
            : 0;

        var activeVendorCount = await _context.Vendors
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == currentUser.RestaurantId && entity.IsActive, cancellationToken);

        var completedPosOrderCount = branchScopeId.HasValue
            ? await _context.PosOrders.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                entity.Status == PosOrderStatus.Confirmed, cancellationToken)
            : 0;

        var draftPosOrderCount = branchScopeId.HasValue
            ? await _context.PosOrders.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                entity.Status == PosOrderStatus.Draft, cancellationToken)
            : 0;

        var paidBillCount = branchScopeId.HasValue
            ? await _context.Bills.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                entity.Status == BillStatus.Paid, cancellationToken)
            : 0;

        var billPaymentCount = branchScopeId.HasValue
            ? await _context.Payments.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                entity.Status == PaymentStatus.Recorded, cancellationToken)
            : 0;

        var openBillCount = branchScopeId.HasValue
            ? await _context.Bills.AsNoTracking().CountAsync(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.BranchId == branchScopeId.Value &&
                (entity.Status == BillStatus.Unpaid || entity.Status == BillStatus.PartiallyPaid), cancellationToken)
            : 0;

        var menuItemsWithInventoryDependency = await _context.MenuItems
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == currentUser.RestaurantId && entity.Status == MenuItemStatus.Active)
            .Select(entity => new
            {
                entity.MenuItemId,
                entity.InventoryDeductionMode
            })
            .ToArrayAsync(cancellationToken);

        var configuredRecipeItemIds = branchScopeId.HasValue
            ? await _context.MenuItemRecipeIngredients
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == currentUser.RestaurantId &&
                    entity.BranchId == branchScopeId.Value)
                .Select(entity => entity.MenuItemId)
                .Distinct()
                .ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        var configuredRecipeItemLookup = configuredRecipeItemIds.ToHashSet();

        var configuredStockItemIds = branchScopeId.HasValue
            ? await _context.MenuItemStockItems
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == currentUser.RestaurantId &&
                    entity.BranchId == branchScopeId.Value)
                .Select(entity => entity.MenuItemId)
                .Distinct()
                .ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        var configuredStockItemLookup = configuredStockItemIds.ToHashSet();

        var recipeConfiguredCount = 0;
        var recipeMissingCount = 0;
        foreach (var menuItem in menuItemsWithInventoryDependency)
        {
            if (menuItem.InventoryDeductionMode == MenuItemInventoryDeductionMode.NoDeduction)
            {
                continue;
            }

            var requiresRecipe = menuItem.InventoryDeductionMode is MenuItemInventoryDeductionMode.RecipeOnServe or MenuItemInventoryDeductionMode.BatchPrepared;
            var requiresStock = menuItem.InventoryDeductionMode is MenuItemInventoryDeductionMode.DirectStockItem or MenuItemInventoryDeductionMode.BatchPrepared;
            var hasRecipeConfiguration = !requiresRecipe || configuredRecipeItemLookup.Contains(menuItem.MenuItemId);
            var hasStockConfiguration = !requiresStock || configuredStockItemLookup.Contains(menuItem.MenuItemId);
            var isConfigured = hasRecipeConfiguration && hasStockConfiguration;

            if (isConfigured)
            {
                recipeConfiguredCount++;
            }
            else
            {
                recipeMissingCount++;
            }
        }

        var items = new[]
        {
            BuildRestaurantProfileItem(restaurant),
            BuildBranchCreatedItem(activeBranchCount, selectedBranch),
            BuildStaffUsersItem(restaurant.BusinessType, activeUserCount),
            BuildMenuCategoriesItem(activeCategoryCount),
            BuildMenuItemsItem(activeMenuItemCount),
            BuildInventoryItemsItem(restaurant.BusinessType, activeInventoryCount),
            BuildRecipeMappingsItem(restaurant.BusinessType, menuItemsWithInventoryDependency.Length, recipeConfiguredCount, recipeMissingCount),
            BuildVendorsItem(restaurant.BusinessType, activeVendorCount),
            BuildPosOrderItem(completedPosOrderCount, draftPosOrderCount),
            BuildBillPaymentItem(paidBillCount, billPaymentCount, openBillCount),
        };

        var completionRelevantItems = items.Where(item => item.Priority is Required or Recommended).ToArray();
        var completedCount = completionRelevantItems.Count(item => item.Status == Complete);
        var totalCount = completionRelevantItems.Length;
        var completionPercent = totalCount == 0
            ? 0
            : (int)Math.Round(completedCount * 100m / totalCount, MidpointRounding.AwayFromZero);

        return new SetupChecklistResponse(
            restaurant.RestaurantId,
            restaurant.Name,
            restaurant.BusinessType.ToString(),
            selectedBranch?.BranchId,
            selectedBranch?.Name,
            completionPercent,
            completedCount,
            totalCount,
            items);
    }

    private async Task<Branch?> ResolveBranchScopeAsync(AuthUserContext currentUser, Guid? branchId, CancellationToken cancellationToken)
    {
        if (branchId.HasValue)
        {
            return await _context.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(entity =>
                    entity.RestaurantId == currentUser.RestaurantId &&
                    entity.BranchId == branchId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Branch not found.");
        }

        if (currentUser.BranchId.HasValue)
        {
            return await _context.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(entity =>
                    entity.RestaurantId == currentUser.RestaurantId &&
                    entity.BranchId == currentUser.BranchId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Branch not found.");
        }

        return await _context.Branches
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == currentUser.RestaurantId &&
                entity.Status == BranchStatus.Active)
            .OrderBy(entity => entity.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SetupChecklistItem BuildRestaurantProfileItem(Restaurant restaurant)
    {
        var isReady = restaurant.Status == RestaurantStatus.Active &&
            !string.IsNullOrWhiteSpace(restaurant.Name) &&
            !string.IsNullOrWhiteSpace(restaurant.RestaurantCode);

        return new SetupChecklistItem(
            "restaurantProfile",
            "Restaurant profile ready",
            GetRestaurantProfileDescription(restaurant.BusinessType),
            isReady ? Complete : Missing,
            "View setup",
            "/owner/dashboard",
            isReady ? 1 : 0,
            null,
            Required);
    }

    private static SetupChecklistItem BuildBranchCreatedItem(int activeBranchCount, Branch? selectedBranch)
    {
        if (selectedBranch is not null && selectedBranch.Status != BranchStatus.Active)
        {
            return new SetupChecklistItem(
                "branchCreated",
                "Branch created",
                "The selected branch is inactive, so pilot workflows should use an active branch.",
                Warning,
                "Add branch",
                "/admin/branches",
                activeBranchCount,
                1,
                Required);
        }

        if (activeBranchCount <= 0)
        {
            return new SetupChecklistItem(
                "branchCreated",
                "Branch created",
                "Create at least one active branch to scope pilot activity.",
                Missing,
                "Add branch",
                "/admin/branches",
                0,
                null,
                Required);
        }

        return new SetupChecklistItem(
            "branchCreated",
            "Branch created",
            "At least one active branch is available for pilot usage.",
            Complete,
            "Add branch",
            "/admin/branches",
            activeBranchCount,
            null,
            Required);
    }

    private static SetupChecklistItem BuildStaffUsersItem(RestaurantBusinessType businessType, int activeUserCount)
    {
        var priority = GetPriority(businessType, "staffUsersAdded");

        if (priority == Optional)
        {
            return new SetupChecklistItem(
                "staffUsersAdded",
                "Staff users added",
                GetStaffUsersDescription(businessType),
                activeUserCount <= 0 ? Missing : Complete,
                "Add users",
                "/admin/users",
                activeUserCount,
                null,
                priority);
        }

        if (activeUserCount <= 0)
        {
            return new SetupChecklistItem(
                "staffUsersAdded",
                "Staff users added",
                GetStaffUsersDescription(businessType),
                Missing,
                "Add users",
                "/admin/users",
                0,
                null,
                priority);
        }

        if (activeUserCount == 1)
        {
            return new SetupChecklistItem(
                "staffUsersAdded",
                "Staff users added",
                GetStaffUsersDescription(businessType),
                Warning,
                "Add users",
                "/admin/users",
                1,
                1,
                priority);
        }

        return new SetupChecklistItem(
            "staffUsersAdded",
            "Staff users added",
            GetStaffUsersDescription(businessType),
            Complete,
            "Add users",
            "/admin/users",
            activeUserCount,
            null,
            priority);
    }

    private static SetupChecklistItem BuildMenuCategoriesItem(int activeCategoryCount)
    {
        return CreateSimpleItem(
            "menuCategoriesAdded",
            "Menu categories added",
            "Add at least one active menu category before loading items.",
            "Add menu",
            "/admin/menu",
            activeCategoryCount,
            Required);
    }

    private static SetupChecklistItem BuildMenuItemsItem(int activeMenuItemCount)
    {
        return CreateSimpleItem(
            "menuItemsAdded",
            "Menu items added",
            "Create active menu items so POS orders and billing can use the catalog.",
            "Add menu",
            "/admin/menu",
            activeMenuItemCount,
            Required);
    }

    private static SetupChecklistItem BuildInventoryItemsItem(RestaurantBusinessType businessType, int activeInventoryCount)
    {
        return CreateSimpleItem(
            "inventoryItemsAdded",
            "Inventory items added",
            GetInventoryItemsDescription(businessType),
            "Add inventory",
            "/inventory",
            activeInventoryCount,
            GetPriority(businessType, "inventoryItemsAdded"));
    }

    private static SetupChecklistItem BuildRecipeMappingsItem(RestaurantBusinessType businessType, int activeMenuItemCount, int configuredCount, int missingCount)
    {
        var priority = GetPriority(businessType, "recipesOrStockMappingsConfigured");

        if (activeMenuItemCount <= 0)
        {
            return new SetupChecklistItem(
                "recipesOrStockMappingsConfigured",
                "Recipes or stock mappings configured",
                GetRecipeMappingsDescription(businessType, "Create menu items before assigning recipe or stock mappings."),
                Missing,
                "Add menu",
                "/admin/menu",
                0,
                null,
                priority);
        }

        if (configuredCount <= 0 && missingCount <= 0)
        {
            return new SetupChecklistItem(
                "recipesOrStockMappingsConfigured",
                "Recipes or stock mappings configured",
                GetRecipeMappingsDescription(businessType, "No menu items currently require inventory deduction mappings."),
                Complete,
                "Add menu",
                "/admin/menu",
                0,
                null,
                priority);
        }

        if (missingCount <= 0)
        {
            return new SetupChecklistItem(
                "recipesOrStockMappingsConfigured",
                "Recipes or stock mappings configured",
                GetRecipeMappingsDescription(businessType, "All menu items that deduct inventory have usable mappings."),
                Complete,
                "Add menu",
                "/admin/menu",
                configuredCount,
                null,
                priority);
        }

        return new SetupChecklistItem(
            "recipesOrStockMappingsConfigured",
            "Recipes or stock mappings configured",
            GetRecipeMappingsDescription(businessType, "Some menu items still need recipe or stock mappings before pilot usage."),
            Warning,
            "Add menu",
            "/admin/menu",
            configuredCount,
            missingCount,
            priority);
    }

    private static SetupChecklistItem BuildVendorsItem(RestaurantBusinessType businessType, int activeVendorCount)
    {
        return CreateSimpleItem(
            "vendorsAdded",
            "Vendors added",
            GetVendorsDescription(businessType),
            "Add vendors",
            "/vendors",
            activeVendorCount,
            GetPriority(businessType, "vendorsAdded"));
    }

    private static SetupChecklistItem BuildPosOrderItem(int completedCount, int draftCount)
    {
        if (completedCount > 0)
        {
            return new SetupChecklistItem(
                "firstPosOrderCompleted",
                "First test POS order completed",
                "At least one confirmed POS order exists for the selected branch.",
                Complete,
                "Create test order",
                "/pos/orders",
                completedCount,
                draftCount > 0 ? draftCount : null,
                Required);
        }

        if (draftCount > 0)
        {
            return new SetupChecklistItem(
                "firstPosOrderCompleted",
                "First test POS order completed",
                "POS drafts exist, but no order has been confirmed yet.",
                Warning,
                "Create test order",
                "/pos/orders",
                0,
                draftCount,
                Required);
        }

        return new SetupChecklistItem(
            "firstPosOrderCompleted",
            "First test POS order completed",
            "No POS orders have been created for this branch yet.",
            Missing,
            "Create test order",
            "/pos/orders",
            0,
            null,
            Required);
    }

    private static SetupChecklistItem BuildBillPaymentItem(int paidBillCount, int recordedPaymentCount, int openBillCount)
    {
        if (paidBillCount > 0 || recordedPaymentCount > 0)
        {
            return new SetupChecklistItem(
                "firstBillPaymentCompleted",
                "First bill/payment completed",
                "A bill has been completed or a payment has been recorded for the selected branch.",
                Complete,
                "Complete first bill",
                "/billing",
                Math.Max(paidBillCount, recordedPaymentCount),
                openBillCount > 0 ? openBillCount : null,
                Required);
        }

        if (openBillCount > 0)
        {
            return new SetupChecklistItem(
                "firstBillPaymentCompleted",
                "First bill/payment completed",
                "Bills exist, but no payment has been recorded yet.",
                Warning,
                "Complete first bill",
                "/billing",
                0,
                openBillCount,
                Required);
        }

        return new SetupChecklistItem(
            "firstBillPaymentCompleted",
            "First bill/payment completed",
            "No bills or payments have been recorded for this branch yet.",
            Missing,
            "Complete first bill",
            "/billing",
            0,
            null,
            Required);
    }

    private static SetupChecklistItem CreateSimpleItem(
        string key,
        string title,
        string description,
        string actionLabel,
        string actionHref,
        int count,
        string priority)
    {
        if (count <= 0)
        {
            return new SetupChecklistItem(key, title, description, Missing, actionLabel, actionHref, 0, null, priority);
        }

        return new SetupChecklistItem(key, title, description, Complete, actionLabel, actionHref, count, null, priority);
    }

    private static string GetPriority(RestaurantBusinessType businessType, string itemKey) =>
        (businessType, itemKey) switch
        {
            (_, "restaurantProfile") => Required,
            (_, "branchCreated") => Required,
            (RestaurantBusinessType.CafeTakeaway, "staffUsersAdded") => Optional,
            (_, "staffUsersAdded") => Recommended,
            (_, "menuCategoriesAdded") => Required,
            (_, "menuItemsAdded") => Required,
            (RestaurantBusinessType.Restaurant, "inventoryItemsAdded") => Recommended,
            (RestaurantBusinessType.JuiceShop, "inventoryItemsAdded") => Required,
            (RestaurantBusinessType.Bakery, "inventoryItemsAdded") => Required,
            (RestaurantBusinessType.DessertShop, "inventoryItemsAdded") => Required,
            (RestaurantBusinessType.CafeTakeaway, "inventoryItemsAdded") => Recommended,
            (RestaurantBusinessType.Restaurant, "recipesOrStockMappingsConfigured") => Recommended,
            (RestaurantBusinessType.JuiceShop, "recipesOrStockMappingsConfigured") => Required,
            (RestaurantBusinessType.Bakery, "recipesOrStockMappingsConfigured") => Required,
            (RestaurantBusinessType.DessertShop, "recipesOrStockMappingsConfigured") => Required,
            (RestaurantBusinessType.CafeTakeaway, "recipesOrStockMappingsConfigured") => Recommended,
            (RestaurantBusinessType.Bakery, "vendorsAdded") => Required,
            (_, "vendorsAdded") => Recommended,
            (_, "firstPosOrderCompleted") => Required,
            (_, "firstBillPaymentCompleted") => Required,
            _ => Recommended
        };

    private static string GetRestaurantProfileDescription(RestaurantBusinessType businessType) =>
        businessType switch
        {
            RestaurantBusinessType.JuiceShop => "Confirm the restaurant name, code, and business profile before pilot usage.",
            RestaurantBusinessType.Bakery => "Confirm the restaurant name, code, and business profile before pilot usage.",
            RestaurantBusinessType.DessertShop => "Confirm the restaurant name, code, and business profile before pilot usage.",
            RestaurantBusinessType.CafeTakeaway => "Confirm the restaurant name, code, and business profile before pilot usage.",
            _ => "Confirm the restaurant name and code before pilot usage."
        };

    private static string GetStaffUsersDescription(RestaurantBusinessType businessType) =>
        businessType switch
        {
            RestaurantBusinessType.CafeTakeaway => "Staff users are optional for cafe / takeaway setup, but add them when you are ready.",
            _ => "Active users are ready for restaurant operations."
        };

    private static string GetInventoryItemsDescription(RestaurantBusinessType businessType) =>
        businessType switch
        {
            RestaurantBusinessType.JuiceShop => "Add inventory items for fruit, liquids, and packaging before pilot usage.",
            RestaurantBusinessType.Bakery => "Add inventory items for ingredients, packaging, and batch production before pilot usage.",
            RestaurantBusinessType.DessertShop => "Add inventory items for ingredients, desserts, and packaging before pilot usage.",
            RestaurantBusinessType.CafeTakeaway => "Add inventory items for packaged drinks and takeaway stock before pilot usage.",
            _ => "Add at least one active inventory item for the selected branch."
        };

    private static string GetRecipeMappingsDescription(RestaurantBusinessType businessType, string fallback) =>
        businessType switch
        {
            RestaurantBusinessType.JuiceShop => "Map fresh juices to their ingredient stock before pilot usage.",
            RestaurantBusinessType.Bakery => "Map bakery items to ingredients and prepared stock before pilot usage.",
            RestaurantBusinessType.DessertShop => "Map dessert items to ingredients and prepared stock before pilot usage.",
            RestaurantBusinessType.CafeTakeaway => "Map drinks and takeaway items to recipes or stock before pilot usage.",
            _ => fallback
        };

    private static string GetVendorsDescription(RestaurantBusinessType businessType) =>
        businessType switch
        {
            RestaurantBusinessType.Bakery => "Add active vendors for ingredients, packaging, and production supplies before pilot usage.",
            _ => "Add at least one active vendor before recording purchases or settlements."
        };
}

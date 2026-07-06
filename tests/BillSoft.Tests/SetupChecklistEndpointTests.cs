using System.Net;
using System.Net.Http.Json;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillSoft.Tests;

public sealed class SetupChecklistEndpointTests
{
    [Fact]
    public async Task Checklist_Should_Return_All_Expected_Item_Keys()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal(
            new[]
            {
                "restaurantProfile",
                "branchCreated",
                "staffUsersAdded",
                "menuCategoriesAdded",
                "menuItemsAdded",
                "inventoryItemsAdded",
                "recipesOrStockMappingsConfigured",
                "vendorsAdded",
                "firstPosOrderCompleted",
                "firstBillPaymentCompleted"
            },
            payload.Items.Select(item => item.Key).ToArray());
    }

    [Fact]
    public async Task Checklist_Should_Expose_Default_Restaurant_Business_Type()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Restaurant", payload.BusinessType);
    }

    [Fact]
    public async Task Empty_New_Setup_Should_Return_Missing_And_Warning_Statuses()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Complete", GetItem(payload, "restaurantProfile").Status);
        Assert.Equal("Complete", GetItem(payload, "branchCreated").Status);
        Assert.Equal("Warning", GetItem(payload, "staffUsersAdded").Status);
        Assert.Equal("Missing", GetItem(payload, "menuCategoriesAdded").Status);
        Assert.Equal("Missing", GetItem(payload, "menuItemsAdded").Status);
        Assert.Equal("Missing", GetItem(payload, "inventoryItemsAdded").Status);
        Assert.Equal("Missing", GetItem(payload, "recipesOrStockMappingsConfigured").Status);
        Assert.Equal("Missing", GetItem(payload, "vendorsAdded").Status);
        Assert.Equal("Missing", GetItem(payload, "firstPosOrderCompleted").Status);
        Assert.Equal("Missing", GetItem(payload, "firstBillPaymentCompleted").Status);
    }

    [Fact]
    public async Task Juice_Shop_Profile_Should_Promote_Inventory_And_Recipe_Guidance()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var updateResponse = await fixture.Client.PutAsJsonAsync("/api/v1/setup/business-type", new { businessType = "JuiceShop" });
        updateResponse.EnsureSuccessStatusCode();

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("JuiceShop", payload.BusinessType);
        Assert.Equal("Required", GetItem(payload, "inventoryItemsAdded").Priority);
        Assert.Equal("Required", GetItem(payload, "recipesOrStockMappingsConfigured").Priority);
    }

    [Fact]
    public async Task Bakery_Profile_Should_Require_Vendors_Along_With_Inventory_And_Recipes()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var updateResponse = await fixture.Client.PutAsJsonAsync("/api/v1/setup/business-type", new { businessType = "Bakery" });
        updateResponse.EnsureSuccessStatusCode();

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Bakery", payload.BusinessType);
        Assert.Equal("Required", GetItem(payload, "inventoryItemsAdded").Priority);
        Assert.Equal("Required", GetItem(payload, "recipesOrStockMappingsConfigured").Priority);
        Assert.Equal("Required", GetItem(payload, "vendorsAdded").Priority);
    }

    [Fact]
    public async Task Cafe_Takeaway_Profile_Should_Keep_Staff_Users_Optional()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var updateResponse = await fixture.Client.PutAsJsonAsync("/api/v1/setup/business-type", new { businessType = "CafeTakeaway" });
        updateResponse.EnsureSuccessStatusCode();

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("CafeTakeaway", payload.BusinessType);
        Assert.Equal("Optional", GetItem(payload, "staffUsersAdded").Priority);
        Assert.Equal(9, payload.TotalCount);
    }

    [Fact]
    public async Task Branch_Created_State_Should_Mark_Branch_Item_Complete()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var branch = await context.Branches.SingleAsync(entity => entity.BranchId == seed.BranchId);
            branch.Status = BranchStatus.Inactive;
            await context.SaveChangesAsync();
        }

        var inactivePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(inactivePayload, "branchCreated").Status);

        Guid activeBranchId;
        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var branch = new Branch
            {
                RestaurantId = seed.RestaurantId,
                Name = "South Branch",
                Status = BranchStatus.Active,
                CountryCode = "SG",
                CurrencyCode = "SGD",
                TimeZoneId = "Asia/Singapore"
            };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            activeBranchId = branch.BranchId;
        }

        var activePayload = await GetChecklistAsync(fixture.Client, activeBranchId);

        Assert.Equal("Complete", GetItem(activePayload, "branchCreated").Status);
        Assert.Equal(activeBranchId, activePayload.BranchId);
        Assert.Equal("South Branch", activePayload.BranchName);
    }

    [Fact]
    public async Task Users_Menu_Inventory_And_Vendors_Should_Affect_Corresponding_Statuses()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Hot Drinks", 1, MenuCategoryStatus.Active);
        _ = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Tea", 10m, 0m, status: MenuItemStatus.Active);
        _ = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Milk", "Dairy", "l", 5m, true);

        await AddActiveUserAsync(fixture, seed.RestaurantId, seed.BranchId, "90000002");
        await AddVendorAsync(fixture, seed.RestaurantId, seed.BranchId, "Fresh Farm");

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Complete", GetItem(payload, "staffUsersAdded").Status);
        Assert.Equal("Complete", GetItem(payload, "menuCategoriesAdded").Status);
        Assert.Equal("Complete", GetItem(payload, "menuItemsAdded").Status);
        Assert.Equal("Complete", GetItem(payload, "inventoryItemsAdded").Status);
        Assert.Equal("Complete", GetItem(payload, "vendorsAdded").Status);
    }

    [Fact]
    public async Task Recipe_On_Serve_Should_Require_Only_Recipe_Ingredients()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var ingredient = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Potato", "Vegetable", "kg", 5m, true);

        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Masala Dosa",
            90m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.RecipeOnServe;
        await UpdateMenuItemAsync(fixture, item);

        var missingPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(missingPayload, "recipesOrStockMappingsConfigured").Status);

        await AddRecipeIngredientAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, ingredient.InventoryItemId);

        var completePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Complete", GetItem(completePayload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task Direct_Stock_Item_Should_Require_Only_Stock_Mapping()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var stockItem = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Potato", "Vegetable", "kg", 5m, true);

        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Poori",
            40m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.DirectStockItem;
        await UpdateMenuItemAsync(fixture, item);

        var missingPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(missingPayload, "recipesOrStockMappingsConfigured").Status);

        await AddStockMappingAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, stockItem.InventoryItemId);

        var completePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Complete", GetItem(completePayload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task Batch_Prepared_Should_Warn_When_Recipe_Is_Missing()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var stockItem = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Potato", "Vegetable", "kg", 5m, true);

        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Batch Curry",
            90m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.BatchPrepared;
        await UpdateMenuItemAsync(fixture, item);
        await AddStockMappingAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, stockItem.InventoryItemId);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Warning", GetItem(payload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task Batch_Prepared_Should_Warn_When_Stock_Mapping_Is_Missing()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var ingredient = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Potato", "Vegetable", "kg", 5m, true);

        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Batch Curry",
            90m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.BatchPrepared;
        await UpdateMenuItemAsync(fixture, item);
        await AddRecipeIngredientAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, ingredient.InventoryItemId);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Warning", GetItem(payload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task Batch_Prepared_Should_Be_Complete_Only_When_Both_Recipe_And_Stock_Mapping_Exist()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var ingredient = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Potato", "Vegetable", "kg", 5m, true);
        var stockItem = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Prepared Curry", "Prepared", "portion", 5m, true);

        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Batch Curry",
            90m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.BatchPrepared;
        await UpdateMenuItemAsync(fixture, item);

        var missingPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(missingPayload, "recipesOrStockMappingsConfigured").Status);

        await AddRecipeIngredientAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, ingredient.InventoryItemId);
        var recipeOnlyPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(recipeOnlyPayload, "recipesOrStockMappingsConfigured").Status);

        await AddStockMappingAsync(fixture, seed.RestaurantId, seed.BranchId, item.MenuItemId, stockItem.InventoryItemId);
        var completePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Complete", GetItem(completePayload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task No_Deduction_Should_Not_Require_Recipe_Or_Stock_Mapping()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Curries", 1, MenuCategoryStatus.Active);
        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Water",
            10m,
            0m,
            status: MenuItemStatus.Active);

        item.InventoryDeductionMode = MenuItemInventoryDeductionMode.NoDeduction;
        await UpdateMenuItemAsync(fixture, item);

        var payload = await GetChecklistAsync(fixture.Client, seed.BranchId);

        Assert.Equal("Complete", GetItem(payload, "recipesOrStockMappingsConfigured").Status);
    }

    [Fact]
    public async Task Confirmed_Pos_Order_Should_Mark_First_Test_Order_Complete()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        await InsertPosOrderAsync(fixture, seed.RestaurantId, seed.BranchId, PosOrderStatus.Draft);

        var draftPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(draftPayload, "firstPosOrderCompleted").Status);

        await InsertPosOrderAsync(fixture, seed.RestaurantId, seed.BranchId, PosOrderStatus.Confirmed);

        var confirmedPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        var item = GetItem(confirmedPayload, "firstPosOrderCompleted");

        Assert.Equal("Complete", item.Status);
        Assert.Equal(1, item.Count);
    }

    [Fact]
    public async Task Bill_And_Payment_Should_Mark_First_Bill_Payment_Complete()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var draftOrder = await InsertPosOrderAsync(fixture, seed.RestaurantId, seed.BranchId, PosOrderStatus.Confirmed);
        var unpaidBill = await InsertBillAsync(fixture, seed.RestaurantId, seed.BranchId, draftOrder.PosOrderId, BillStatus.Unpaid, 120m, 0m, 120m);

        var warningPayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Warning", GetItem(warningPayload, "firstBillPaymentCompleted").Status);

        await InsertPaymentAsync(fixture, seed.RestaurantId, seed.BranchId, unpaidBill.BillId, PaymentStatus.Recorded, 120m);

        var completePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        var item = GetItem(completePayload, "firstBillPaymentCompleted");

        Assert.Equal("Complete", item.Status);
        Assert.Equal(1, item.Count);
    }

    [Fact]
    public async Task Branch_Scoping_Should_Be_Respected_For_Branch_Specific_Setup_Items()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        Guid secondBranchId;
        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var branch = new Branch
            {
                RestaurantId = seed.RestaurantId,
                Name = "North Branch",
                Status = BranchStatus.Active,
                CountryCode = "SG",
                CurrencyCode = "SGD",
                TimeZoneId = "Asia/Singapore"
            };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            secondBranchId = branch.BranchId;
        }

        await AddActiveUserAsync(fixture, seed.RestaurantId, seed.BranchId, "90000002");
        await fixture.InsertCategoryAsync(seed.RestaurantId, "Starters", 1, MenuCategoryStatus.Active);
        await fixture.InsertItemAsync(seed.RestaurantId, (await GetCategoryAsync(fixture, seed.RestaurantId)).MenuCategoryId, "Soup", 50m, 0m, status: MenuItemStatus.Active);
        await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Onion", "Vegetable", "kg", 2m, true);
        await AddVendorAsync(fixture, seed.RestaurantId, seed.BranchId, "Branch One Vendor");
        await InsertPosOrderAsync(fixture, seed.RestaurantId, seed.BranchId, PosOrderStatus.Confirmed);
        var branchOneOrder = await InsertPosOrderAsync(fixture, seed.RestaurantId, seed.BranchId, PosOrderStatus.Confirmed);
        await InsertBillAsync(fixture, seed.RestaurantId, seed.BranchId, branchOneOrder.PosOrderId, BillStatus.Paid, 80m, 80m, 0m);

        await AddActiveUserAsync(fixture, seed.RestaurantId, secondBranchId, "90000003");
        await fixture.InsertInventoryItemAsync(seed.RestaurantId, secondBranchId, "Potato", "Vegetable", "kg", 2m, true);
        await AddVendorAsync(fixture, seed.RestaurantId, secondBranchId, "Branch Two Vendor");
        await InsertPosOrderAsync(fixture, seed.RestaurantId, secondBranchId, PosOrderStatus.Draft);

        var branchOnePayload = await GetChecklistAsync(fixture.Client, seed.BranchId);
        Assert.Equal("Complete", GetItem(branchOnePayload, "inventoryItemsAdded").Status);
        Assert.Equal("Complete", GetItem(branchOnePayload, "firstPosOrderCompleted").Status);
        Assert.Equal("Complete", GetItem(branchOnePayload, "firstBillPaymentCompleted").Status);

        var branchTwoPayload = await GetChecklistAsync(fixture.Client, secondBranchId);
        Assert.Equal("Warning", GetItem(branchTwoPayload, "firstPosOrderCompleted").Status);
        Assert.Equal("Missing", GetItem(branchTwoPayload, "firstBillPaymentCompleted").Status);
    }

    [Fact]
    public async Task Unauthorized_User_Should_Not_Access_The_Checklist()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Waiter");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync($"/api/v1/setup/checklist?branchId={seed.BranchId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Setup_Business_Type_Update_Should_Require_Branch_Or_User_Manage()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Waiter");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PutAsJsonAsync("/api/v1/setup/business-type", new { businessType = "Bakery" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<SetupChecklistResponseDto> GetChecklistAsync(HttpClient client, Guid branchId)
    {
        var response = await client.GetAsync($"/api/v1/setup/checklist?branchId={branchId}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SetupChecklistResponseDto>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static SetupChecklistItemDto GetItem(SetupChecklistResponseDto payload, string key) =>
        Assert.Single(payload.Items, item => item.Key == key);

    private static async Task<User> AddActiveUserAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, string mobileNumber)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var user = new User
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            FullName = $"Support {mobileNumber}",
            Status = UserStatus.Active
        };
        user.SetMobileNumber("SG", mobileNumber);

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Vendor> AddVendorAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid? branchId, string name)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var vendor = new Vendor
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            Name = name,
            NormalizedName = Vendor.NormalizeKey(name),
            VendorType = VendorType.Other,
            IsActive = true
        };

        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();
        return vendor;
    }

    private static async Task<MenuCategory> GetCategoryAsync(MenuAdminApiFactory fixture, Guid restaurantId)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        return await context.MenuCategories.FirstAsync(entity => entity.RestaurantId == restaurantId && entity.Status == MenuCategoryStatus.Active);
    }

    private static async Task UpdateMenuItemAsync(MenuAdminApiFactory fixture, MenuItem item)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        context.MenuItems.Update(item);
        await context.SaveChangesAsync();
    }

    private static async Task AddRecipeIngredientAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, Guid menuItemId, Guid inventoryItemId)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        context.MenuItemRecipeIngredients.Add(new MenuItemRecipeIngredient
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            MenuItemId = menuItemId,
            InventoryItemId = inventoryItemId,
            QuantityRequired = 1m
        });

        await context.SaveChangesAsync();
    }

    private static async Task AddStockMappingAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, Guid menuItemId, Guid inventoryItemId)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        context.MenuItemStockItems.Add(new MenuItemStockItem
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            MenuItemId = menuItemId,
            InventoryItemId = inventoryItemId
        });

        await context.SaveChangesAsync();
    }

    private static async Task<PosOrder> InsertPosOrderAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, PosOrderStatus status)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var order = new PosOrder
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..16],
            OrderType = PosOrderType.EatIn,
            Status = status,
            Subtotal = 100m,
            TaxTotal = 0m,
            GrandTotal = 100m
        };

        context.PosOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task<Bill> InsertBillAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, Guid posOrderId, BillStatus status, decimal grandTotal, decimal amountPaid, decimal balanceDue)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var bill = new Bill
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            PosOrderId = posOrderId,
            BillNumber = $"BILL-{Guid.NewGuid():N}"[..17],
            BusinessDate = DateTime.UtcNow.Date,
            Status = status,
            Subtotal = grandTotal,
            TaxTotal = 0m,
            GrandTotal = grandTotal,
            AmountPaid = amountPaid,
            BalanceDue = balanceDue
        };

        context.Bills.Add(bill);
        await context.SaveChangesAsync();
        return bill;
    }

    private static async Task<Payment> InsertPaymentAsync(MenuAdminApiFactory fixture, Guid restaurantId, Guid branchId, Guid billId, PaymentStatus status, decimal amount)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var payment = new Payment
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            BillId = billId,
            PaymentNumber = $"PAY-{Guid.NewGuid():N}"[..17],
            PaymentMode = PaymentMode.Cash,
            Status = status,
            Amount = amount
        };

        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment;
    }

    private sealed record SetupChecklistResponseDto(
        Guid RestaurantId,
        string RestaurantName,
        string BusinessType,
        Guid? BranchId,
        string? BranchName,
        int CompletionPercent,
        int CompletedCount,
        int TotalCount,
        SetupChecklistItemDto[] Items);

    private sealed record SetupChecklistItemDto(
        string Key,
        string Title,
        string Description,
        string Status,
        string ActionLabel,
        string ActionHref,
        int? Count,
        int? WarningCount,
        string Priority);
}

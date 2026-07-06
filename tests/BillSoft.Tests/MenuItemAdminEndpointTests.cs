using System.Net;
using System.Net.Http.Json;
using BillSoft.Application.Menu;
using BillSoft.Domain.Menu;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillSoft.Tests;

public sealed class MenuItemAdminEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/items");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Menu_Permissions()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("AccountsUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/items");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MenuItem_View_Should_Allow_List_And_Detail()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var viewSeed = await fixture.SeedUserInRestaurantAsync(seed.RestaurantId, seed.BranchId, "Cashier", "90000035");
        await fixture.AuthenticateAsync(viewSeed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var list = await fixture.Client.GetAsync("/api/v1/admin/menu/items");
        list.EnsureSuccessStatusCode();
        var listPayload = await list.Content.ReadFromJsonAsync<MenuItemListResponseDto>();
        Assert.NotNull(listPayload);
        Assert.NotEmpty(listPayload!.Items);

        var detail = await fixture.Client.GetAsync($"/api/v1/admin/menu/items/{item.MenuItemId}");
        detail.EnsureSuccessStatusCode();
        var detailPayload = await detail.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(detailPayload);
        Assert.Equal(item.MenuItemId, detailPayload!.MenuItemId);
        Assert.Equal(category.MenuCategoryId, detailPayload.MenuCategoryId);
    }

    [Fact]
    public async Task List_Invalid_Status_Filter_Should_Return_400_With_All_Allowed_Status_Names()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/items?status=Archived");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);

        var expectedStatuses = Enum.GetNames<MenuItemStatus>();
        Assert.Equal($"Status filter must be one of: {string.Join(", ", expectedStatuses)}.", problem!.Detail);
        foreach (var status in expectedStatuses)
        {
            Assert.Contains(status, problem.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task List_Invalid_Availability_Filter_Should_Return_400_With_All_Allowed_Names()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/items?availability=Delivery");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);

        var expectedFilters = Enum.GetNames<MenuItemAvailabilityFilter>();
        Assert.Equal($"Availability filter must be one of: {string.Join(", ", expectedFilters)}.", problem!.Detail);
        foreach (var filter in expectedFilters)
        {
            Assert.Contains(filter, problem.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task MenuItem_Manage_Should_Allow_Item_Create()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.RestaurantId, payload!.RestaurantId);
        Assert.Equal(category.MenuCategoryId, payload.MenuCategoryId);
        Assert.Equal("Idli", payload.Name);
        Assert.Equal(2.50m, payload.BasePrice);
        Assert.Equal("RecipeOnServe", payload.InventoryDeductionMode);
        Assert.Null(payload.StockInventoryItemId);
        Assert.Equal("Active", payload.Status);
    }

    [Fact]
    public async Task MenuItem_Manage_Should_Allow_Setting_Inventory_Deduction_Mode_And_Stock_Item()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var stockItem = await fixture.InsertInventoryItemAsync(seed.RestaurantId, seed.BranchId, "Prepared Idli", "Prepared", "pcs", 5m, true);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true,
            inventoryDeductionMode = "BatchPrepared",
            stockInventoryItemId = stockItem.InventoryItemId
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("BatchPrepared", created!.InventoryDeductionMode);
        Assert.Equal(stockItem.InventoryItemId, created.StockInventoryItemId);

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/admin/menu/items/{created.MenuItemId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("BatchPrepared", detail!.InventoryDeductionMode);
        Assert.Equal(stockItem.InventoryItemId, detail.StockInventoryItemId);
    }

    [Fact]
    public async Task Create_Should_Use_Current_Restaurant()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            restaurantId = foreign.RestaurantId,
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.RestaurantId, payload!.RestaurantId);
        Assert.NotEqual(foreign.RestaurantId, payload.RestaurantId);
    }

    [Fact]
    public async Task Create_Should_Reject_Category_From_Another_Restaurant()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = foreignCategory.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Blank_Name()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "   ",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Negative_Price()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = -1m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Create_Should_Reject_Tax_Rate_Outside_Range(decimal taxRate)
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_When_Both_Availability_Flags_Are_False()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = false,
            isAvailableForParcel = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Name_In_Same_Category_Case_Insensitive()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        first.EnsureSuccessStatusCode();

        fixture.SqlCapture.Clear();
        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = " idli ",
            description = "Soft rice cakes again",
            sku = "IDLI2",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.DoesNotContain(fixture.SqlCapture.Commands, command =>
            command.Contains("UPPER(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_Should_Allow_Same_Name_In_Different_Categories()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var breakfast = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var meals = await fixture.InsertCategoryAsync(seed.RestaurantId, "Meals", 2);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = breakfast.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI-1",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = meals.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI-2",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Should_Allow_Same_Name_In_Different_Restaurants()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var currentCategory = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);

        var current = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = currentCategory.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI-1",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        current.EnsureSuccessStatusCode();

        await fixture.AuthenticateAsync(foreign);
        var other = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = foreignCategory.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI-2",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        other.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_SKU_In_Same_Restaurant_Case_Insensitive()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var breakfast = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var meals = await fixture.InsertCategoryAsync(seed.RestaurantId, "Meals", 2);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = breakfast.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = meals.MenuCategoryId,
            name = "Vada",
            description = "Fried snack",
            sku = " idli ",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Allow_Same_SKU_In_Different_Restaurants()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var currentCategory = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);

        var current = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = currentCategory.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "COMMON-SKU",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        current.EnsureSuccessStatusCode();

        await fixture.AuthenticateAsync(foreign);
        var other = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = foreignCategory.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "common-sku",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        other.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Update_Should_Change_Profile_Fields()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var breakfast = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var meals = await fixture.InsertCategoryAsync(seed.RestaurantId, "Meals", 2);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{item.MenuItemId}", new
        {
            menuCategoryId = meals.MenuCategoryId,
            name = "Masala Idli",
            description = "Spiced rice cakes",
            sku = "IDLI-NEW",
            basePrice = 3.00m,
            taxRate = 5m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = false
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(meals.MenuCategoryId, payload!.MenuCategoryId);
        Assert.Equal("Masala Idli", payload.Name);
        Assert.Equal("Spiced rice cakes", payload.Description);
        Assert.Equal("IDLI-NEW", payload.Sku);
        Assert.Equal(3.00m, payload.BasePrice);
        Assert.Equal(5m, payload.TaxRate);
        Assert.False(payload.IsAvailableForParcel);
        Assert.NotEqual(default, payload.UpdatedAt);
    }

    [Fact]
    public async Task Update_Price_Should_Write_Price_History()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, sku: "IDLI");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{item.MenuItemId}", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 3.00m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        response.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var history = await context.Set<MenuItemPriceHistory>()
            .AsNoTracking()
            .Where(entry => entry.MenuItemId == item.MenuItemId && entry.RestaurantId == seed.RestaurantId)
            .ToListAsync();

        Assert.Single(history);
        Assert.Equal(2.50m, history[0].OldPrice);
        Assert.Equal(3.00m, history[0].NewPrice);
        Assert.Equal("Price updated from menu admin", history[0].Reason);
    }

    [Fact]
    public async Task Update_With_Same_Price_Should_Not_Write_Price_History()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, sku: "IDLI");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{item.MenuItemId}", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        response.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var history = await context.Set<MenuItemPriceHistory>()
            .AsNoTracking()
            .Where(entry => entry.MenuItemId == item.MenuItemId && entry.RestaurantId == seed.RestaurantId)
            .ToListAsync();

        Assert.Empty(history);
    }

    [Fact]
    public async Task Price_History_Should_Be_Scoped_To_Current_Restaurant()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);
        var currentItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, sku: "IDLI");
        var foreignItem = await fixture.InsertItemAsync(foreign.RestaurantId, foreignCategory.MenuCategoryId, "Idli", basePrice: 2.50m, sku: "IDLI-FOREIGN");

        await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{currentItem.MenuItemId}", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 3.00m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        await fixture.AuthenticateAsync(foreign);
        var history = await fixture.Client.GetAsync($"/api/v1/admin/menu/items/{foreignItem.MenuItemId}/price-history");

        history.EnsureSuccessStatusCode();
        var payload = await history.Content.ReadFromJsonAsync<MenuItemPriceHistoryResponseDto>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Items);
    }

    [Fact]
    public async Task Get_Should_Return_404_For_Other_Restaurant_Item()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);
        var foreignItem = await fixture.InsertItemAsync(foreign.RestaurantId, foreignCategory.MenuCategoryId, "Idli", sku: "IDLI-FOREIGN");

        var response = await fixture.Client.GetAsync($"/api/v1/admin/menu/items/{foreignItem.MenuItemId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Activate_And_Deactivate_Should_Work()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", status: MenuItemStatus.Inactive);

        var activate = await fixture.Client.PostAsync($"/api/v1/admin/menu/items/{item.MenuItemId}/activate", null);
        activate.EnsureSuccessStatusCode();

        var deactivate = await fixture.Client.PostAsync($"/api/v1/admin/menu/items/{item.MenuItemId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "MenuItem" && log.EntityId == item.MenuItemId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("MenuItem.Activated", actions);
        Assert.Contains("MenuItem.Deactivated", actions);
    }

    [Fact]
    public async Task Successful_Mutations_Should_Write_Audit_Rows()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<MenuItemDetailDto>();
        Assert.NotNull(created);

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{created!.MenuItemId}", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli Deluxe",
            description = "Soft rice cakes deluxe",
            sku = "IDLI",
            basePrice = 3.00m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        update.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "MenuItem" && log.EntityId == created.MenuItemId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("MenuItem.Created", actions);
        Assert.Contains("MenuItem.Updated", actions);
        Assert.Contains("MenuItem.PriceChanged", actions);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/admin/menu/items")]
    [InlineData("DELETE", "/api/v1/admin/menu/items/{id}")]
    public async Task Delete_Should_Not_Exist(string method, string pathTemplate)
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var path = pathTemplate.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase);
        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Duplicate_Name_Path_Should_Not_Use_Ef_Side_Upper()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = "Idli",
            description = "Soft rice cakes",
            sku = "IDLI-1",
            basePrice = 2.50m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });
        first.EnsureSuccessStatusCode();

        fixture.SqlCapture.Clear();
        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/items", new
        {
            menuCategoryId = category.MenuCategoryId,
            name = " idli ",
            description = "Soft rice cakes again",
            sku = "IDLI-2",
            basePrice = 2.75m,
            taxRate = 0m,
            isVegetarian = true,
            isAvailableForEatIn = true,
            isAvailableForParcel = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.DoesNotContain(fixture.SqlCapture.Commands, command =>
            command.Contains("UPPER(", StringComparison.OrdinalIgnoreCase));
    }
}

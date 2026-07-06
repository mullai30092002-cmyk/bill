using System.Net;
using System.Net.Http.Json;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillSoft.Tests;

public sealed class MenuItemRecipeEndpointTests
{
    [Fact]
    public async Task Put_Should_Save_And_Get_Recipe_Ingredients_For_The_Current_Branch()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var menuItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
        var inventoryItem = await InsertInventoryItemAsync(fixture, seed.RestaurantId, seed.BranchId, "Rice", "Grains");

        var putResponse = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe", new
        {
            ingredients = new[]
            {
                new
                {
                    inventoryItemId = inventoryItem.InventoryItemId,
                    quantityRequired = 0.25m
                }
            }
        });

        putResponse.EnsureSuccessStatusCode();
        var putPayload = await putResponse.Content.ReadFromJsonAsync<MenuItemRecipeResponseDto>();
        Assert.NotNull(putPayload);
        Assert.Single(putPayload!.Ingredients);
        Assert.Equal(inventoryItem.InventoryItemId, putPayload.Ingredients[0].InventoryItemId);
        Assert.Equal(0.25m, putPayload.Ingredients[0].QuantityRequired);

        var getResponse = await fixture.Client.GetAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe");
        getResponse.EnsureSuccessStatusCode();
        var getPayload = await getResponse.Content.ReadFromJsonAsync<MenuItemRecipeResponseDto>();
        Assert.NotNull(getPayload);
        Assert.Single(getPayload!.Ingredients);
        Assert.Equal(inventoryItem.InventoryItemId, getPayload.Ingredients[0].InventoryItemId);
    }

    [Fact]
    public async Task Put_Should_Reject_Duplicate_Ingredient_Mappings_For_The_Same_Menu_Item()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var menuItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
        var inventoryItem = await InsertInventoryItemAsync(fixture, seed.RestaurantId, seed.BranchId, "Rice", "Grains");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe", new
        {
            ingredients = new[]
            {
                new
                {
                    inventoryItemId = inventoryItem.InventoryItemId,
                    quantityRequired = 0.25m
                },
                new
                {
                    inventoryItemId = inventoryItem.InventoryItemId,
                    quantityRequired = 0.50m
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Put_Should_Reject_Non_Positive_Ingredient_Quantity(decimal quantityRequired)
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var menuItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
        var inventoryItem = await InsertInventoryItemAsync(fixture, seed.RestaurantId, seed.BranchId, "Rice", "Grains");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe", new
        {
            ingredients = new[]
            {
                new
                {
                    inventoryItemId = inventoryItem.InventoryItemId,
                    quantityRequired
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_Should_Reject_Cross_Restaurant_Ingredient_Mappings()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var menuItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
        var foreignInventoryItem = await InsertInventoryItemAsync(fixture, foreign.RestaurantId, foreign.BranchId, "Rice", "Grains");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe", new
        {
            ingredients = new[]
            {
                new
                {
                    inventoryItemId = foreignInventoryItem.InventoryItemId,
                    quantityRequired = 0.25m
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_Should_Reject_Cross_Branch_Ingredient_Mappings()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var foreignBranch = await InsertBranchAsync(fixture, seed.RestaurantId, "Second Branch");
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var menuItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
        var foreignInventoryItem = await InsertInventoryItemAsync(fixture, seed.RestaurantId, foreignBranch.BranchId, "Rice", "Grains");

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/items/{menuItem.MenuItemId}/recipe", new
        {
            ingredients = new[]
            {
                new
                {
                    inventoryItemId = foreignInventoryItem.InventoryItemId,
                    quantityRequired = 0.25m
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<InventoryItem> InsertInventoryItemAsync(
        MenuAdminApiFactory fixture,
        Guid restaurantId,
        Guid branchId,
        string name,
        string category)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var now = DateTimeOffset.UtcNow;

        var inventoryItem = new InventoryItem
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            Name = name.Trim(),
            NormalizedName = name.Trim().ToUpperInvariant(),
            Category = category.Trim(),
            UnitOfMeasure = "kg",
            LowStockThreshold = 1m,
            IsActive = true,
            CreatedAtUtc = now
        };

        context.InventoryItems.Add(inventoryItem);
        await context.SaveChangesAsync();
        return inventoryItem;
    }

    private static async Task<Branch> InsertBranchAsync(MenuAdminApiFactory fixture, Guid restaurantId, string name)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var branch = new Branch
        {
            RestaurantId = restaurantId,
            Name = name,
            Status = BranchStatus.Active,
            Timezone = "Asia/Singapore",
            Currency = "SGD"
        };

        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        return branch;
    }

    private sealed record MenuItemRecipeResponseDto(MenuItemRecipeIngredientDto[] Ingredients);

    private sealed record MenuItemRecipeIngredientDto(
        Guid MenuItemRecipeIngredientId,
        Guid MenuItemId,
        Guid InventoryItemId,
        decimal QuantityRequired,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}

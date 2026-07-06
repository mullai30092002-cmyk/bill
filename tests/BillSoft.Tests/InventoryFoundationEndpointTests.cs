using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Auth;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BillSoft.Tests;

public sealed class InventoryFoundationEndpointTests
{
    [Fact]
    public async Task Create_Should_Allow_An_Admin_To_Add_An_Inventory_Item()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = admin.BranchId,
            name = "Rice",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<InventoryItemDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(admin.BranchId, payload.BranchId);
        Assert.Equal("Rice", payload.Name);
        Assert.Equal("RICE", payload.NormalizedName);
        Assert.Equal("Grains", payload.Category);
        Assert.Equal("kg", payload.UnitOfMeasure);
        Assert.Equal(10m, payload.LowStockThreshold);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Name_In_Same_Branch_Case_Insensitive()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = admin.BranchId,
            name = "Rice",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });
        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = admin.BranchId,
            name = " rice ",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Allow_Same_Name_In_Different_Branches()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedBranchlessSystemUserAsync("Admin");
        var firstBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "First Branch");
        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");
        await fixture.AuthenticateAsync(admin);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = firstBranch.BranchId,
            name = "Rice",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });
        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = secondBranch.BranchId,
            name = "Rice",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });

        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Stock_In_Should_Increase_Current_Stock()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        var movement = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry",
            unitCost = 3.25m,
            referenceNumber = "PO-1",
            notes = "Initial stock"
        });

        movement.EnsureSuccessStatusCode();
        var movementPayload = await movement.Content.ReadFromJsonAsync<InventoryMovementItemDto>();
        Assert.NotNull(movementPayload);
        Assert.Equal("Manual purchase entry", movementPayload!.Reason);
        Assert.Equal(0m, movementPayload.PreviousStock);
        Assert.Equal(10m, movementPayload.Delta);
        Assert.Equal(10m, movementPayload.ResultingStock);
        var list = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={admin.BranchId}");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<InventoryItemListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(10m, payload!.Items.Single().CurrentStock);
    }

    [Fact]
    public async Task Adjustment_Decrease_Should_Reduce_Current_Stock()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry"
        });

        var decrease = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 3m,
            reason = "Damaged/wastage"
        });

        decrease.EnsureSuccessStatusCode();
        var list = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={admin.BranchId}");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<InventoryItemListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(7m, payload!.Items.Single().CurrentStock);
    }

    [Fact]
    public async Task Negative_Stock_Result_Should_Be_Rejected()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry"
        });

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 3m,
            reason = "Damaged/wastage"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_Item_Should_Reject_New_Movements()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, false);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Manual purchase entry"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Low_Stock_Status_Should_Be_Calculated_Correctly()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 5m, true);

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 3m,
            reason = "Manual purchase entry"
        });

        var list = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={admin.BranchId}");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<InventoryItemListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("Low stock", payload!.Items.Single().Status);
    }

    [Fact]
    public async Task Out_Of_Stock_Status_Should_Be_Calculated_Correctly()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 5m, true);

        var list = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={admin.BranchId}");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<InventoryItemListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("Out of stock", payload!.Items.Single().Status);
    }

    [Fact]
    public async Task Movement_History_Should_Be_Scoped_To_Current_Item()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var firstItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);
        var secondItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Oil", "Cooking", "l", 5m, true);

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{firstItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry"
        });

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{secondItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 5m,
            reason = "Manual purchase entry"
        });

        var response = await fixture.Client.GetAsync($"/api/v1/inventory/items/{firstItem.InventoryItemId}/movements");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<InventoryMovementListResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items);
        Assert.All(payload.Items, movement => Assert.Equal(firstItem.InventoryItemId, movement.InventoryItemId));
    }

    [Fact]
    public async Task Cross_Restaurant_Access_Should_Be_Rejected()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(admin);

        var foreignItem = await fixture.InsertInventoryItemAsync(foreign.RestaurantId, foreign.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.GetAsync($"/api/v1/inventory/items/{foreignItem.InventoryItemId}/movements");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Item_Create_And_Update_Should_Write_Audit_Rows()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            branchId = admin.BranchId,
            name = "Rice",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 10m,
            isActive = true
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<InventoryItemListItemDto>();
        Assert.NotNull(created);

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/inventory/items/{created!.InventoryItemId}", new
        {
            name = "Rice Premium",
            category = "Grains",
            unitOfMeasure = "kg",
            lowStockThreshold = 12m,
            isActive = true
        });
        update.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "InventoryItem" && log.EntityId == created.InventoryItemId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("InventoryItem.Created", actions);
        Assert.Contains("InventoryItem.Updated", actions);
    }

    [Fact]
    public async Task Stock_Movement_Should_Write_Audit_Row()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry"
        });
        response.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "InventoryMovement")
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("InventoryMovement.Recorded", actions);
    }

    [Fact]
    public async Task Adjustment_Should_Require_A_Reason()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adjustment_Should_Reject_Inventory_View_Only_Users()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var inventoryUser = await fixture.SeedSystemUserAsync("InventoryUser");
        await fixture.AuthenticateAsync(inventoryUser);

        var item = await fixture.InsertInventoryItemAsync(inventoryUser.RestaurantId, inventoryUser.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Opening stock correction"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Adjustment_Should_Be_Rejected_For_Foreign_Restaurant_Items()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(admin);

        var foreignItem = await fixture.InsertInventoryItemAsync(foreign.RestaurantId, foreign.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{foreignItem.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 1m,
            reason = "Damaged/wastage"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Batch_Production_Should_Create_Prepared_Stock_Lot()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m,
            notes = "Morning batch"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BatchProductionDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(idli.MenuItemId, payload!.MenuItemId);
        Assert.Equal(preparedStock.InventoryItemId, payload.PreparedInventoryItemId);
        Assert.Equal(3m, payload.QuantityProduced);
        Assert.Single(payload.IngredientConsumptions);
        Assert.Equal(6m, payload.IngredientConsumptions[0].QuantityConsumed);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var rawMovements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == riceBatter.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var preparedMovements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == preparedStock.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var rawStock = rawMovements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);
        var preparedStockBalance = preparedMovements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);

        Assert.Equal(4m, rawStock);
        Assert.Equal(3m, preparedStockBalance);

        var preparedLot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == preparedStock.InventoryItemId &&
            entity.SourceBatchProductionId == payload!.BatchProductionId);

        Assert.Equal(payload.PreparedInventoryMovementId, preparedLot.SourceMovementId);
        Assert.Equal(payload.BatchReference, preparedLot.BatchReference);
        Assert.Equal(payload.ProducedAtUtc, preparedLot.ReceivedAtUtc);
        Assert.Equal(payload.ExpiresAtUtc, preparedLot.ExpiresAtUtc);
        Assert.Equal(payload.QuantityProduced, preparedLot.InitialQuantity);
        Assert.Equal(payload.QuantityProduced, preparedLot.RemainingQuantity);
        Assert.Equal(preparedStock.UnitOfMeasure, preparedLot.UnitOfMeasure);

        var rawConsumptionMovementIds = await context.BatchProductionIngredientConsumptions
            .Where(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.BatchProductionId == payload.BatchProductionId)
            .Select(entity => entity.InventoryMovementId)
            .ToArrayAsync();

        var rawLot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == riceBatter.InventoryItemId);

        Assert.Equal(10m, rawLot.InitialQuantity);
        Assert.Equal(4m, rawLot.RemainingQuantity);
        Assert.Equal(1, await context.InventoryLotAllocations.CountAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == riceBatter.InventoryItemId &&
            rawConsumptionMovementIds.Contains(entity.InventoryMovementId)));
    }

    [Fact]
    public async Task Batch_Production_Should_Reject_When_Recipe_Is_Missing()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Contains("recipe", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Batch_Production_Should_Reject_When_Raw_Stock_Is_Insufficient()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 1m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Contains("Insufficient stock", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Batch_Production_Should_Reject_Branch_Mismatch()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = secondBranch.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 1m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Contains("Branch access is restricted", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Prepared_Stock_Wastage_Should_Reduce_Stock_And_Create_A_Movement()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var production = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m
        });
        production.EnsureSuccessStatusCode();

        var wastage = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/prepared-stock/wastage", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantity = 1m,
            reason = "Spoilage",
            notes = "End of day"
        });

        wastage.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var preparedMovements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == preparedStock.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var preparedStockBalance = preparedMovements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);
        var wasteMovement = await context.InventoryMovements.SingleAsync(entity =>
            entity.InventoryItemId == preparedStock.InventoryItemId &&
            entity.MovementType == InventoryMovementType.Waste);

        Assert.Equal(2m, preparedStockBalance);
        Assert.Equal("Spoilage", wasteMovement.Reason);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Return_Produced_Served_Wasted_And_Remaining_Quantities()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");
        var rawItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw", "kg");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rawItem.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rawItem.InventoryItemId, 2m);

        var businessDate = new DateTime(2026, 6, 13);
        var batchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);
        var servedAt = new DateTimeOffset(2026, 6, 13, 4, 0, 0, TimeSpan.Zero);
        var wasteAt = new DateTimeOffset(2026, 6, 13, 5, 0, 0, TimeSpan.Zero);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);
        var ticket = await fixture.InsertKitchenTicketAsync(
            admin.RestaurantId,
            admin.BranchId,
            order.PosOrderId,
            "KIT-20260613-0001",
            order.OrderNumber,
            "EatIn",
            KitchenTicketStatus.Served,
            servedAt.AddMinutes(-15),
            servedAt);

        var batch = await fixture.InsertBatchProductionAsync(
            admin.RestaurantId,
            admin.BranchId,
            idli.MenuItemId,
            preparedStock.InventoryItemId,
            admin.UserId,
            quantityProduced: 3m,
            businessDate,
            batchAt,
            notes: "Morning batch");

        var preparedMovement = await fixture.InsertInventoryMovementAsync(
            admin.RestaurantId,
            admin.BranchId,
            preparedStock.InventoryItemId,
            admin.UserId,
            InventoryMovementType.StockIn,
            3m,
            batchAt,
            reason: "Batch production");

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            context.KitchenTicketInventoryDeductions.Add(new KitchenTicketInventoryDeduction
            {
                RestaurantId = admin.RestaurantId,
                BranchId = admin.BranchId,
                KitchenTicketId = ticket.KitchenTicketId,
                InventoryItemId = preparedStock.InventoryItemId,
                InventoryMovementId = preparedMovement.InventoryMovementId,
                QuantityDeducted = 1m,
                CreatedAtUtc = servedAt
            });

            context.InventoryMovements.Add(new InventoryMovement
            {
                RestaurantId = admin.RestaurantId,
                BranchId = admin.BranchId,
                InventoryItemId = preparedStock.InventoryItemId,
                MovementType = InventoryMovementType.Consumption,
                Quantity = 1m,
                Reason = "Kitchen ticket completion consumption",
                MovementDate = servedAt,
                RecordedByUserId = admin.UserId,
                CreatedAtUtc = servedAt
            });

            context.InventoryMovements.Add(new InventoryMovement
            {
                RestaurantId = admin.RestaurantId,
                BranchId = admin.BranchId,
                InventoryItemId = preparedStock.InventoryItemId,
                MovementType = InventoryMovementType.Waste,
                Quantity = 1m,
                Reason = "Spoilage",
                MovementDate = wasteAt,
                RecordedByUserId = admin.UserId,
                CreatedAtUtc = wasteAt
            });

            await context.SaveChangesAsync();
        }

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(admin.BranchId, payload!.BranchId);
        Assert.Equal("2026-06-13", payload.BusinessDate);
        Assert.Equal(3m, payload.Totals.ProducedQuantity);
        Assert.Equal(1m, payload.Totals.ServedQuantity);
        Assert.Equal(1m, payload.Totals.WastedQuantity);
        Assert.Equal(1m, payload.Totals.RemainingQuantity);
        Assert.Equal(1, payload.Totals.ItemCount);
        Assert.Equal(0, payload.Totals.WarningCount);
        var row = Assert.Single(payload.Rows);
        Assert.Equal(idli.MenuItemId, row.MenuItemId);
        Assert.Equal(preparedStock.InventoryItemId, row.PreparedInventoryItemId);
        Assert.Equal("Idli", row.MenuItemName);
        Assert.Equal("Idli Prepared", row.PreparedInventoryItemName);
        Assert.Equal("pcs", row.UnitOfMeasure);
        Assert.Equal(3m, row.ProducedQuantity);
        Assert.Equal(1m, row.ServedQuantity);
        Assert.Equal(1m, row.WastedQuantity);
        Assert.Equal(1m, row.RemainingQuantity);
        Assert.False(row.HasWarning);
        Assert.Null(row.WarningReason);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Be_Branch_Scoped()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedBranchlessSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var firstBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Main Branch");
        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");
        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedOne = await fixture.InsertInventoryItemAsync(admin.RestaurantId, firstBranch.BranchId, "Idli Prepared - Main", "Prepared", "pcs");
        var preparedTwo = await fixture.InsertInventoryItemAsync(admin.RestaurantId, secondBranch.BranchId, "Idli Prepared - Second", "Prepared", "pcs");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, firstBranch.BranchId, idli.MenuItemId, preparedOne.InventoryItemId);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, secondBranch.BranchId, idli.MenuItemId, preparedTwo.InventoryItemId);

        var businessDate = new DateTime(2026, 6, 13);
        var batchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, firstBranch.BranchId, idli.MenuItemId, preparedOne.InventoryItemId, admin.UserId, 2m, businessDate, batchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, firstBranch.BranchId, preparedOne.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 2m, batchAt, reason: "Batch production");

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, secondBranch.BranchId, idli.MenuItemId, preparedTwo.InventoryItemId, admin.UserId, 4m, businessDate, batchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, secondBranch.BranchId, preparedTwo.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 4m, batchAt, reason: "Batch production");

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={firstBranch.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(firstBranch.BranchId, payload!.BranchId);
        Assert.Single(payload.Rows);
        Assert.Equal(2m, payload.Totals.ProducedQuantity);
        Assert.Equal(2m, payload.Totals.RemainingQuantity);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Use_Business_Date_Correctly()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);

        var firstDate = new DateTime(2026, 6, 13);
        var secondDate = new DateTime(2026, 6, 14);
        var firstBatchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);
        var secondBatchAt = new DateTimeOffset(2026, 6, 14, 2, 0, 0, TimeSpan.Zero);

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId, admin.UserId, 2m, firstDate, firstBatchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 2m, firstBatchAt, reason: "Batch production");
        await fixture.InsertBatchProductionAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId, admin.UserId, 4m, secondDate, secondBatchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 4m, secondBatchAt, reason: "Batch production");

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("2026-06-13", payload!.BusinessDate);
        Assert.Single(payload.Rows);
        Assert.Equal(2m, payload.Totals.ProducedQuantity);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Show_Missing_Mapping_Warning()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);

        var businessDate = new DateTime(2026, 6, 13);
        var batchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId, admin.UserId, 2m, businessDate, batchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 2m, batchAt, reason: "Batch production");

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        var row = Assert.Single(payload!.Rows);
        Assert.True(row.HasWarning);
        Assert.Contains("Missing prepared stock mapping", row.WarningReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Warn_On_Negative_Remaining_Stock()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);

        var businessDate = new DateTime(2026, 6, 13);
        var batchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);
        var wasteAt = new DateTimeOffset(2026, 6, 13, 5, 0, 0, TimeSpan.Zero);

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId, admin.UserId, 1m, businessDate, batchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 1m, batchAt, reason: "Batch production");
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.Waste, 2m, wasteAt, reason: "Spoilage");

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        var payload = await response.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        var row = Assert.Single(payload!.Rows);
        Assert.True(row.HasWarning);
        Assert.Equal(-1m, row.RemainingQuantity);
        Assert.Contains("Negative remaining stock", row.WarningReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Should_Not_Mutate_Inventory_Records()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);

        var businessDate = new DateTime(2026, 6, 13);
        var batchAt = new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero);

        await fixture.InsertBatchProductionAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId, admin.UserId, 1m, businessDate, batchAt);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, preparedStock.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 1m, batchAt, reason: "Batch production");

        using var beforeScope = fixture.Services.CreateScope();
        var beforeContext = beforeScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var beforeMovementCount = await beforeContext.InventoryMovements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var beforeBatchCount = await beforeContext.BatchProductions.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var beforeAuditCount = await beforeContext.AuditLogs.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-13");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);

        using var afterScope = fixture.Services.CreateScope();
        var afterContext = afterScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var afterMovementCount = await afterContext.InventoryMovements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var afterBatchCount = await afterContext.BatchProductions.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var afterAuditCount = await afterContext.AuditLogs.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);

        Assert.Equal(beforeMovementCount, afterMovementCount);
        Assert.Equal(beforeBatchCount, afterBatchCount);
        Assert.Equal(beforeAuditCount, afterAuditCount);
    }

    [Fact]
    public async Task Summary_Should_Report_Recently_Adjusted_Items()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 4m,
            reason = "Manual purchase entry"
        });
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);

        var summaryResponse = await fixture.Client.GetAsync($"/api/v1/inventory/summary?branchId={admin.BranchId}");
        var summaryBody = await summaryResponse.Content.ReadAsStringAsync();
        Assert.True(summaryResponse.IsSuccessStatusCode, summaryBody);
        var payload = await summaryResponse.Content.ReadFromJsonAsync<InventorySummaryResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.RecentlyAdjustedCount);
    }

    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Batch_Production_Should_Accept_Expiry_Metadata()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero);
        var expiresAt = producedAt.AddHours(8);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m,
            producedAtUtc = producedAt,
            shelfLifeHours = 8m,
            expiresAt,
            storageNote = "Refrigerate below 4C",
            batchReference = "BATCH-AM-001"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BatchProductionDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(8m, payload!.ShelfLifeHours);
        Assert.Equal(expiresAt, payload.ExpiresAtUtc);
        Assert.Equal("Refrigerate below 4C", payload.StorageNote);
        Assert.Equal("BATCH-AM-001", payload.BatchReference);
    }

    [Fact]
    public async Task Batch_Production_Should_Calculate_Expiry_From_Shelf_Life_Hours()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m,
            producedAtUtc = producedAt,
            shelfLifeHours = 6m
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BatchProductionDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(6m, payload!.ShelfLifeHours);
        Assert.Equal(producedAt.AddHours(6), payload.ExpiresAtUtc);
    }

    [Fact]
    public async Task Batch_Production_Should_Reject_Expiry_Before_Production_Time()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero);
        var badExpiry = producedAt.AddHours(-1);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m,
            producedAtUtc = producedAt,
            expiresAt = badExpiry
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Contains("Expiry date must be after", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stock_In_Should_Create_Inventory_Lot()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "L", 5m, true);
        var movementDate = DateTimeOffset.UtcNow;
        var expiresAt = movementDate.AddDays(5);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry",
            movementDate,
            expiresAt,
            batchReference = "LOT-2026-001"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<InventoryMovementItemDto>();
        Assert.NotNull(payload);
        Assert.Equal(expiresAt, payload!.ExpiresAtUtc);
        Assert.Equal("LOT-2026-001", payload.BatchReference);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == item.InventoryItemId &&
            entity.SourceMovementId == payload.InventoryMovementId);

        Assert.Equal(admin.RestaurantId, lot.RestaurantId);
        Assert.Equal(admin.BranchId, lot.BranchId);
        Assert.Equal(item.InventoryItemId, lot.InventoryItemId);
        Assert.Equal(payload.InventoryMovementId, lot.SourceMovementId);
        Assert.Null(lot.SourceBatchProductionId);
        Assert.Equal(expiresAt, lot.ExpiresAtUtc);
        Assert.Equal("LOT-2026-001", lot.BatchReference);
        Assert.Equal(movementDate, lot.ReceivedAtUtc);
        Assert.Equal(10m, lot.InitialQuantity);
        Assert.Equal(10m, lot.RemainingQuantity);
        Assert.Equal("L", lot.UnitOfMeasure);
    }

    [Fact]
    public async Task AdjustmentIncrease_Should_Create_Inventory_Lot()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Butter", "Dairy", "kg", 5m, true);
        var movementDate = DateTimeOffset.UtcNow;
        var expiresAt = movementDate.AddDays(7);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "AdjustmentIncrease",
            quantity = 4m,
            reason = "Physical count correction",
            movementDate,
            expiresAt,
            batchReference = "ADJ-2026-007"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<InventoryMovementItemDto>();
        Assert.NotNull(payload);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == item.InventoryItemId &&
            entity.SourceMovementId == payload!.InventoryMovementId);

        Assert.Equal(expiresAt, lot.ExpiresAtUtc);
        Assert.Equal("ADJ-2026-007", lot.BatchReference);
        Assert.Equal(4m, lot.InitialQuantity);
        Assert.Equal(4m, lot.RemainingQuantity);
        Assert.Equal("kg", lot.UnitOfMeasure);
    }

    [Fact]
    public async Task Consumption_Should_Allocate_Earliest_Expiring_Lot_First()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "L", 5m, true);
        var now = DateTimeOffset.UtcNow;
        var tomorrow = now.AddDays(1);
        var nextWeek = now.AddDays(7);

        var firstStockIn = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-2),
            expiresAt = tomorrow,
            batchReference = "LOT-A"
        });
        firstStockIn.EnsureSuccessStatusCode();

        var secondStockIn = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 5m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-1),
            expiresAt = nextWeek,
            batchReference = "LOT-B"
        });
        secondStockIn.EnsureSuccessStatusCode();

        var consumption = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 3m,
            reason = "Damaged/wastage"
        });

        consumption.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lots = (await context.InventoryLots
                .Where(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId)
                .ToListAsync())
            .OrderBy(entity => entity.ExpiresAtUtc)
            .ThenBy(entity => entity.ReceivedAtUtc)
            .ThenBy(entity => entity.InventoryLotId)
            .ToList();

        Assert.Equal(2, lots.Count);
        Assert.Equal(0m, lots.Single(entity => entity.BatchReference == "LOT-A").RemainingQuantity);
        Assert.Equal(4m, lots.Single(entity => entity.BatchReference == "LOT-B").RemainingQuantity);
        Assert.Equal(2, await context.InventoryLotAllocations.CountAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == item.InventoryItemId));
    }

    [Fact]
    public async Task Consumption_Should_Use_FIFO_For_NoExpiry_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Flour", "Dry", "kg", 5m, true);
        var now = DateTimeOffset.UtcNow;

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-3),
            batchReference = "LOT-A"
        });

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 5m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-1),
            batchReference = "LOT-B"
        });

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 3m,
            reason = "Damaged/wastage"
        });

        response.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lotA = await context.InventoryLots.SingleAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.BatchReference == "LOT-A");
        var lotB = await context.InventoryLots.SingleAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.BatchReference == "LOT-B");

        Assert.Equal(0m, lotA.RemainingQuantity);
        Assert.Equal(4m, lotB.RemainingQuantity);
    }

    [Fact]
    public async Task Consumption_Should_Not_Allocate_From_Expired_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Yogurt", "Dairy", "kg", 5m, true);
        var now = DateTimeOffset.UtcNow;

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-5),
            expiresAt = now.AddDays(-1),
            batchReference = "EXPIRED"
        });

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 3m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-2),
            expiresAt = now.AddDays(2),
            batchReference = "FRESH"
        });

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 2m,
            reason = "Damaged/wastage"
        });

        response.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var expiredLot = await context.InventoryLots.SingleAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.BatchReference == "EXPIRED");
        var freshLot = await context.InventoryLots.SingleAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.BatchReference == "FRESH");

        Assert.Equal(2m, expiredLot.RemainingQuantity);
        Assert.Equal(1m, freshLot.RemainingQuantity);
        Assert.Single(await context.InventoryLotAllocations.Where(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.InventoryMovementId != Guid.Empty).ToListAsync());
    }

    [Fact]
    public async Task Consumption_Should_Fail_When_Only_Expired_Stock_Is_Available()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Curd", "Dairy", "kg", 5m, true);
        var now = DateTimeOffset.UtcNow;

        await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate = now.AddDays(-5),
            expiresAt = now.AddDays(-1),
            batchReference = "EXPIRED"
        });

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 1m,
            reason = "Damaged/wastage"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lot = await context.InventoryLots.SingleAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId && entity.BatchReference == "EXPIRED");
        Assert.Equal(2m, lot.RemainingQuantity);
        Assert.Empty(await context.InventoryLotAllocations.Where(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId && entity.InventoryItemId == item.InventoryItemId).ToListAsync());
    }

    [Fact]
    public async Task Wastage_Should_Allocate_Expired_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var expiresAt = producedAt.AddHours(6);

        var production = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 2m,
            producedAtUtc = producedAt,
            shelfLifeHours = 6m,
            expiresAt
        });
        production.EnsureSuccessStatusCode();
        var productionPayload = await production.Content.ReadFromJsonAsync<BatchProductionDetailDto>();
        Assert.NotNull(productionPayload);

        var wastage = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/prepared-stock/wastage", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantity = 1m,
            reason = "Spoilage",
            wastedAtUtc = DateTimeOffset.UtcNow
        });

        wastage.EnsureSuccessStatusCode();
        var wastagePayload = await wastage.Content.ReadFromJsonAsync<InventoryMovementItemDto>();
        Assert.NotNull(wastagePayload);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == preparedStock.InventoryItemId &&
            entity.SourceBatchProductionId == productionPayload!.BatchProductionId);

        Assert.Equal(1m, lot.RemainingQuantity);
        Assert.Equal(1, await context.InventoryLotAllocations.CountAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == preparedStock.InventoryItemId &&
            entity.InventoryMovementId == wastagePayload!.InventoryMovementId));
    }

    [Fact]
    public async Task Lot_Creation_Should_Be_Branch_Scoped()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedBranchlessSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var firstBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "First Branch");
        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");
        var firstItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, firstBranch.BranchId, "Rice", "Grains", "kg", 5m, true);
        var secondItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, secondBranch.BranchId, "Rice", "Grains", "kg", 5m, true);

        var firstResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{firstItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 3m,
            reason = "Manual purchase entry",
            movementDate = DateTimeOffset.UtcNow,
            expiresAt = DateTimeOffset.UtcNow.AddDays(5),
            batchReference = "BR-A-001"
        });

        firstResponse.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var firstBranchLots = await context.InventoryLots.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == firstBranch.BranchId);
        var secondBranchLots = await context.InventoryLots.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == secondBranch.BranchId);
        var secondBranchItemLots = await context.InventoryLots.CountAsync(entity => entity.InventoryItemId == secondItem.InventoryItemId);

        Assert.Equal(1, firstBranchLots);
        Assert.Equal(0, secondBranchLots);
        Assert.Equal(0, secondBranchItemLots);
    }

    [Fact]
    public async Task Existing_Stock_Without_Lots_Should_Create_Opening_Lot_And_Allocate()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Sugar", "Grains", "kg", 5m, true);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, item.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 5m, DateTimeOffset.UtcNow, reason: "Legacy stock");

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            Assert.Equal(0, await context.InventoryLots.CountAsync(entity => entity.InventoryItemId == item.InventoryItemId));
        }

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 2m,
            reason = "Damaged/wastage"
        });

        response.EnsureSuccessStatusCode();

        var list = await fixture.Client.GetAsync($"/api/v1/inventory/items?branchId={admin.BranchId}");
        list.EnsureSuccessStatusCode();
        var payload = await list.Content.ReadFromJsonAsync<InventoryItemListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(3m, payload!.Items.Single().CurrentStock);

        using var afterScope = fixture.Services.CreateScope();
        var afterContext = afterScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var openingLot = await afterContext.InventoryLots.SingleAsync(entity => entity.InventoryItemId == item.InventoryItemId);
        Assert.Equal("Opening lot", openingLot.BatchReference);
        Assert.Equal(5m, openingLot.InitialQuantity);
        Assert.Equal(3m, openingLot.RemainingQuantity);

        Assert.Single(await afterContext.InventoryLotAllocations.Where(entity => entity.InventoryItemId == item.InventoryItemId).ToListAsync());
    }

    [Fact]
    public async Task Expiry_Report_Should_Use_Lot_Remaining_Quantity()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "L", 5m, true);
        var movementDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var expiresAt = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        var stockIn = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 10m,
            reason = "Manual purchase entry",
            movementDate,
            expiresAt,
            batchReference = "LOT-2026-001"
        });
        stockIn.EnsureSuccessStatusCode();
        var stockInPayload = await stockIn.Content.ReadFromJsonAsync<InventoryMovementItemDto>();
        Assert.NotNull(stockInPayload);

        var consumption = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 4m,
            reason = "Damaged/wastage"
        });
        consumption.EnsureSuccessStatusCode();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = await response.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        Assert.Equal(admin.BranchId, payload!.BranchId);
        Assert.Equal("2026-06-22", payload.AsOfDate);
        var row = Assert.Single(payload.Rows);
        Assert.Equal(item.InventoryItemId, row.InventoryItemId);
        Assert.Equal(6m, row.Quantity);
        Assert.Equal("Adjustment", row.SourceType);
        Assert.Equal("LOT-2026-001", row.BatchReference);
        Assert.Equal(expiresAt, row.ExpiresAtUtc);
        Assert.Equal($"MOV-{stockInPayload!.InventoryMovementId:N}", row.SourceReference);
        Assert.Equal("Fresh", row.ExpiryStatus);
        Assert.Equal(1, payload.Totals.FreshCount);
        Assert.Equal(0, payload.Totals.NearExpiryCount);
        Assert.Equal(0, payload.Totals.ExpiredCount);
        Assert.Equal(0, payload.Totals.NoExpiryCount);
        Assert.Equal(1, payload.Totals.TotalTrackedItems);
    }

    [Fact]
    public async Task Expiry_Report_Should_Not_Show_Fully_Consumed_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Yogurt", "Dairy", "kg", 5m, true);
        var movementDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var expiresAt = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        var stockInResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate,
            expiresAt,
            batchReference = "LOT-ZERO"
        });
        stockInResponse.EnsureSuccessStatusCode();

        var consumption = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 2m,
            reason = "Damaged/wastage"
        });
        consumption.EnsureSuccessStatusCode();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = await response.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        Assert.Empty(payload!.Rows);
        Assert.Equal(0, payload.Totals.TotalTrackedItems);
        Assert.Equal(0, payload.Totals.FreshCount);
        Assert.Equal(0, payload.Totals.NearExpiryCount);
        Assert.Equal(0, payload.Totals.ExpiredCount);
        Assert.Equal(0, payload.Totals.NoExpiryCount);
    }

    [Fact]
    public async Task Expiry_Report_Should_Show_NoExpiry_Opening_Lot()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Sugar", "Grains", "kg", 5m, true);
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, item.InventoryItemId, admin.UserId, InventoryMovementType.StockIn, 5m, new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), reason: "Legacy stock");

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Decrease",
            quantity = 2m,
            reason = "Damaged/wastage"
        });
        response.EnsureSuccessStatusCode();

        var reportResponse = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        var reportBody = await reportResponse.Content.ReadAsStringAsync();
        Assert.True(reportResponse.IsSuccessStatusCode, reportBody);
        var payload = await reportResponse.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        var row = Assert.Single(payload!.Rows);
        Assert.Equal(item.InventoryItemId, row.InventoryItemId);
        Assert.Equal("OpeningLot", row.SourceType);
        Assert.Equal("Opening lot", row.BatchReference);
        Assert.Equal("NoExpiry", row.ExpiryStatus);
        Assert.Null(row.ExpiresAtUtc);
        Assert.Equal(3m, row.Quantity);
        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var openingLot = await context.InventoryLots.SingleAsync(entity =>
                entity.RestaurantId == admin.RestaurantId &&
                entity.BranchId == admin.BranchId &&
                entity.InventoryItemId == item.InventoryItemId &&
                entity.BatchReference == "Opening lot");

            Assert.Equal($"LOT-{openingLot.InventoryLotId:N}", row.SourceReference);
        }
        Assert.Equal(1, payload.Totals.NoExpiryCount);
        Assert.Equal(1, payload.Totals.TotalTrackedItems);
    }

    [Fact]
    public async Task Expiry_Report_Should_Classify_Expired_NearExpiry_Fresh_From_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var expiredItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "L", 5m, true);
        var nearExpiryItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Curd", "Dairy", "kg", 2m, true);
        var freshItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Paneer", "Dairy", "kg", 1m, true);
        var noExpiryItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Salt", "Dry", "kg", 1m, true);
        var receivedAt = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);

        var expiredResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{expiredItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Manual purchase entry",
            movementDate = receivedAt,
            expiresAt = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero),
            batchReference = "EXPIRED"
        });
        expiredResponse.EnsureSuccessStatusCode();

        var nearResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{nearExpiryItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Manual purchase entry",
            movementDate = receivedAt,
            expiresAt = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero),
            batchReference = "NEAR"
        });
        nearResponse.EnsureSuccessStatusCode();

        var freshResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{freshItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Manual purchase entry",
            movementDate = receivedAt,
            expiresAt = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            batchReference = "FRESH"
        });
        freshResponse.EnsureSuccessStatusCode();

        var noExpiryResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{noExpiryItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 1m,
            reason = "Manual purchase entry",
            movementDate = receivedAt,
            batchReference = "NOEXP"
        });
        noExpiryResponse.EnsureSuccessStatusCode();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = await response.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        Assert.Equal(1, payload!.Totals.ExpiredCount);
        Assert.Equal(1, payload.Totals.NearExpiryCount);
        Assert.Equal(1, payload.Totals.FreshCount);
        Assert.Equal(1, payload.Totals.NoExpiryCount);
        Assert.Equal(4, payload.Totals.TotalTrackedItems);
        Assert.Equal("Expired", payload.Rows[0].ExpiryStatus);
        Assert.Equal("NearExpiry", payload.Rows[1].ExpiryStatus);
        Assert.Equal("Fresh", payload.Rows[2].ExpiryStatus);
        Assert.Equal("NoExpiry", payload.Rows[3].ExpiryStatus);
    }

    [Fact]
    public async Task Expiry_Report_Should_Be_Branch_Scoped_With_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedBranchlessSystemUserAsync("Admin");
        var firstBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Alpha Branch");
        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Beta Branch");
        await fixture.AuthenticateAsync(admin);

        var firstItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, firstBranch.BranchId, "Milk", "Dairy", "L", 5m, true);
        var secondItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, secondBranch.BranchId, "Curd", "Dairy", "kg", 2m, true);

        var firstResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{firstItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 5m,
            reason = "Manual purchase entry",
            movementDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            expiresAt = new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero),
            batchReference = "BR-A-001"
        });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{secondItem.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 2m,
            reason = "Manual purchase entry",
            movementDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            expiresAt = new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero),
            batchReference = "BR-B-001"
        });
        secondResponse.EnsureSuccessStatusCode();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={firstBranch.BranchId}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = await response.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        Assert.Equal(firstBranch.BranchId, payload!.BranchId);
        Assert.Equal(1, payload.Rows.Count());
        Assert.All(payload.Rows, row => Assert.Equal(firstItem.InventoryItemId, row.InventoryItemId));
    }

    [Fact]
    public async Task Expiry_Report_Should_Be_Read_Only_With_Lots()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var item = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "L", 5m, true);
        var stockInResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/inventory/items/{item.InventoryItemId}/movements", new
        {
            movementType = "Increase",
            quantity = 5m,
            reason = "Manual purchase entry",
            movementDate = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            expiresAt = new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero),
            batchReference = "READONLY"
        });
        stockInResponse.EnsureSuccessStatusCode();

        using var beforeScope = fixture.Services.CreateScope();
        var beforeContext = beforeScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var beforeLotCount = await beforeContext.InventoryLots.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var beforeAllocationCount = await beforeContext.InventoryLotAllocations.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        Assert.True(response.IsSuccessStatusCode);

        using var afterScope = fixture.Services.CreateScope();
        var afterContext = afterScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var afterLotCount = await afterContext.InventoryLots.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);
        var afterAllocationCount = await afterContext.InventoryLotAllocations.CountAsync(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId);

        Assert.Equal(beforeLotCount, afterLotCount);
        Assert.Equal(beforeAllocationCount, afterAllocationCount);
    }

    [Fact]
    public async Task Expiry_Report_Should_Use_Batch_Production_Lot_Source()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw", "kg");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero);
        var expiresAt = producedAt.AddHours(6);

        var batchResponse = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m,
            producedAtUtc = producedAt,
            shelfLifeHours = 6m,
            batchReference = "BATCH-IDLI-001"
        });
        batchResponse.EnsureSuccessStatusCode();
        var batchPayload = await batchResponse.Content.ReadFromJsonAsync<BatchProductionDetailDto>();
        Assert.NotNull(batchPayload);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/expiry-stock?branchId={admin.BranchId}&asOfDate=2026-06-22");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = await response.Content.ReadFromJsonAsync<ExpiryStockReportResponseDto>();
        Assert.NotNull(payload);

        var batchRow = Assert.Single(payload!.Rows.Where(r => r.SourceType == "BatchProduction"));
        Assert.Equal(preparedStock.InventoryItemId, batchRow.InventoryItemId);
        Assert.Equal("BATCH-IDLI-001", batchRow.BatchReference);
        Assert.Equal(expiresAt, batchRow.ExpiresAtUtc);
        Assert.Equal(3m, batchRow.Quantity);
        Assert.Equal("BatchProduction", batchRow.SourceType);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var lot = await context.InventoryLots.SingleAsync(entity =>
            entity.RestaurantId == admin.RestaurantId &&
            entity.BranchId == admin.BranchId &&
            entity.InventoryItemId == preparedStock.InventoryItemId &&
            entity.SourceBatchProductionId == batchPayload!.BatchProductionId);

        Assert.Equal(3m, lot.RemainingQuantity);
        Assert.Equal(batchPayload.BatchProductionId, lot.SourceBatchProductionId);
    }

    [Fact]
    public async Task Prepared_Stock_Report_Math_Should_Remain_Unchanged_By_Expiry_Metadata()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared", "pcs");
        var rawItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw", "kg");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rawItem.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rawItem.InventoryItemId, 2m);

        var businessDate = new DateTime(2026, 6, 22);
        var batchAt = new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero);

        var batchResponse = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m,
            businessDate,
            producedAtUtc = batchAt,
            shelfLifeHours = 8m,
            storageNote = "Keep cool"
        });
        batchResponse.EnsureSuccessStatusCode();

        var reportResponse = await fixture.Client.GetAsync($"/api/v1/reports/prepared-stock?branchId={admin.BranchId}&businessDate=2026-06-22");
        var reportBody = await reportResponse.Content.ReadAsStringAsync();
        Assert.True(reportResponse.IsSuccessStatusCode, reportBody);
        var payload = await reportResponse.Content.ReadFromJsonAsync<PreparedStockReportResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(3m, payload!.Totals.ProducedQuantity);
        Assert.Equal(0m, payload.Totals.ServedQuantity);
        Assert.Equal(0m, payload.Totals.WastedQuantity);
    }

    [Fact]
    public async Task Wastage_For_Expired_Stock_Must_Still_Be_Explicit_And_Not_Automatic()
    {
        await using var fixture = await InventoryApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var riceBatter = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, riceBatter.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, riceBatter.InventoryItemId, 2m);

        var producedAt = DateTimeOffset.UtcNow.AddHours(-10);

        var batchResponse = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m,
            producedAtUtc = producedAt,
            shelfLifeHours = 2m
        });
        batchResponse.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var wasteMovementCount = await context.InventoryMovements
            .CountAsync(entity =>
                entity.RestaurantId == admin.RestaurantId &&
                entity.InventoryItemId == preparedStock.InventoryItemId &&
                entity.MovementType == InventoryMovementType.Waste);

        Assert.Equal(0, wasteMovementCount);
    }

    private sealed class InventoryApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<InventoryApiFactory> CreateAsync()
        {
            var factory = new InventoryApiFactory();
            await factory.InitializeAsync();
            return factory;
        }

        private async Task InitializeAsync()
        {
            await _connection.OpenAsync();
            _ = Services;
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task<SeedResult> SeedSystemUserAsync(string roleName)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = $"{roleName} Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode($"REST{roleName[..1].ToUpperInvariant()}01");
            restaurant.SetCountryProfile("SG");

            var branch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"{roleName} User",
                MobileNumber = "90000004",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = assignedRole.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                branch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<SeedResult> SeedBranchlessSystemUserAsync(string roleName)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = $"{roleName} Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode($"REST{roleName[..1].ToUpperInvariant()}02");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = null,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000014");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = assignedRole.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                Guid.Empty,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<string> AuthenticateAsync(SeedResult seed)
        {
            var payload = await LoginAsync(seed.RestaurantCode, seed.MobileNumber, seed.Password);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
            return payload.AccessToken;
        }

        public async Task<Branch> InsertBranchAsync(Guid restaurantId, string name)
        {
            using var scope = Services.CreateScope();
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

        public async Task<SeedResult> SeedForeignRestaurantAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = new Restaurant
            {
                Name = "Foreign Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTFOREIGN01");

            var branch = new Branch
            {
                Name = "Foreign Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                branch.BranchId,
                Guid.Empty,
                restaurant.NormalizedRestaurantCode,
                string.Empty,
                string.Empty,
                restaurant.Name);
        }

        public async Task<InventoryItem> InsertInventoryItemAsync(
            Guid restaurantId,
            Guid branchId,
            string name,
            string category,
            string unitOfMeasure = "kg",
            decimal lowStockThreshold = 1m,
            bool isActive = true)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var item = new InventoryItem
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                Name = name.Trim(),
                NormalizedName = name.Trim().ToUpperInvariant(),
                Category = category.Trim(),
                UnitOfMeasure = unitOfMeasure.Trim(),
                LowStockThreshold = lowStockThreshold,
                IsActive = isActive,
                CreatedAtUtc = now
            };

            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            return item;
        }

        public async Task<MenuCategory> InsertCategoryAsync(Guid restaurantId, string name, int displayOrder = 0)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var category = new MenuCategory
            {
                RestaurantId = restaurantId,
                Name = name,
                DisplayOrder = displayOrder,
                Status = MenuCategoryStatus.Active
            };

            context.MenuCategories.Add(category);
            await context.SaveChangesAsync();
            return category;
        }

        public async Task<MenuItem> InsertItemAsync(
            Guid restaurantId,
            Guid categoryId,
            string name,
            decimal basePrice = 2.50m,
            decimal taxRate = 0,
            string? sku = null,
            bool isAvailableForEatIn = true,
            bool isAvailableForParcel = true,
            MenuItemStatus status = MenuItemStatus.Active)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var item = new MenuItem
            {
                RestaurantId = restaurantId,
                MenuCategoryId = categoryId,
                Name = name,
                Sku = sku,
                BasePrice = basePrice,
                TaxRate = taxRate,
                IsVegetarian = true,
                IsAvailableForEatIn = isAvailableForEatIn,
                IsAvailableForParcel = isAvailableForParcel,
                Status = status
            };

            context.MenuItems.Add(item);
            await context.SaveChangesAsync();
            return item;
        }

        public async Task AddStockInMovementAsync(Guid restaurantId, Guid branchId, Guid recordedByUserId, Guid inventoryItemId, decimal quantity)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            context.InventoryMovements.Add(new InventoryMovement
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                MovementType = InventoryMovementType.StockIn,
                Quantity = quantity,
                MovementDate = DateTimeOffset.UtcNow,
                RecordedByUserId = recordedByUserId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }

        public async Task AddRecipeIngredientAsync(
            Guid restaurantId,
            Guid branchId,
            Guid menuItemId,
            Guid inventoryItemId,
            decimal quantityRequired)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            context.Set<MenuItemRecipeIngredient>().Add(new MenuItemRecipeIngredient
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                MenuItemId = menuItemId,
                InventoryItemId = inventoryItemId,
                QuantityRequired = quantityRequired,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }

        public async Task<BatchProduction> InsertBatchProductionAsync(
            Guid restaurantId,
            Guid branchId,
            Guid menuItemId,
            Guid preparedInventoryItemId,
            Guid producedByUserId,
            decimal quantityProduced,
            DateTime businessDate,
            DateTimeOffset producedAtUtc,
            string? notes = null,
            Guid? preparedInventoryMovementId = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var batchProduction = new BatchProduction
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                MenuItemId = menuItemId,
                PreparedInventoryItemId = preparedInventoryItemId,
                QuantityProduced = quantityProduced,
                BusinessDate = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Utc),
                ProducedAtUtc = producedAtUtc,
                ProducedByUserId = producedByUserId,
                Notes = notes,
                PreparedInventoryMovementId = preparedInventoryMovementId,
                CreatedAtUtc = producedAtUtc
            };

            context.BatchProductions.Add(batchProduction);
            await context.SaveChangesAsync();
            return batchProduction;
        }

        public async Task<InventoryMovement> InsertInventoryMovementAsync(
            Guid restaurantId,
            Guid branchId,
            Guid inventoryItemId,
            Guid recordedByUserId,
            InventoryMovementType movementType,
            decimal quantity,
            DateTimeOffset movementDate,
            string? reason = null,
            string? notes = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var movement = new InventoryMovement
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                MovementType = movementType,
                Quantity = quantity,
                MovementDate = movementDate,
                RecordedByUserId = recordedByUserId,
                Reason = reason,
                Notes = notes,
                CreatedAtUtc = movementDate
            };

            context.InventoryMovements.Add(movement);
            await context.SaveChangesAsync();
            return movement;
        }

        public async Task<KitchenTicket> InsertKitchenTicketAsync(
            Guid restaurantId,
            Guid branchId,
            Guid posOrderId,
            string ticketNumber,
            string orderNumberSnapshot,
            string orderTypeSnapshot,
            KitchenTicketStatus status,
            DateTimeOffset createdAt,
            DateTimeOffset? servedAt = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var ticket = new KitchenTicket
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                PosOrderId = posOrderId,
                TicketNumber = ticketNumber,
                Status = status,
                OrderNumberSnapshot = orderNumberSnapshot,
                OrderTypeSnapshot = orderTypeSnapshot,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                ServedAt = servedAt
            };

            context.KitchenTickets.Add(ticket);
            await context.SaveChangesAsync();
            return ticket;
        }

        public async Task<KitchenTicketInventoryDeduction> InsertKitchenTicketInventoryDeductionAsync(
            Guid restaurantId,
            Guid branchId,
            Guid kitchenTicketId,
            Guid inventoryItemId,
            Guid inventoryMovementId,
            decimal quantityDeducted)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var deduction = new KitchenTicketInventoryDeduction
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                KitchenTicketId = kitchenTicketId,
                InventoryItemId = inventoryItemId,
                InventoryMovementId = inventoryMovementId,
                QuantityDeducted = quantityDeducted,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            context.KitchenTicketInventoryDeductions.Add(deduction);
            await context.SaveChangesAsync();
            return deduction;
        }

        public async Task UpdateMenuItemDeductionModeAsync(Guid itemId, MenuItemInventoryDeductionMode mode)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var item = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == itemId);
            item.InventoryDeductionMode = mode;
            await context.SaveChangesAsync();
        }

        public async Task<MenuItemStockItem> MapMenuItemToInventoryItemAsync(
            Guid restaurantId,
            Guid branchId,
            Guid menuItemId,
            Guid inventoryItemId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var mapping = new MenuItemStockItem
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                MenuItemId = menuItemId,
                InventoryItemId = inventoryItemId,
                CreatedAtUtc = now
            };

            context.MenuItemStockItems.Add(mapping);
            await context.SaveChangesAsync();
            return mapping;
        }

        public async Task<PosOrder> InsertConfirmedOrderAsync(
            Guid restaurantId,
            Guid branchId,
            MenuCategory category,
            MenuItem item,
            string orderNumber,
            decimal unitPrice,
            decimal taxRate,
            decimal quantity,
            PosOrderType orderType = PosOrderType.EatIn)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;
            var lineSubtotal = RoundMoney(unitPrice * quantity);
            var lineTax = RoundMoney(lineSubtotal * taxRate / 100m);
            var lineTotal = RoundMoney(lineSubtotal + lineTax);

            var order = new PosOrder
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderNumber = orderNumber,
                OrderType = orderType,
                Status = PosOrderStatus.Confirmed,
                Subtotal = lineSubtotal,
                TaxTotal = lineTax,
                GrandTotal = lineTotal,
                ConfirmedAt = now,
                CreatedAt = now
            };

            order.PosOrderLines.Add(new PosOrderLine
            {
                RestaurantId = restaurantId,
                MenuItemId = item.MenuItemId,
                MenuCategoryId = category.MenuCategoryId,
                MenuItemNameSnapshot = item.Name,
                MenuCategoryNameSnapshot = category.Name,
                SkuSnapshot = item.Sku,
                UnitPrice = unitPrice,
                TaxRate = taxRate,
                Quantity = quantity,
                LineSubtotal = lineSubtotal,
                LineTax = lineTax,
                LineTotal = lineTotal,
                CreatedAt = now
            });

            context.PosOrders.Add(order);
            await context.SaveChangesAsync();
            return order;
        }

        public async Task<AuthLoginResponseDto> LoginAsync(string restaurantCode, string mobileNumber, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                restaurantCode,
                mobileNumber,
                password
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponseDto>();
            Assert.NotNull(payload);

            return payload!;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "SqlServer",
                    ["Database:ConnectionString"] = "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Jwt:Issuer"] = "BillSoft",
                    ["Jwt:Audience"] = "BillSoft",
                    ["Jwt:SigningKey"] = "unit-test-signing-key-unit-test-signing-key",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Jwt:RefreshTokenLifetimeDays"] = "7"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BillSoftDbContext>>();
                services.RemoveAll<BillSoftDbContext>();
                services.AddDbContext<BillSoftDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
                _connection.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed record SeedResult(
        Guid RestaurantId,
        Guid BranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName);

    private sealed record AuthLoginResponseDto(string AccessToken, string RefreshToken);

    private sealed record InventoryItemListResponseDto(InventoryItemListItemDto[] Items);

    private sealed record InventoryItemListItemDto(
        Guid InventoryItemId,
        Guid RestaurantId,
        Guid BranchId,
        string Name,
        string NormalizedName,
        string Category,
        string UnitOfMeasure,
        decimal LowStockThreshold,
        bool IsActive,
        decimal CurrentStock,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record InventoryMovementListResponseDto(InventoryMovementItemDto[] Items);

    private sealed record InventoryMovementItemDto(
        Guid InventoryMovementId,
        Guid InventoryItemId,
        Guid RestaurantId,
        Guid BranchId,
        string MovementType,
        decimal Quantity,
        decimal? UnitCost,
        string? ReferenceNumber,
        string? Reason,
        string? Notes,
        DateTimeOffset MovementDate,
        Guid RecordedByUserId,
        DateTimeOffset CreatedAtUtc,
        decimal PreviousStock,
        decimal Delta,
        decimal ResultingStock,
        string ResultingStatus,
        DateTimeOffset? ExpiresAtUtc,
        string? BatchReference);

    private sealed record InventoryItemDetailDto(
        Guid InventoryItemId,
        Guid RestaurantId,
        Guid BranchId,
        string Name,
        string NormalizedName,
        string Category,
        string UnitOfMeasure,
        decimal LowStockThreshold,
        bool IsActive,
        decimal CurrentStock,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record InventorySummaryResponseDto(
        Guid RestaurantId,
        Guid BranchId,
        int TotalItems,
        int ActiveItems,
        int InactiveItems,
        int LowStockCount,
        int OutOfStockCount,
        decimal TotalCurrentStock,
        int RecentlyAdjustedCount,
        InventoryAlertItemDto[] LowStockItems,
        InventoryAlertItemDto[] OutOfStockItems);

    private sealed record InventoryAlertItemDto(
        Guid InventoryItemId,
        string Name,
        string Category,
        string UnitOfMeasure,
        decimal LowStockThreshold,
        decimal CurrentStock,
        string Status);

    private sealed record BatchProductionDetailDto(
        Guid BatchProductionId,
        Guid RestaurantId,
        Guid BranchId,
        Guid MenuItemId,
        string MenuItemName,
        Guid PreparedInventoryItemId,
        string PreparedInventoryItemName,
        decimal QuantityProduced,
        DateTime BusinessDate,
        DateTimeOffset ProducedAtUtc,
        Guid ProducedByUserId,
        string ProducedByUserName,
        string? Notes,
        Guid? PreparedInventoryMovementId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc,
        BatchProductionIngredientConsumptionDto[] IngredientConsumptions,
        decimal? ShelfLifeHours,
        DateTimeOffset? ExpiresAtUtc,
        string? StorageNote,
        string? BatchReference);

    private sealed record BatchProductionIngredientConsumptionDto(
        Guid BatchProductionIngredientConsumptionId,
        Guid InventoryItemId,
        string InventoryItemName,
        decimal QuantityConsumed,
        Guid InventoryMovementId,
        DateTimeOffset CreatedAtUtc);

    private sealed record PreparedStockReportResponseDto(
        Guid BranchId,
        string BranchName,
        string BusinessDate,
        PreparedStockReportTotalsDto Totals,
        PreparedStockReportRowDto[] Rows);

    private sealed record PreparedStockReportTotalsDto(
        decimal ProducedQuantity,
        decimal ServedQuantity,
        decimal WastedQuantity,
        decimal RemainingQuantity,
        int ItemCount,
        int WarningCount);

    private sealed record PreparedStockReportRowDto(
        Guid MenuItemId,
        string? MenuItemName,
        Guid? PreparedInventoryItemId,
        string? PreparedInventoryItemName,
        string? UnitOfMeasure,
        decimal ProducedQuantity,
        decimal ServedQuantity,
        decimal WastedQuantity,
        decimal RemainingQuantity,
        bool HasWarning,
        string? WarningReason);

    private sealed record ExpiryStockReportResponseDto(
        Guid BranchId,
        string BranchName,
        string AsOfDate,
        ExpiryStockReportTotalsDto Totals,
        ExpiryStockReportRowDto[] Rows);

    private sealed record ExpiryStockReportTotalsDto(
        int FreshCount,
        int NearExpiryCount,
        int ExpiredCount,
        int NoExpiryCount,
        int TotalTrackedItems);

    private sealed record ExpiryStockReportRowDto(
        Guid InventoryItemId,
        string InventoryItemName,
        string UnitOfMeasure,
        string SourceType,
        string? BatchReference,
        decimal Quantity,
        DateTimeOffset? ProducedOrReceivedAt,
        DateTimeOffset? ExpiresAtUtc,
        string ExpiryStatus,
        string? WarningReason,
        string? SourceReference);
}

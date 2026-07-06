using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class PosOrderEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/pos/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Order_Permission()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("AccountsUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/pos/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Use_Current_Restaurant_And_Snapshot_Menu_Data()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(
            seed.RestaurantId,
            category.MenuCategoryId,
            "Idli",
            basePrice: 2.50m,
            taxRate: 5m,
            sku: "IDLI-1");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            restaurantId = Guid.NewGuid(),
            branchId = seed.BranchId,
            orderType = "EatIn",
            tableName = "T1",
            customerName = "Walk-in",
            customerMobile = "9000000000",
            notes = "No onion",
            lines = new[]
            {
                new
                {
                    menuItemId = item.MenuItemId,
                    quantity = 2m,
                    notes = "Less spicy"
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.RestaurantId, payload!.RestaurantId);
        Assert.Equal(seed.BranchId, payload.BranchId);
        Assert.Equal("Draft", payload.Status);
        Assert.Equal($"ORD-{today}-0001", payload.OrderNumber);
        Assert.Single(payload.Lines);
        Assert.Equal("Idli", payload.Lines[0].MenuItemNameSnapshot);
        Assert.Equal("Breakfast", payload.Lines[0].MenuCategoryNameSnapshot);
        Assert.Equal("IDLI-1", payload.Lines[0].SkuSnapshot);
        Assert.Equal(2.50m, payload.Lines[0].UnitPrice);
        Assert.Equal(5m, payload.Lines[0].TaxRate);
        Assert.Equal(5.25m, payload.Lines[0].LineTotal);
    }

    [Fact]
    public async Task Create_First_And_Second_Order_Should_Get_Sequential_Numbers_For_Same_Branch_And_Day()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(firstPayload);

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(secondPayload);

        Assert.Equal($"ORD-{today}-0001", firstPayload!.OrderNumber);
        Assert.Equal($"ORD-{today}-0002", secondPayload!.OrderNumber);
    }

    [Fact]
    public async Task Create_Should_Keep_Branch_Sequences_Independent()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var secondBranch = await fixture.InsertBranchAsync(seed.RestaurantId, "Second Branch", BranchStatus.Active);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(firstPayload);

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = secondBranch.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(secondPayload);

        Assert.Equal($"ORD-{today}-0001", firstPayload!.OrderNumber);
        Assert.Equal($"ORD-{today}-0001", secondPayload!.OrderNumber);
    }

    [Fact]
    public async Task Create_Should_Keep_Restaurant_Sequences_Independent()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var secondRestaurant = await fixture.SeedCustomUserAsync("Admin", SystemPermissions.OrderCreate, SystemPermissions.OrderView, SystemPermissions.OrderCancel);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(firstPayload);

        await fixture.AuthenticateAsync(secondRestaurant);
        var secondCategory = await fixture.InsertCategoryAsync(secondRestaurant.RestaurantId, "Breakfast", 1);
        var secondItem = await fixture.InsertItemAsync(secondRestaurant.RestaurantId, secondCategory.MenuCategoryId, "Vada");

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = secondRestaurant.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = secondItem.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(secondPayload);

        Assert.Equal($"ORD-{today}-0001", firstPayload!.OrderNumber);
        Assert.Equal($"ORD-{today}-0001", secondPayload!.OrderNumber);
    }

    [Fact]
    public async Task Create_Should_Not_Accept_Order_Number_From_Request_Body()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderNumber = "ORD-20991231-9999",
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(payload);

        Assert.NotEqual("ORD-20991231-9999", payload!.OrderNumber);
        Assert.Equal($"ORD-{today}-0001", payload.OrderNumber);
    }

    [Fact]
    public async Task Create_Should_Reject_Branch_From_Another_Restaurant()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = foreign.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = Guid.NewGuid(), quantity = 1m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Detail_Should_Return_404_For_Order_In_Another_Restaurant()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var foreignOrder = await fixture.InsertOrderAsync(foreign.RestaurantId, foreign.BranchId, "ORD-20260612-0001");

        var response = await fixture.Client.GetAsync($"/api/v1/pos/orders/{foreignOrder.PosOrderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Inactive_Branch_In_Current_Restaurant()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var inactiveBranch = await fixture.InsertBranchAsync(seed.RestaurantId, "Inactive Branch", BranchStatus.Inactive);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = inactiveBranch.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_When_No_Lines_Are_Provided()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Quantity_At_Or_Below_Zero()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 0m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Inactive_Menu_Item()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", status: MenuItemStatus.Inactive);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_EatIn_Item_That_Is_Parcel_Only()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", isAvailableForEatIn: false, isAvailableForParcel: true);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Parcel_Item_That_Is_EatIn_Only()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", isAvailableForEatIn: true, isAvailableForParcel: false);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "Parcel",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Draft_Should_Replace_Lines_And_Recalculate_Totals()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var first = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 0m);
        var second = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Vada", basePrice: 3.00m, taxRate: 10m);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = first.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/pos/orders/{created!.PosOrderId}", new
        {
            orderType = "Parcel",
            tableName = (string?)null,
            customerName = "Walk-in",
            customerMobile = "9000000000",
            notes = "updated",
            lines = new[]
            {
                new { menuItemId = first.MenuItemId, quantity = 2m, notes = "first" },
                new { menuItemId = second.MenuItemId, quantity = 1m, notes = "second" }
            }
        });

        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("Parcel", updated!.OrderType);
        Assert.Equal(2, updated.Lines.Length);
        Assert.Equal(8.30m, updated.GrandTotal);
    }

    [Fact]
    public async Task Confirm_And_Cancel_Should_Update_Status_And_Audit_Friendly_Fields()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created!.PosOrderId}/confirm", null);
        confirm.EnsureSuccessStatusCode();
        var confirmed = await confirm.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(confirmed);
        Assert.Equal("Confirmed", confirmed!.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
        Assert.Equal(seed.UserId, confirmed.ConfirmedByUserId);

        var cancel = await fixture.Client.PostAsJsonAsync($"/api/v1/pos/orders/{created.PosOrderId}/cancel", new { reason = "Customer cancelled" });
        cancel.EnsureSuccessStatusCode();
        var cancelled = await cancel.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Customer cancelled", cancelled.CancelReason);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.Equal(seed.UserId, cancelled.CancelledByUserId);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "PosOrder" && log.EntityId == created.PosOrderId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("PosOrder.Created", actions);
        Assert.Contains("PosOrder.Confirmed", actions);
        Assert.Contains("PosOrder.Cancelled", actions);
    }

    [Fact]
    public async Task Confirm_Should_Create_One_Kitchen_Ticket_And_Return_Its_Summary()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 5m, sku: "IDLI-1");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            tableName = "T1",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 2m, notes = "Less spicy" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var menuItem = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == item.MenuItemId);
            menuItem.UpdateProfile(
                menuItem.MenuCategoryId,
                "Changed Idli",
                menuItem.Description,
                "NEW-SKU",
                9.99m,
                12m,
                menuItem.IsVegetarian,
                menuItem.IsAvailableForEatIn,
                menuItem.IsAvailableForParcel,
                menuItem.InventoryDeductionMode);
            await context.SaveChangesAsync();
        }

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created!.PosOrderId}/confirm", null);
        confirm.EnsureSuccessStatusCode();
        var confirmed = await confirm.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(confirmed);
        Assert.Equal("Confirmed", confirmed!.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
        Assert.Equal(seed.UserId, confirmed.ConfirmedByUserId);
        Assert.NotNull(confirmed.KitchenTicketId);
        Assert.NotNull(confirmed.KitchenTicketNumber);
        Assert.Equal("Pending", confirmed.KitchenTicketStatus);

        using var confirmedScope = fixture.Services.CreateScope();
        var confirmedContext = confirmedScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var kitchenTicket = await confirmedContext.KitchenTickets
            .Include(entity => entity.KitchenTicketLines)
            .SingleAsync(entity => entity.PosOrderId == created.PosOrderId && entity.RestaurantId == seed.RestaurantId);

        Assert.Equal(KitchenTicketStatus.Pending, kitchenTicket.Status);
        Assert.Equal(created.PosOrderId, kitchenTicket.PosOrderId);
        Assert.Equal(created.OrderNumber, kitchenTicket.OrderNumberSnapshot);
        Assert.Single(kitchenTicket.KitchenTicketLines);
        Assert.Equal("Idli", kitchenTicket.KitchenTicketLines.Single().MenuItemNameSnapshot);
        Assert.Equal("Breakfast", kitchenTicket.KitchenTicketLines.Single().MenuCategoryNameSnapshot);
        Assert.Equal("IDLI-1", kitchenTicket.KitchenTicketLines.Single().SkuSnapshot);

        var actions = await confirmedContext.AuditLogs
            .Where(log => log.Action == "PosOrder.Confirmed" || log.Action == "KitchenTicket.Created")
            .Select(log => new { log.Action, log.EntityType, log.EntityId })
            .ToListAsync();

        Assert.Contains(actions, log => log.Action == "PosOrder.Confirmed" && log.EntityType == "PosOrder" && log.EntityId == created.PosOrderId.ToString());
        Assert.Contains(actions, log => log.Action == "KitchenTicket.Created" && log.EntityType == "KitchenTicket" && log.EntityId == confirmed.KitchenTicketId!.Value.ToString());
    }

    [Fact]
    public async Task Confirm_Should_Copy_Pos_Snapshots_And_Preserve_Multiple_Lines()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var firstItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 5m, sku: "IDLI-1");
        var secondItem = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Vada", basePrice: 3.00m, taxRate: 0m, sku: "VADA-1");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            tableName = "T2",
            lines = new[]
            {
                new { menuItemId = firstItem.MenuItemId, quantity = 2m, notes = "Less spicy" },
                new { menuItemId = secondItem.MenuItemId, quantity = 1m, notes = "Extra chutney" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var firstMenuItem = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == firstItem.MenuItemId);
            firstMenuItem.UpdateProfile(
                firstMenuItem.MenuCategoryId,
                "Changed Idli",
                firstMenuItem.Description,
                "CHANGED-IDLI",
                9.99m,
                12m,
                firstMenuItem.IsVegetarian,
                firstMenuItem.IsAvailableForEatIn,
                firstMenuItem.IsAvailableForParcel,
                firstMenuItem.InventoryDeductionMode);

            var secondMenuItem = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == secondItem.MenuItemId);
            secondMenuItem.UpdateProfile(
                secondMenuItem.MenuCategoryId,
                "Changed Vada",
                secondMenuItem.Description,
                "CHANGED-VADA",
                4.99m,
                0m,
                secondMenuItem.IsVegetarian,
                secondMenuItem.IsAvailableForEatIn,
                secondMenuItem.IsAvailableForParcel,
                secondMenuItem.InventoryDeductionMode);

            await context.SaveChangesAsync();
        }

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created!.PosOrderId}/confirm", null);
        confirm.EnsureSuccessStatusCode();

        using var confirmScope = fixture.Services.CreateScope();
        var confirmedContext = confirmScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var kitchenTicket = await confirmedContext.KitchenTickets
            .Include(entity => entity.KitchenTicketLines)
            .SingleAsync(entity => entity.PosOrderId == created.PosOrderId && entity.RestaurantId == seed.RestaurantId);

        Assert.Equal(2, kitchenTicket.KitchenTicketLines.Count);
        Assert.Collection(
            kitchenTicket.KitchenTicketLines.OrderBy(line => line.DisplayOrder),
            firstLine =>
            {
                Assert.Equal("Idli", firstLine.MenuItemNameSnapshot);
                Assert.Equal("Breakfast", firstLine.MenuCategoryNameSnapshot);
                Assert.Equal("IDLI-1", firstLine.SkuSnapshot);
                Assert.Equal(2m, firstLine.Quantity);
                Assert.Equal("Less spicy", firstLine.Notes);
            },
            secondLine =>
            {
                Assert.Equal("Vada", secondLine.MenuItemNameSnapshot);
                Assert.Equal("Breakfast", secondLine.MenuCategoryNameSnapshot);
                Assert.Equal("VADA-1", secondLine.SkuSnapshot);
                Assert.Equal(1m, secondLine.Quantity);
                Assert.Equal("Extra chutney", secondLine.Notes);
            });
    }

    [Fact]
    public async Task Confirm_Should_Not_Create_A_Duplicate_Ticket_When_One_Already_Exists()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", sku: "IDLI-1");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;
            var ticket = new KitchenTicket
            {
                RestaurantId = seed.RestaurantId,
                BranchId = seed.BranchId,
                PosOrderId = created!.PosOrderId,
                TicketNumber = "KIT-20260614-0001",
                Status = KitchenTicketStatus.Pending,
                OrderNumberSnapshot = created.OrderNumber,
                OrderTypeSnapshot = created.OrderType,
                CreatedByUserId = seed.UserId,
                CreatedAt = now
            };
            ticket.KitchenTicketLines.Add(new KitchenTicketLine
            {
                RestaurantId = seed.RestaurantId,
                PosOrderLineId = created.Lines[0].PosOrderLineId,
                MenuItemId = created.Lines[0].MenuItemId,
                MenuCategoryId = created.Lines[0].MenuCategoryId,
                MenuItemNameSnapshot = created.Lines[0].MenuItemNameSnapshot,
                MenuCategoryNameSnapshot = created.Lines[0].MenuCategoryNameSnapshot,
                SkuSnapshot = created.Lines[0].SkuSnapshot,
                Quantity = created.Lines[0].Quantity,
                Notes = created.Lines[0].Notes,
                DisplayOrder = 1,
                CreatedAt = now
            });

            context.KitchenTickets.Add(ticket);
            await context.SaveChangesAsync();
        }

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created.PosOrderId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);

        using var verifyScope = fixture.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var order = await verifyContext.PosOrders.SingleAsync(entity => entity.PosOrderId == created.PosOrderId);
        Assert.Equal(PosOrderStatus.Draft, order.Status);
        Assert.Equal(1, await verifyContext.KitchenTickets.CountAsync(entity => entity.PosOrderId == created.PosOrderId && entity.RestaurantId == seed.RestaurantId));
    }

    [Fact]
    public async Task Cancel_Draft_Should_Set_Status_And_Cancel_Fields()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        var cancel = await fixture.Client.PostAsJsonAsync($"/api/v1/pos/orders/{created!.PosOrderId}/cancel", new { reason = "Customer changed mind" });
        cancel.EnsureSuccessStatusCode();
        var cancelled = await cancel.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Customer changed mind", cancelled.CancelReason);
        Assert.Null(cancelled.ConfirmedAt);
    }

    [Fact]
    public async Task Update_Confirmed_Order_Should_Be_Rejected()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created!.PosOrderId}/confirm", null);
        confirm.EnsureSuccessStatusCode();

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/pos/orders/{created.PosOrderId}", new
        {
            orderType = "Parcel",
            tableName = (string?)null,
            customerName = "Walk-in",
            customerMobile = "9000000000",
            notes = "updated",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 2m, notes = "first" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Confirm_Cancelled_Order_Should_Be_Rejected()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);

        var cancel = await fixture.Client.PostAsJsonAsync($"/api/v1/pos/orders/{created!.PosOrderId}/cancel", new { reason = "Customer changed mind" });
        cancel.EnsureSuccessStatusCode();

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{created.PosOrderId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(0, await context.KitchenTickets.CountAsync(entity => entity.PosOrderId == created.PosOrderId));
    }

    [Fact]
    public async Task Confirm_Should_Return_404_For_Order_In_Another_Restaurant_And_Not_Create_Ticket()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var foreignOrder = await fixture.InsertOrderAsync(foreign.RestaurantId, foreign.BranchId, "ORD-20260612-0099");

        var confirm = await fixture.Client.PostAsync($"/api/v1/pos/orders/{foreignOrder.PosOrderId}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(0, await context.KitchenTickets.CountAsync(entity => entity.PosOrderId == foreignOrder.PosOrderId));
    }

    [Fact]
    public async Task Menu_Price_Change_Should_Not_Alter_Existing_Order_Snapshot()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 5m, sku: "IDLI-1");

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(2.50m, created!.Lines[0].UnitPrice);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var menuItem = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == item.MenuItemId);
            menuItem.BasePrice = 4.75m;
            await context.SaveChangesAsync();
        }

        var detail = await fixture.Client.GetAsync($"/api/v1/pos/orders/{created.PosOrderId}");
        detail.EnsureSuccessStatusCode();
        var payload = await detail.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(2.50m, payload!.Lines[0].UnitPrice);
        Assert.Equal(5m, payload.Lines[0].TaxRate);
    }

    [Fact]
    public async Task List_Should_Filter_By_Branch_Status_OrderType_And_Search()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);
        var secondBranch = await fixture.InsertBranchAsync(seed.RestaurantId, "Second Branch", BranchStatus.Active);
        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 0m);

        var firstOrder = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = seed.BranchId,
            orderType = "EatIn",
            tableName = "T1",
            customerName = "Walk-in",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        firstOrder.EnsureSuccessStatusCode();
        var createdFirst = await firstOrder.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(createdFirst);

        var secondOrder = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = secondBranch.BranchId,
            orderType = "Parcel",
            tableName = "P2",
            customerName = "Delivery",
            lines = new[]
            {
                new { menuItemId = item.MenuItemId, quantity = 1m, notes = "" }
            }
        });
        secondOrder.EnsureSuccessStatusCode();
        var createdSecond = await secondOrder.Content.ReadFromJsonAsync<PosOrderDetailDto>();
        Assert.NotNull(createdSecond);

        var confirmFirst = await fixture.Client.PostAsync($"/api/v1/pos/orders/{createdFirst!.PosOrderId}/confirm", null);
        confirmFirst.EnsureSuccessStatusCode();
        var cancelSecond = await fixture.Client.PostAsJsonAsync($"/api/v1/pos/orders/{createdSecond!.PosOrderId}/cancel", new { reason = "cancelled" });
        cancelSecond.EnsureSuccessStatusCode();

        var response = await fixture.Client.GetAsync($"/api/v1/pos/orders?branchId={seed.BranchId}&status=Confirmed&orderType=EatIn&search=T1");
        if (!response.IsSuccessStatusCode)
        {
            var failureBody = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected success, got {(int)response.StatusCode}: {failureBody}");
        }
        var payload = await response.Content.ReadFromJsonAsync<PosOrderListResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items);
        Assert.Equal(createdFirst.PosOrderId, payload.Items[0].PosOrderId);
    }

    [Fact]
    public async Task List_Invalid_Status_Filter_Should_Return_Allowed_Values()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/pos/orders?status=Archived");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal($"Status filter must be one of: {string.Join(", ", Enum.GetNames<PosOrderStatus>())}.", problem!.Detail);
    }

    [Fact]
    public async Task List_Invalid_OrderType_Filter_Should_Return_Allowed_Values()
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/pos/orders?orderType=Delivery");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal($"Order type filter must be one of: {string.Join(", ", Enum.GetNames<PosOrderType>())}.", problem!.Detail);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/pos/orders")]
    [InlineData("DELETE", "/api/v1/pos/orders/{id}")]
    public async Task Delete_Route_Should_Not_Exist(string method, string pathTemplate)
    {
        await using var fixture = await PosOrderApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var path = pathTemplate.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase);
        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    private sealed class PosOrderApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<PosOrderApiFactory> CreateAsync()
        {
            var factory = new PosOrderApiFactory();
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
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var branch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000029");

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

        public async Task<SeedResult> SeedCustomUserAsync(string roleName, params string[] permissionCodes)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = $"{roleName} Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode($"RESTC{roleName[..1].ToUpperInvariant()}01");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var branch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var role = new Role
            {
                RestaurantId = restaurant.RestaurantId,
                Name = roleName,
                Description = $"{roleName} role",
                IsSystemRole = false
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000030");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            context.Roles.Add(role);

            foreach (var permissionCode in permissionCodes)
            {
                var permission = await context.Permissions.SingleAsync(entity => entity.Code == permissionCode);
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.RoleId,
                    PermissionId = permission.PermissionId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
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
            restaurant.SetCountryProfile("SG");

            var branch = new Branch
            {
                Name = "Foreign Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = "SG",
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

        public async Task<string> AuthenticateAsync(SeedResult seed)
        {
            var payload = await LoginAsync(seed.RestaurantCode, seed.MobileNumber, seed.Password);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
            return payload.AccessToken;
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

        public async Task<Branch> InsertBranchAsync(Guid restaurantId, string name, BranchStatus status = BranchStatus.Active)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var branch = new Branch
            {
                RestaurantId = restaurantId,
                Name = name,
                Status = status,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            return branch;
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

        public async Task<PosOrder> InsertOrderAsync(Guid restaurantId, Guid branchId, string orderNumber)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var order = new PosOrder
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderNumber = orderNumber,
                OrderType = PosOrderType.EatIn,
                Status = PosOrderStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.PosOrders.Add(order);
            await context.SaveChangesAsync();
            return order;
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

    private sealed record PosOrderListResponseDto(PosOrderListItemDto[] Items);

    private sealed record PosOrderListItemDto(
        Guid PosOrderId,
        Guid BranchId,
        string OrderNumber,
        string OrderType,
        string Status,
        string? TableName,
        string? CustomerName,
        decimal GrandTotal,
        int LineCount,
        DateTimeOffset CreatedAt);

    private sealed record PosOrderDetailDto(
        Guid PosOrderId,
        Guid RestaurantId,
        Guid BranchId,
        string OrderNumber,
        string OrderType,
        string Status,
        string? TableName,
        string? CustomerName,
        string? CustomerMobile,
        string? Notes,
        decimal Subtotal,
        decimal TaxTotal,
        decimal GrandTotal,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? CancelledAt,
        string? CancelReason,
        Guid? CreatedByUserId,
        Guid? ConfirmedByUserId,
        Guid? CancelledByUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        PosOrderLineDto[] Lines,
        Guid? KitchenTicketId = null,
        string? KitchenTicketNumber = null,
        string? KitchenTicketStatus = null);

    private sealed record PosOrderLineDto(
        Guid PosOrderLineId,
        Guid MenuItemId,
        Guid MenuCategoryId,
        string MenuItemNameSnapshot,
        string MenuCategoryNameSnapshot,
        string? SkuSnapshot,
        decimal UnitPrice,
        decimal TaxRate,
        decimal Quantity,
        decimal LineSubtotal,
        decimal LineTax,
        decimal LineTotal,
        string? Notes,
        int DisplayOrder,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}

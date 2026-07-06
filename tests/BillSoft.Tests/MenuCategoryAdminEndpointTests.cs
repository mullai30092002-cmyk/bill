using System.Net;
using System.Net.Http.Json;
using BillSoft.Domain.Menu;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillSoft.Tests;

public sealed class MenuCategoryAdminEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Menu_Permissions()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("AccountsUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/categories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MenuItem_View_Should_Allow_Category_List()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);
        await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.GetAsync("/api/v1/admin/menu/categories");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuCategoryListResponseDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Items);
    }

    [Fact]
    public async Task MenuCategory_Manage_Should_Allow_Category_Create()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "Breakfast",
            displayOrder = 1
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuCategoryDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.RestaurantId, payload!.RestaurantId);
        Assert.Equal("Breakfast", payload.Name);
        Assert.Equal(1, payload.DisplayOrder);
        Assert.Equal("Active", payload.Status);
    }

    [Fact]
    public async Task Create_Should_Use_Current_Restaurant_Even_When_Extra_RestaurantId_Is_Sent()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            restaurantId = foreign.RestaurantId,
            name = "Breakfast",
            displayOrder = 1
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuCategoryDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.RestaurantId, payload!.RestaurantId);
        Assert.NotEqual(foreign.RestaurantId, payload.RestaurantId);
    }

    [Fact]
    public async Task Create_Should_Reject_Blank_Name()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "   ",
            displayOrder = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Name_In_Same_Restaurant_Case_Insensitive()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "Breakfast",
            displayOrder = 1
        });
        first.EnsureSuccessStatusCode();

        fixture.SqlCapture.Clear();
        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = " breakfast ",
            displayOrder = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.DoesNotContain(fixture.SqlCapture.Commands, command =>
            command.Contains("UPPER(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_Should_Allow_Same_Name_In_Different_Restaurants()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var current = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "Breakfast",
            displayOrder = 1
        });
        current.EnsureSuccessStatusCode();

        await fixture.AuthenticateAsync(foreign);
        var other = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "Breakfast",
            displayOrder = 1
        });

        other.EnsureSuccessStatusCode();
        var payload = await other.Content.ReadFromJsonAsync<MenuCategoryDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(foreign.RestaurantId, payload!.RestaurantId);
    }

    [Fact]
    public async Task Update_Should_Change_Name_And_Display_Order()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/categories/{category.MenuCategoryId}", new
        {
            name = "Meals",
            displayOrder = 3
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuCategoryDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("Meals", payload!.Name);
        Assert.Equal(3, payload.DisplayOrder);
        Assert.NotEqual(default, payload.UpdatedAt);
    }

    [Fact]
    public async Task Update_Should_Reject_Duplicate_Name_In_Same_Restaurant()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var breakfast = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        var meals = await fixture.InsertCategoryAsync(seed.RestaurantId, "Meals", 2);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/categories/{meals.MenuCategoryId}", new
        {
            name = " breakfast ",
            displayOrder = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Should_Return_404_For_Other_Restaurant_Category()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);

        var response = await fixture.Client.GetAsync($"/api/v1/admin/menu/categories/{foreignCategory.MenuCategoryId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Should_Be_Blocked_When_Active_Menu_Items_Exist()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
        _ = await fixture.InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");

        var response = await fixture.Client.PostAsync($"/api/v1/admin/menu/categories/{category.MenuCategoryId}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Activate_And_Deactivate_Should_Work()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var category = await fixture.InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1, MenuCategoryStatus.Inactive);

        var activate = await fixture.Client.PostAsync($"/api/v1/admin/menu/categories/{category.MenuCategoryId}/activate", null);
        activate.EnsureSuccessStatusCode();

        var deactivate = await fixture.Client.PostAsync($"/api/v1/admin/menu/categories/{category.MenuCategoryId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "MenuCategory" && log.EntityId == category.MenuCategoryId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("MenuCategory.Activated", actions);
        Assert.Contains("MenuCategory.Deactivated", actions);
    }

    [Fact]
    public async Task Successful_Mutations_Should_Write_Audit_Rows()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/categories", new
        {
            name = "Breakfast",
            displayOrder = 1
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<MenuCategoryDetailDto>();
        Assert.NotNull(created);

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/menu/categories/{created!.MenuCategoryId}", new
        {
            name = "Meals",
            displayOrder = 2
        });
        update.EnsureSuccessStatusCode();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "MenuCategory" && log.EntityId == created.MenuCategoryId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("MenuCategory.Created", actions);
        Assert.Contains("MenuCategory.Updated", actions);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/admin/menu/categories")]
    [InlineData("DELETE", "/api/v1/admin/menu/categories/{id}")]
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
}

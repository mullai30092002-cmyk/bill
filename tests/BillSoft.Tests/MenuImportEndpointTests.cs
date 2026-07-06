using System.Net;
using System.Net.Http.Json;
using BillSoft.Domain.Menu;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillSoft.Tests;

public sealed class MenuImportEndpointTests
{
    [Fact]
    public async Task Preview_Should_Validate_Required_Columns()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName
            Breakfast,Idli
            """
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preview_Should_Detect_Current_Restaurant_Duplicates_But_Not_Foreign_Restaurant_Items()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var current = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        var category = await fixture.InsertCategoryAsync(current.RestaurantId, "Breakfast", 1);
        await fixture.InsertItemAsync(current.RestaurantId, category.MenuCategoryId, "Idli");
        await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);

        await fixture.AuthenticateAsync(current);
        var duplicatePreview = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice
            Breakfast,Idli,2.50
            """
        });

        duplicatePreview.EnsureSuccessStatusCode();
        var duplicatePayload = await duplicatePreview.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(duplicatePayload);
        Assert.Equal(1, duplicatePayload!.Summary.DuplicateRows);
        Assert.Equal("Duplicate", duplicatePayload.Rows.Single().Status);
        Assert.True(duplicatePayload.Rows.Single().IsDuplicate);

        await fixture.AuthenticateAsync(foreign);
        var foreignPreview = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice
            Breakfast,Idli,2.50
            """
        });

        foreignPreview.EnsureSuccessStatusCode();
        var foreignPayload = await foreignPreview.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(foreignPayload);
        Assert.Equal(0, foreignPayload!.Summary.DuplicateRows);
        Assert.Equal("Ready", foreignPayload.Rows.Single().Status);
    }

    [Fact]
    public async Task Confirm_Should_Create_Imported_Menu_Items_And_Write_Audit_Summary()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/confirm", new
        {
            importName = "pilot-menu.csv",
            csvText = """
            Category,ItemName,Description,EatInPrice,Available,BranchName
            Breakfast,Idli,Steamed rice cakes,2.50,Yes,Alpha Branch
            """
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Summary.ImportedRows);
        Assert.Equal(0, payload.Summary.UpdatedRows);
        Assert.Equal(0, payload.Summary.InvalidRows);
        Assert.Equal("Imported", payload.Rows.Single().Status);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var category = await context.MenuCategories.SingleAsync(entity =>
            entity.RestaurantId == seed.RestaurantId && entity.Name == "Breakfast");
        var item = await context.MenuItems.SingleAsync(entity =>
            entity.RestaurantId == seed.RestaurantId &&
            entity.MenuCategoryId == category.MenuCategoryId &&
            entity.Name == "Idli");

        Assert.Equal(2.50m, item.BasePrice);
        Assert.Equal(MenuItemStatus.Active, item.Status);
        Assert.Contains(await context.AuditLogs.Select(log => log.Action).ToListAsync(), action => action == "MenuImport.Confirmed");
    }

    [Fact]
    public async Task Confirm_Should_Reject_Invalid_Rows_And_Not_Create_Items()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/confirm", new
        {
            csvText = """
            Category,ItemName,EatInPrice
            Breakfast,,2.50
            """
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(0, await context.MenuItems.CountAsync(entity => entity.RestaurantId == seed.RestaurantId));
    }

    [Fact]
    public async Task Import_Should_Require_MenuItem_Manage_Permission()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("AccountsUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice
            Breakfast,Idli,2.50
            """
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Preview_Should_Reject_Unknown_Branch_Name()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice,BranchName
            Breakfast,Idli,2.50,Missing Branch
            """
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(payload);
        var row = Assert.Single(payload!.Rows);
        Assert.Contains(row.Errors, error => error.Contains("BranchName must match an existing branch in the current restaurant.", StringComparison.Ordinal));
        Assert.Equal("Invalid", row.Status);
    }

    [Fact]
    public async Task Preview_Should_Accept_BranchName_And_BranchCode_As_An_Alias()
    {
        await using var fixture = await MenuAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branchNameResponse = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice,BranchName
            Breakfast,Idli,2.50,Alpha Branch
            """
        });

        branchNameResponse.EnsureSuccessStatusCode();
        var branchNamePayload = await branchNameResponse.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(branchNamePayload);
        var branchNameRow = Assert.Single(branchNamePayload!.Rows);
        Assert.Equal("Alpha Branch", branchNameRow.BranchName);
        Assert.Equal("Ready", branchNameRow.Status);

        var branchCodeResponse = await fixture.Client.PostAsJsonAsync("/api/v1/admin/menu/import/preview", new
        {
            csvText = """
            Category,ItemName,EatInPrice,BranchCode
            Breakfast,Idli,2.50,Alpha Branch
            """
        });

        branchCodeResponse.EnsureSuccessStatusCode();
        var branchCodePayload = await branchCodeResponse.Content.ReadFromJsonAsync<MenuImportResponseDto>();
        Assert.NotNull(branchCodePayload);
        var branchCodeRow = Assert.Single(branchCodePayload!.Rows);
        Assert.Equal("Alpha Branch", branchCodeRow.BranchName);
        Assert.Equal("Ready", branchCodeRow.Status);
    }

    private sealed record MenuImportResponseDto(MenuImportSummaryDto Summary, MenuImportRowDto[] Rows);

    private sealed record MenuImportSummaryDto(
        int TotalRows,
        int ReadyRows,
        int DuplicateRows,
        int InvalidRows,
        int ImportedRows,
        int UpdatedRows,
        int SkippedRows,
        int FailedRows);

    private sealed record MenuImportRowDto(
        int RowNumber,
        string Category,
        string ItemName,
        string? Description,
        decimal? EatInPrice,
        bool? Available,
        string? BranchName,
        string Status,
        string Message,
        string[] Errors,
        string[] Warnings,
        bool IsDuplicate,
        string? ExistingCategoryName,
        string? ExistingMenuItemId,
        string SuggestedAction);
}

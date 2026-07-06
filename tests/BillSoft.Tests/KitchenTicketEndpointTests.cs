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

public sealed class KitchenTicketEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/kitchen/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Kitchen_Ticket_Permission()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("AccountsUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/kitchen/tickets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task KitchenTicket_View_Should_List_And_Get_Tickets()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        var kitchenUser = await fixture.SeedRoleUserAsync(admin.RestaurantId, admin.BranchId, "KitchenUser", "90000031");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 5m, sku: "IDLI-01");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 2m);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        if (!createResponse.IsSuccessStatusCode)
        {
            var failureBody = await createResponse.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected kitchen ticket create success, got {(int)createResponse.StatusCode}: {failureBody}");
        }

        var createdTicket = await createResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(createdTicket);

        await fixture.AuthenticateAsync(kitchenUser);
        var listResponse = await fixture.Client.GetAsync("/api/v1/kitchen/tickets?branchId=" + kitchenUser.BranchId + "&status=Pending");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<KitchenTicketListResponseDto>();
        Assert.NotNull(listPayload);
        Assert.Single(listPayload!.Items);
        Assert.Equal(createdTicket!.KitchenTicketId, listPayload.Items[0].KitchenTicketId);
        Assert.Equal(createdTicket.TicketNumber, listPayload.Items[0].TicketNumber);

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/kitchen/tickets/{createdTicket.KitchenTicketId}");
        detailResponse.EnsureSuccessStatusCode();
        var detailPayload = await detailResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(detailPayload);
        Assert.Single(detailPayload!.Lines);
        Assert.Equal("Idli", detailPayload.Lines[0].MenuItemNameSnapshot);
        Assert.Equal("Breakfast", detailPayload.Lines[0].MenuCategoryNameSnapshot);
        Assert.Equal("IDLI-01", detailPayload.Lines[0].SkuSnapshot);
    }

    [Fact]
    public async Task Create_Should_Copy_Pos_Order_Snapshots_And_Use_Server_Numbering()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli", basePrice: 2.50m, taxRate: 5m, sku: "IDLI-01");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 2m,
            orderType: PosOrderType.Parcel);

        await fixture.UpdateMenuCategoryNameAsync(category.MenuCategoryId, "Changed Breakfast");
        await fixture.UpdateMenuItemAsync(item.MenuItemId, "Changed Idli", sku: "NEW-SKU", basePrice: 9.99m, taxRate: 12m);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new
        {
            restaurantId = Guid.NewGuid(),
            posOrderId = order.PosOrderId,
            ticketNumber = "KIT-20991231-9999"
        });

        createResponse.EnsureSuccessStatusCode();
        var createdTicket = await createResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(createdTicket);
        Assert.StartsWith("KIT-", createdTicket!.TicketNumber, StringComparison.Ordinal);
        Assert.NotEqual("KIT-20991231-9999", createdTicket.TicketNumber);
        Assert.Equal("ORD-20260613-0001", createdTicket.OrderNumberSnapshot);
        Assert.Equal("Parcel", createdTicket.OrderTypeSnapshot);
        Assert.Equal("Idli", createdTicket.Lines[0].MenuItemNameSnapshot);
        Assert.Equal("Breakfast", createdTicket.Lines[0].MenuCategoryNameSnapshot);
        Assert.Equal("IDLI-01", createdTicket.Lines[0].SkuSnapshot);
        Assert.Equal(2m, createdTicket.Lines[0].Quantity);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Contains(await context.AuditLogs.Select(entity => entity.Action).ToListAsync(), action => action == "KitchenTicket.Created");
    }

    [Theory]
    [InlineData(PosOrderStatus.Draft, "Draft orders cannot generate kitchen tickets.")]
    [InlineData(PosOrderStatus.Cancelled, "Cancelled orders cannot generate kitchen tickets.")]
    public async Task Create_Should_Reject_Draft_And_Cancelled_Pos_Orders(PosOrderStatus status, string expectedDetail)
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli");
        var order = await fixture.InsertOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            "ORD-20260613-0001",
            status);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal(expectedDetail, problem!.Detail);
    }

    [Fact]
    public async Task Create_Should_Reject_Orders_From_Another_Restaurant()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);
        var foreignItem = await fixture.InsertItemAsync(foreign.RestaurantId, foreignCategory.MenuCategoryId, "Idli");
        var foreignOrder = await fixture.InsertConfirmedOrderAsync(
            foreign.RestaurantId,
            foreign.BranchId,
            foreignCategory,
            foreignItem,
            "ORD-20260613-0009",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = foreignOrder.PosOrderId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Active_Ticket_For_Same_Pos_Order()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var firstResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("A kitchen ticket already exists for this POS order.", problem!.Detail);
    }

    [Fact]
    public async Task Create_After_Cancelled_Previous_Ticket_Should_Be_Allowed()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var firstResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        firstResponse.EnsureSuccessStatusCode();
        var firstTicket = await firstResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(firstTicket);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{firstTicket!.KitchenTicketId}/cancel", new { reason = "Customer changed mind" });
        cancelResponse.EnsureSuccessStatusCode();

        var secondResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        secondResponse.EnsureSuccessStatusCode();
        var secondTicket = await secondResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(secondTicket);
        Assert.NotEqual(firstTicket.KitchenTicketId, secondTicket!.KitchenTicketId);
    }

    [Fact]
    public async Task Menu_Changes_After_Pos_Order_Should_Not_Affect_Ticket_Line_Snapshots()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli", sku: "IDLI-01");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            item,
            "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        await fixture.UpdateMenuCategoryNameAsync(category.MenuCategoryId, "Changed Breakfast");
        await fixture.UpdateMenuItemAsync(item.MenuItemId, "Changed Idli", sku: "NEW-SKU");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        Assert.Equal("Idli", ticket!.Lines[0].MenuItemNameSnapshot);
        Assert.Equal("Breakfast", ticket.Lines[0].MenuCategoryNameSnapshot);
        Assert.Equal("IDLI-01", ticket.Lines[0].SkuSnapshot);
    }

    [Fact]
    public async Task List_Should_Be_Restaurant_Scoped_And_Newest_First()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Idli");
        var firstOrder = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, category, item, "ORD-20260613-0001", 2.50m, 0m, 1m);
        var secondOrder = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, category, item, "ORD-20260613-0002", 2.50m, 0m, 1m);

        var firstTicketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = firstOrder.PosOrderId });
        firstTicketResponse.EnsureSuccessStatusCode();
        var firstTicket = await firstTicketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(firstTicket);

        var secondTicketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = secondOrder.PosOrderId });
        secondTicketResponse.EnsureSuccessStatusCode();
        var secondTicket = await secondTicketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(secondTicket);

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Lunch", 1);
        var foreignItem = await fixture.InsertItemAsync(foreign.RestaurantId, foreignCategory.MenuCategoryId, "Rice");
        var foreignOrder = await fixture.InsertConfirmedOrderAsync(foreign.RestaurantId, foreign.BranchId, foreignCategory, foreignItem, "ORD-20260613-0099", 3.00m, 0m, 1m);
        await fixture.AuthenticateAsync(foreign);
        var foreignTicketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = foreignOrder.PosOrderId });
        foreignTicketResponse.EnsureSuccessStatusCode();
        await fixture.AuthenticateAsync(admin);

        var listResponse = await fixture.Client.GetAsync($"/api/v1/kitchen/tickets?branchId={admin.BranchId}");
        listResponse.EnsureSuccessStatusCode();
        var payload = await listResponse.Content.ReadFromJsonAsync<KitchenTicketListResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Items.Length);
        Assert.Equal(secondTicket!.KitchenTicketId, payload.Items[0].KitchenTicketId);
        Assert.Equal(firstTicket!.KitchenTicketId, payload.Items[1].KitchenTicketId);
    }

    [Fact]
    public async Task Detail_Should_Return_404_For_Ticket_In_Another_Restaurant()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");

        var foreignCategory = await fixture.InsertCategoryAsync(foreign.RestaurantId, "Breakfast", 1);
        var foreignItem = await fixture.InsertItemAsync(foreign.RestaurantId, foreignCategory.MenuCategoryId, "Idli");
        var foreignOrder = await fixture.InsertConfirmedOrderAsync(foreign.RestaurantId, foreign.BranchId, foreignCategory, foreignItem, "ORD-20260613-0009", 2.50m, 0m, 1m);
        await fixture.AuthenticateAsync(foreign);
        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = foreignOrder.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var foreignTicket = await createResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(foreignTicket);

        await fixture.AuthenticateAsync(admin);
        var response = await fixture.Client.GetAsync($"/api/v1/kitchen/tickets/{foreignTicket!.KitchenTicketId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_Status_Filter_Should_Return_Allowed_Values()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/kitchen/tickets?status=Archived");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Status filter must be one of: Pending, Preparing, Ready, Served, Cancelled.", problem!.Detail);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/kitchen/tickets")]
    [InlineData("DELETE", "/api/v1/kitchen/tickets/{id}")]
    public async Task Delete_Route_Should_Not_Exist(string method, string pathTemplate)
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var path = pathTemplate.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase);
        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Theory]
    [InlineData("Pending", "Preparing", "PreparingAt")]
    [InlineData("Pending", "Ready", "ReadyAt")]
    [InlineData("Preparing", "Ready", "ReadyAt")]
    [InlineData("Ready", "Served", "ServedAt")]
    public async Task Status_Change_Should_Allow_Valid_Transitions(string startingStatus, string targetStatus, string expectedTimestampField)
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);
        if (!string.Equals(startingStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            var prepResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = startingStatus });
            prepResponse.EnsureSuccessStatusCode();
            ticket = await prepResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>() ?? throw new Xunit.Sdk.XunitException("Expected kitchen ticket payload.");
        }

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = targetStatus });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal(targetStatus, updated!.Status);

        Assert.NotNull(typeof(KitchenTicketDetailDto).GetProperty(expectedTimestampField)!.GetValue(updated));

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Contains(await context.AuditLogs.Select(entity => entity.Action).ToListAsync(), action => action == "KitchenTicket.StatusChanged");
    }

    [Fact]
    public async Task Invalid_Status_Transition_Should_Be_Rejected()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Kitchen ticket status transition is not allowed.", problem!.Detail);
    }

    [Fact]
    public async Task Cancelled_Ticket_Cannot_Change_Status()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);
        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "Customer cancelled order" });
        cancelResponse.EnsureSuccessStatusCode();

        var statusResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Ready" });

        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        var problem = await statusResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Kitchen ticket status transition is not allowed.", problem!.Detail);
    }

    [Fact]
    public async Task Served_Ticket_Cannot_Change_Status()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);
        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Ready" });
        servedResponse.EnsureSuccessStatusCode();
        var readyTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(readyTicket);

        var servedAgainResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedAgainResponse.EnsureSuccessStatusCode();

        var statusResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Preparing" });

        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        var problem = await statusResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Kitchen ticket status transition is not allowed.", problem!.Detail);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Preparing")]
    [InlineData("Ready")]
    public async Task Cancel_Should_Allow_Pending_Preparing_And_Ready_Tickets(string startingStatus)
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);
        if (!string.Equals(startingStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            var statusResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = startingStatus });
            statusResponse.EnsureSuccessStatusCode();
            ticket = await statusResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>() ?? throw new Xunit.Sdk.XunitException("Expected kitchen ticket payload.");
        }

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "Customer cancelled order" });

        cancelResponse.EnsureSuccessStatusCode();
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Customer cancelled order", cancelled.CancelReason);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.NotNull(cancelled.CancelledByUserId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Contains(await context.AuditLogs.Select(entity => entity.Action).ToListAsync(), action => action == "KitchenTicket.Cancelled");
    }

    [Fact]
    public async Task Served_Ticket_Cannot_Be_Cancelled()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);
        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Ready" });
        servedResponse.EnsureSuccessStatusCode();
        var servedAgainResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedAgainResponse.EnsureSuccessStatusCode();

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "Too late" });

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
        var problem = await cancelResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Kitchen ticket cannot be cancelled in its current status.", problem!.Detail);
    }

    [Fact]
    public async Task Cancel_Should_Require_Reason_And_Block_Repeat_Cancel()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var ticket = await fixture.CreatePendingTicketAsync(admin);

        var missingReasonResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);
        var missingReasonProblem = await missingReasonResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(missingReasonProblem);
        Assert.Equal("Cancel reason is required.", missingReasonProblem!.Detail);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "Customer cancelled order" });
        cancelResponse.EnsureSuccessStatusCode();

        var repeatCancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/cancel", new { reason = "Again" });
        Assert.Equal(HttpStatusCode.BadRequest, repeatCancelResponse.StatusCode);
        var repeatProblem = await repeatCancelResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(repeatProblem);
        Assert.Equal("Kitchen ticket is already cancelled.", repeatProblem!.Detail);
    }

    [Fact]
    public async Task Deduction_Preview_Should_Show_Recipe_And_NoRecipe_Lines()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");
        var vada = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Vada", sku: "VADA-01");
        var rice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains");
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rice.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rice.InventoryItemId, 0.50m);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0001",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 2m);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;
            var trackedOrder = await context.PosOrders
                .Include(entity => entity.PosOrderLines)
                .SingleAsync(entity => entity.PosOrderId == order.PosOrderId);

            trackedOrder.PosOrderLines.Add(new PosOrderLine
            {
                RestaurantId = admin.RestaurantId,
                PosOrderId = trackedOrder.PosOrderId,
                MenuItemId = vada.MenuItemId,
                MenuCategoryId = breakfast.MenuCategoryId,
                MenuItemNameSnapshot = vada.Name,
                MenuCategoryNameSnapshot = breakfast.Name,
                SkuSnapshot = vada.Sku,
                UnitPrice = 1.75m,
                TaxRate = 0m,
                Quantity = 1m,
                LineSubtotal = 1.75m,
                LineTax = 0m,
                LineTotal = 1.75m,
                DisplayOrder = 2,
                CreatedAt = now
            });

            trackedOrder.Subtotal = 6.75m;
            trackedOrder.TaxTotal = 0m;
            trackedOrder.GrandTotal = 6.75m;
            await context.SaveChangesAsync();
        }

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var previewResponse = await fixture.Client.GetAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/deduction-preview");
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<KitchenTicketDeductionPreviewResponseDto>();
        Assert.NotNull(preview);
        Assert.True(preview!.CanComplete);
        Assert.Contains(preview.Lines, line => line.Status == "NoRecipe");
        Assert.Contains(preview.Lines, line => line.InventoryItemName == "Rice");
    }

    [Fact]
    public async Task Served_BatchPrepared_Item_Should_Deduct_Prepared_Stock_And_Be_Idempotent()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");
        var preparedStock = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Idli Prepared", "Prepared");
        var rice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice Batter", "Raw");

        await fixture.UpdateMenuItemDeductionModeAsync(idli.MenuItemId, MenuItemInventoryDeductionMode.BatchPrepared);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, preparedStock.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rice.InventoryItemId, 10m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rice.InventoryItemId, 2m);

        var production = await fixture.Client.PostAsJsonAsync("/api/v1/inventory/batch-productions", new
        {
            branchId = admin.BranchId,
            menuItemId = idli.MenuItemId,
            quantityProduced = 3m
        });
        production.EnsureSuccessStatusCode();

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0101",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 2m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("Deducted", servedTicket!.InventoryDeductionStatus);

        var servedAgainResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedAgainResponse.EnsureSuccessStatusCode();
        var servedAgainTicket = await servedAgainResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedAgainTicket);
        Assert.Equal("Deducted", servedAgainTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var preparedMovements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == preparedStock.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var rawMovements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == rice.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var preparedStockBalance = preparedMovements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);
        var rawStockBalance = rawMovements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);
        var preparedLot = await context.InventoryLots.SingleAsync(entity => entity.InventoryItemId == preparedStock.InventoryItemId);
        var rawLot = await context.InventoryLots.SingleAsync(entity => entity.InventoryItemId == rice.InventoryItemId);
        var preparedDeduction = await context.KitchenTicketInventoryDeductions.SingleAsync(entity =>
            entity.KitchenTicketId == ticket.KitchenTicketId &&
            entity.InventoryItemId == preparedStock.InventoryItemId);

        Assert.Equal(1m, preparedStockBalance);
        Assert.Equal(4m, rawStockBalance);
        Assert.Equal(1m, preparedLot.RemainingQuantity);
        Assert.Equal(4m, rawLot.RemainingQuantity);
        Assert.Equal(1, await context.InventoryLotAllocations.CountAsync(entity => entity.InventoryMovementId == preparedDeduction.InventoryMovementId));
    }

    [Fact]
    public async Task Served_DirectStockItem_Should_Deduct_Mapped_Stock()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Drinks", 1);
        var bottledWater = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Bottled Water", sku: "WATER-01");
        var stockItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Water Bottle", "Packed");

        await fixture.UpdateMenuItemDeductionModeAsync(bottledWater.MenuItemId, MenuItemInventoryDeductionMode.DirectStockItem);
        await fixture.MapMenuItemToInventoryItemAsync(admin.RestaurantId, admin.BranchId, bottledWater.MenuItemId, stockItem.InventoryItemId);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, stockItem.InventoryItemId, 5m);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            bottledWater,
            orderNumber: "ORD-20260613-0102",
            unitPrice: 1.00m,
            taxRate: 0m,
            quantity: 2m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("Deducted", servedTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var movements = await context.InventoryMovements
            .Where(entity => entity.InventoryItemId == stockItem.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var remainingStock = movements.Sum(entity => entity.MovementType == InventoryMovementType.StockIn ? entity.Quantity : -entity.Quantity);
        var lot = await context.InventoryLots.SingleAsync(entity => entity.InventoryItemId == stockItem.InventoryItemId);
        var deduction = await context.KitchenTicketInventoryDeductions.SingleAsync(entity =>
            entity.KitchenTicketId == ticket.KitchenTicketId &&
            entity.InventoryItemId == stockItem.InventoryItemId);

        Assert.Equal(3m, remainingStock);
        Assert.Equal(3m, lot.RemainingQuantity);
        Assert.Equal(1, await context.InventoryLotAllocations.CountAsync(entity => entity.InventoryMovementId == deduction.InventoryMovementId));
    }

    [Fact]
    public async Task Served_NoDeduction_Item_Should_Not_Create_Stock_Movement()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var category = await fixture.InsertCategoryAsync(admin.RestaurantId, "Extras", 1);
        var freeItem = await fixture.InsertItemAsync(admin.RestaurantId, category.MenuCategoryId, "Complimentary Papad", sku: "FREE-01");

        await fixture.UpdateMenuItemDeductionModeAsync(freeItem.MenuItemId, MenuItemInventoryDeductionMode.NoDeduction);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            category,
            freeItem,
            orderNumber: "ORD-20260613-0103",
            unitPrice: 0m,
            taxRate: 0m,
            quantity: 1m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("Deducted", servedTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Empty(await context.InventoryMovements.Where(entity => entity.RestaurantId == admin.RestaurantId && entity.BranchId == admin.BranchId).ToListAsync());
    }

    [Fact]
    public async Task Served_Completion_Should_Deduct_Inventory_Once_And_Be_Idempotent()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");
        var rice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains");
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rice.InventoryItemId, 5m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rice.InventoryItemId, 1m);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0002",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 2m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("Deducted", servedTicket!.InventoryDeductionStatus);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var currentStock = await context.InventoryMovements
                .AsNoTracking()
                .Where(movement => movement.InventoryItemId == rice.InventoryItemId)
                .Select(movement => new { movement.MovementType, movement.Quantity })
                .ToListAsync();

            var resultingStock = currentStock.Sum(entry => entry.MovementType == InventoryMovementType.StockIn ? entry.Quantity : -entry.Quantity);
            Assert.Equal(3m, resultingStock);

            var lot = await context.InventoryLots.SingleAsync(entity => entity.InventoryItemId == rice.InventoryItemId);
            var deduction = await context.KitchenTicketInventoryDeductions.SingleAsync(entity =>
                entity.KitchenTicketId == ticket.KitchenTicketId &&
                entity.InventoryItemId == rice.InventoryItemId);

            Assert.Equal(3m, lot.RemainingQuantity);
            Assert.Equal(1, await context.InventoryLotAllocations.CountAsync(entity => entity.InventoryMovementId == deduction.InventoryMovementId));

            var deductions = await context.Set<KitchenTicketInventoryDeduction>()
                .AsNoTracking()
                .Where(entity => entity.KitchenTicketId == ticket.KitchenTicketId)
                .ToListAsync();

            Assert.Single(deductions);
        }

        var retryResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        retryResponse.EnsureSuccessStatusCode();
        var retryTicket = await retryResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(retryTicket);
        Assert.Equal("Deducted", retryTicket!.InventoryDeductionStatus);

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var deductionCount = await context.Set<KitchenTicketInventoryDeduction>()
                .AsNoTracking()
                .CountAsync(entity => entity.KitchenTicketId == ticket.KitchenTicketId);

            Assert.Equal(1, deductionCount);
        }
    }

    [Fact]
    public async Task Served_Completion_Should_Reject_When_Stock_Is_Insufficient()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");
        var rice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains");
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, rice.InventoryItemId, 1m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, rice.InventoryItemId, 2m);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0003",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });

        Assert.Equal(HttpStatusCode.BadRequest, servedResponse.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var deductionCount = await context.Set<KitchenTicketInventoryDeduction>()
            .AsNoTracking()
            .CountAsync(entity => entity.KitchenTicketId == ticket.KitchenTicketId);

        Assert.Equal(0, deductionCount);
    }

    [Fact]
    public async Task Served_Completion_With_No_Recipe_Should_Show_Deduction_Warning()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0004",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("DeductionWarning", servedTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var deductionCount = await context.Set<KitchenTicketInventoryDeduction>()
            .AsNoTracking()
            .CountAsync(entity => entity.KitchenTicketId == ticket.KitchenTicketId);

        Assert.Equal(0, deductionCount);
    }

    [Fact]
    public async Task Cancelled_Ticket_Should_Remain_Not_Deducted()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0005",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/cancel", new { reason = "Customer changed mind" });
        cancelResponse.EnsureSuccessStatusCode();
        var cancelledTicket = await cancelResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(cancelledTicket);
        Assert.Equal("NotDeducted", cancelledTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var deductionCount = await context.Set<KitchenTicketInventoryDeduction>()
            .AsNoTracking()
            .CountAsync(entity => entity.KitchenTicketId == ticket.KitchenTicketId);

        Assert.Equal(0, deductionCount);
    }

    [Fact]
    public async Task Served_Completion_Should_Stay_Within_Source_Branch()
    {
        await using var fixture = await KitchenTicketApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var secondBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");
        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var idli = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Idli", sku: "IDLI-01");
        var branchARice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice A", "Grains");
        var branchBRice = await fixture.InsertInventoryItemAsync(admin.RestaurantId, secondBranch.BranchId, "Rice B", "Grains");
        await fixture.AddStockInMovementAsync(admin.RestaurantId, admin.BranchId, admin.UserId, branchARice.InventoryItemId, 5m);
        await fixture.AddStockInMovementAsync(admin.RestaurantId, secondBranch.BranchId, admin.UserId, branchBRice.InventoryItemId, 5m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, admin.BranchId, idli.MenuItemId, branchARice.InventoryItemId, 1m);
        await fixture.AddRecipeIngredientAsync(admin.RestaurantId, secondBranch.BranchId, idli.MenuItemId, branchBRice.InventoryItemId, 1m);

        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            idli,
            orderNumber: "ORD-20260613-0006",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 2m);

        var ticketResponse = await fixture.Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(ticket);

        var readyResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket!.KitchenTicketId}/status", new { status = "Ready" });
        readyResponse.EnsureSuccessStatusCode();

        var servedResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/kitchen/tickets/{ticket.KitchenTicketId}/status", new { status = "Served" });
        servedResponse.EnsureSuccessStatusCode();
        var servedTicket = await servedResponse.Content.ReadFromJsonAsync<KitchenTicketDetailDto>();
        Assert.NotNull(servedTicket);
        Assert.Equal("Deducted", servedTicket!.InventoryDeductionStatus);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var branchAStock = await context.InventoryMovements
            .AsNoTracking()
            .Where(entity => entity.InventoryItemId == branchARice.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();
        var branchBStock = await context.InventoryMovements
            .AsNoTracking()
            .Where(entity => entity.InventoryItemId == branchBRice.InventoryItemId)
            .Select(entity => new { entity.MovementType, entity.Quantity })
            .ToListAsync();

        Assert.Equal(3m, branchAStock.Sum(entry => entry.MovementType == InventoryMovementType.StockIn ? entry.Quantity : -entry.Quantity));
        Assert.Equal(5m, branchBStock.Sum(entry => entry.MovementType == InventoryMovementType.StockIn ? entry.Quantity : -entry.Quantity));
    }

    private sealed class KitchenTicketApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<KitchenTicketApiFactory> CreateAsync()
        {
            var factory = new KitchenTicketApiFactory();
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

        public async Task<SeedResult> SeedForeignSystemUserAsync(string roleName)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = $"Foreign {roleName} Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode($"RESTF{roleName[..1].ToUpperInvariant()}01");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var branch = new Branch
            {
                Name = "Foreign Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"Foreign {roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000030");

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

        public async Task<SeedResult> SeedRoleUserAsync(Guid restaurantId, Guid branchId, string roleName, string mobileNumber)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = await context.Restaurants.SingleAsync(entity => entity.RestaurantId == restaurantId);
            var branch = await context.Branches.SingleAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId);
            var role = await context.Roles.SingleAsync(entity => entity.RestaurantId == null && entity.Name == roleName);

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(restaurant.CountryCode, mobileNumber);

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

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

        public async Task<InventoryItem> InsertInventoryItemAsync(
            Guid restaurantId,
            Guid branchId,
            string name,
            string category,
            string unitOfMeasure = "kg")
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
                LowStockThreshold = 1m,
                IsActive = true,
                CreatedAtUtc = now
            };

            context.InventoryItems.Add(item);
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

        public async Task<Branch> InsertBranchAsync(Guid restaurantId, string name)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var branch = new Branch
            {
                RestaurantId = restaurantId,
                Name = name,
                Status = BranchStatus.Active,
                CountryCode = "SG",
                TimeZoneId = "Asia/Singapore",
                CurrencyCode = "SGD"
            };

            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            return branch;
        }

        public async Task UpdateMenuCategoryNameAsync(Guid categoryId, string name)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var category = await context.MenuCategories.SingleAsync(entity => entity.MenuCategoryId == categoryId);
            category.UpdateProfile(name, category.DisplayOrder);
            await context.SaveChangesAsync();
        }

        public async Task UpdateMenuItemAsync(Guid itemId, string name, string? sku = null, decimal? basePrice = null, decimal? taxRate = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var item = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == itemId);
            item.UpdateProfile(
                item.MenuCategoryId,
                name,
                item.Description,
                sku ?? item.Sku,
                basePrice ?? item.BasePrice,
                taxRate ?? item.TaxRate,
                item.IsVegetarian,
                item.IsAvailableForEatIn,
                item.IsAvailableForParcel,
                item.InventoryDeductionMode);
            await context.SaveChangesAsync();
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

        public async Task<PosOrder> InsertOrderAsync(
            Guid restaurantId,
            Guid branchId,
            MenuCategory category,
            MenuItem item,
            string orderNumber,
            PosOrderStatus status,
            PosOrderType orderType = PosOrderType.EatIn)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var order = new PosOrder
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderNumber = orderNumber,
                OrderType = orderType,
                Status = status,
                Subtotal = 2.50m,
                TaxTotal = 0m,
                GrandTotal = 2.50m,
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
                UnitPrice = 2.50m,
                TaxRate = 0m,
                Quantity = 1m,
                LineSubtotal = 2.50m,
                LineTax = 0m,
                LineTotal = 2.50m,
                CreatedAt = now
            });

            context.PosOrders.Add(order);
            await context.SaveChangesAsync();
            return order;
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

        public async Task<KitchenTicketDetailDto> CreatePendingTicketAsync(SeedResult seed)
        {
            var category = await InsertCategoryAsync(seed.RestaurantId, "Breakfast", 1);
            var item = await InsertItemAsync(seed.RestaurantId, category.MenuCategoryId, "Idli");
            var order = await InsertConfirmedOrderAsync(seed.RestaurantId, seed.BranchId, category, item, $"ORD-{DateTime.UtcNow:yyyyMMdd}-0001", 2.50m, 0m, 1m);
            var response = await Client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { posOrderId = order.PosOrderId });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<KitchenTicketDetailDto>() ?? throw new Xunit.Sdk.XunitException("Expected kitchen ticket payload.");
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

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
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

    private sealed record KitchenTicketListResponseDto(KitchenTicketListItemDto[] Items);

    private sealed record KitchenTicketListItemDto(
        Guid KitchenTicketId,
        Guid BranchId,
        Guid PosOrderId,
        string TicketNumber,
        string OrderNumberSnapshot,
        string OrderTypeSnapshot,
        string Status,
        int LineCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record KitchenTicketDetailDto(
        Guid KitchenTicketId,
        Guid RestaurantId,
        Guid BranchId,
        Guid PosOrderId,
        string TicketNumber,
        string OrderNumberSnapshot,
        string OrderTypeSnapshot,
        string Status,
        Guid? CreatedByUserId,
        Guid? LastStatusChangedByUserId,
        Guid? CancelledByUserId,
        DateTimeOffset? CancelledAt,
        string? CancelReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset? PreparingAt,
        DateTimeOffset? ReadyAt,
        DateTimeOffset? ServedAt,
        string InventoryDeductionStatus,
        KitchenTicketLineDto[] Lines);

    private sealed record KitchenTicketLineDto(
        Guid KitchenTicketLineId,
        Guid PosOrderLineId,
        Guid MenuItemId,
        Guid MenuCategoryId,
        string MenuItemNameSnapshot,
        string MenuCategoryNameSnapshot,
        string? SkuSnapshot,
        decimal Quantity,
        string? Notes,
        int DisplayOrder,
        DateTimeOffset CreatedAt);

    private sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);

    private sealed record KitchenTicketDeductionPreviewResponseDto(Guid KitchenTicketId, bool CanComplete, KitchenTicketDeductionPreviewLineDto[] Lines);

    private sealed record KitchenTicketDeductionPreviewLineDto(
        string MenuItemName,
        string? InventoryItemName,
        decimal RequiredQuantity,
        decimal AvailableQuantity,
        decimal ResultingQuantity,
        string Status);
}

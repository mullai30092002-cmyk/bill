using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BillSoft.Domain.Billing;
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

public sealed class BillingEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/billing/bills");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Billing_Permission()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("KitchenUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/billing/bills");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Billing_View_Should_List_And_Get_Bills()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 2.50m, taxRate: 5m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            dosa,
            orderNumber: "ORD-20260612-0001",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 2m,
            orderType: PosOrderType.EatIn);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        if (!createResponse.IsSuccessStatusCode)
        {
            var failureBody = await createResponse.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected bill create success, got {(int)createResponse.StatusCode}: {failureBody}");
        }
        var createdBill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(createdBill);

        var waiter = await fixture.SeedRoleUserAsync(admin.RestaurantId, admin.BranchId, "Waiter", "90000036");
        await fixture.AuthenticateAsync(waiter);

        var listResponse = await fixture.Client.GetAsync("/api/v1/billing/bills?search=ORD-20260612-0001");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<BillListResponseDto>();
        Assert.NotNull(listPayload);
        Assert.Single(listPayload!.Items);
        Assert.Equal(createdBill!.BillId, listPayload.Items[0].BillId);

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/billing/bills/{createdBill.BillId}");
        detailResponse.EnsureSuccessStatusCode();
        var detailPayload = await detailResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(detailPayload);
        Assert.Single(detailPayload!.Lines);
        Assert.Empty(detailPayload.Payments);
        Assert.Equal("Masala Dosa", detailPayload.Lines[0].MenuItemNameSnapshot);
        Assert.Equal(2.50m, detailPayload.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Create_Bill_Should_Return_And_Filter_By_Business_Date_And_Branch()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 2.50m, taxRate: 5m, sku: "DOSA-01");
        var billDate = DateTime.UtcNow.Date;
        var otherDate = billDate.AddDays(-1);

        var firstOrder = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            dosa,
            orderNumber: $"ORD-{billDate:yyyyMMdd}-0001",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 1m,
            orderType: PosOrderType.EatIn);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = firstOrder.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var createdBill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(createdBill);
        Assert.Equal(billDate, createdBill!.BusinessDate);

        var secondOrder = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            dosa,
            orderNumber: $"ORD-{billDate:yyyyMMdd}-0002",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 1m,
            orderType: PosOrderType.EatIn);

        var secondCreateResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = secondOrder.PosOrderId });
        secondCreateResponse.EnsureSuccessStatusCode();
        var secondBill = await secondCreateResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(secondBill);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bills SET BusinessDate = {otherDate} WHERE BillId = {createdBill.BillId}");
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bills SET BusinessDate = {billDate} WHERE BillId = {secondBill!.BillId}");
        }

        var listResponse = await fixture.Client.GetAsync($"/api/v1/billing/bills?businessDate={billDate:yyyy-MM-dd}&branchId={admin.BranchId}");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<BillListResponseDto>();
        Assert.NotNull(listPayload);
        Assert.Single(listPayload!.Items);
        Assert.Equal(secondBill!.BillId, listPayload.Items[0].BillId);
        Assert.Equal(billDate, listPayload.Items[0].BusinessDate);
    }

    [Fact]
    public async Task Create_Bill_Should_Copy_Pos_Order_Snapshots_And_Use_Server_Numbering()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 2.50m, taxRate: 5m, sku: "DOSA-01");
        var firstOrder = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            dosa,
            orderNumber: $"ORD-{today}-0001",
            unitPrice: 2.50m,
            taxRate: 5m,
            quantity: 2m);

        await fixture.UpdateMenuItemPriceAsync(dosa.MenuItemId, 9.99m);

        var firstBillResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = firstOrder.PosOrderId });
        firstBillResponse.EnsureSuccessStatusCode();
        var firstBill = await firstBillResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(firstBill);
        Assert.Equal($"BILL-{today}-0001", firstBill!.BillNumber);
        Assert.Equal(5.00m, firstBill.Subtotal);
        Assert.Equal(0.25m, firstBill.TaxTotal);
        Assert.Equal(5.25m, firstBill.GrandTotal);
        Assert.Single(firstBill.Lines);
        Assert.Equal(2.50m, firstBill.Lines[0].UnitPrice);
        Assert.Equal(5m, firstBill.Lines[0].TaxRate);

        var secondOrder = await fixture.InsertConfirmedOrderAsync(
            admin.RestaurantId,
            admin.BranchId,
            breakfast,
            dosa,
            orderNumber: $"ORD-{today}-0002",
            unitPrice: 9.99m,
            taxRate: 5m,
            quantity: 1m);

        var secondBillResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = secondOrder.PosOrderId });
        secondBillResponse.EnsureSuccessStatusCode();
        var secondBill = await secondBillResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(secondBill);
        Assert.Equal($"BILL-{today}-0002", secondBill!.BillNumber);
        Assert.Equal(9.99m, secondBill.Lines[0].UnitPrice);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(2, await context.AuditLogs.CountAsync(entity => entity.Action == "Bill.Created"));
    }

    [Fact]
    public async Task Create_Bill_Should_Reject_Draft_And_Cancelled_Pos_Orders()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var draftOrder = await fixture.InsertOrderAsync(admin.RestaurantId, admin.BranchId, PosOrderStatus.Draft, "ORD-20260612-0001");
        var cancelledOrder = await fixture.InsertOrderAsync(admin.RestaurantId, admin.BranchId, PosOrderStatus.Cancelled, "ORD-20260612-0002");

        var draftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = draftOrder.PosOrderId });
        var cancelledResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = cancelledOrder.PosOrderId });

        Assert.Equal(HttpStatusCode.BadRequest, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancelledResponse.StatusCode);
    }

    [Fact]
    public async Task Create_Bill_Should_Reject_Orders_From_Another_Restaurant()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var foreign = await fixture.SeedForeignRestaurantAsync();
        var foreignOrder = await fixture.InsertOrderAsync(foreign.RestaurantId, foreign.BranchId, PosOrderStatus.Confirmed, "ORD-20260612-0001");

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = foreignOrder.PosOrderId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Active_Bill_Should_Be_Rejected()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 2.50m, taxRate: 5m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, breakfast, dosa, "ORD-20260612-0001", 2.50m, 5m, 1m);

        var firstResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("A bill already exists for this POS order.", problem!.Detail);
    }

    [Fact]
    public async Task Cancel_Bill_Should_Work_Without_Payments_And_Be_Blocked_When_Payments_Exist()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 4.00m, taxRate: 0m, sku: "DOSA-01");
        var billableOrder = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, breakfast, dosa, "ORD-20260612-0001", 4m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = billableOrder.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/cancel", new { reason = "Customer cancelled" });
        cancelResponse.EnsureSuccessStatusCode();
        var cancelledBill = await cancelResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(cancelledBill);
        Assert.Equal("Cancelled", cancelledBill!.Status);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Contains(await context.AuditLogs.Select(entity => entity.Action).ToListAsync(), action => action == "Bill.Cancelled");

        var secondOrder = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, breakfast, dosa, "ORD-20260612-0002", 4m, 0m, 1m);
        var secondBillResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = secondOrder.PosOrderId });
        secondBillResponse.EnsureSuccessStatusCode();
        var secondBill = await secondBillResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(secondBill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.BranchId,
            openingCashAmount = 100m,
            openingNote = "Morning shift"
        });
        openShiftResponse.EnsureSuccessStatusCode();

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{secondBill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 4m,
            referenceNumber = "",
            notes = ""
        });
        paymentResponse.EnsureSuccessStatusCode();

        var blockedCancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{secondBill.BillId}/cancel", new { reason = "Too late" });
        Assert.Equal(HttpStatusCode.BadRequest, blockedCancelResponse.StatusCode);
    }

    [Fact]
    public async Task Record_Payment_Should_Update_Bill_Status_And_Numbering()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 2m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashier.BranchId,
            openingCashAmount = 10m,
            openingNote = "Counter float"
        });
        openShiftResponse.EnsureSuccessStatusCode();

        var partialResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 4m,
            referenceNumber = "",
            notes = "First payment"
        });
        partialResponse.EnsureSuccessStatusCode();
        var partiallyPaid = await partialResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(partiallyPaid);
        var paymentDate = DateTime.UtcNow.ToString("yyyyMMdd");
        Assert.Equal("PartiallyPaid", partiallyPaid!.Status);
        Assert.Equal(4m, partiallyPaid.AmountPaid);
        Assert.Equal(6m, partiallyPaid.BalanceDue);
        Assert.Equal($"PAY-{paymentDate}-0001", partiallyPaid.Payments[0].PaymentNumber);

        var fullResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill.BillId}/payments", new
        {
            paymentMode = "Card",
            amount = 6m,
            referenceNumber = "CARD-1",
            notes = "Second payment"
        });
        fullResponse.EnsureSuccessStatusCode();
        var paidBill = await fullResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(paidBill);
        Assert.Equal("Paid", paidBill!.Status);
        Assert.Equal(10m, paidBill.AmountPaid);
        Assert.Equal(0m, paidBill.BalanceDue);
        Assert.Equal($"PAY-{paymentDate}-0002", paidBill.Payments[1].PaymentNumber);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(2, await context.AuditLogs.CountAsync(entity => entity.Action == "Payment.Recorded"));
    }

    [Fact]
    public async Task Cancel_Payment_Should_Recalculate_Bill_And_Be_Blocked_When_Bill_Is_Cancelled()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 2m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashier.BranchId,
            openingCashAmount = 10m,
            openingNote = "Counter float"
        });
        openShiftResponse.EnsureSuccessStatusCode();

        var firstPaymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 4m,
            referenceNumber = "",
            notes = "First payment"
        });
        firstPaymentResponse.EnsureSuccessStatusCode();
        var paidBill = await firstPaymentResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(paidBill);
        var paymentId = paidBill!.Payments[0].PaymentId;

        var cancelPaymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/payments/{paymentId}/cancel", new { reason = "Wrong amount entered" });
        cancelPaymentResponse.EnsureSuccessStatusCode();
        var recalculatedBill = await cancelPaymentResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(recalculatedBill);
        Assert.Equal("Unpaid", recalculatedBill!.Status);
        Assert.Equal(0m, recalculatedBill.AmountPaid);
        Assert.Equal(10m, recalculatedBill.BalanceDue);

        var cancelledOrder = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0002", 5m, 0m, 2m);
        var cancelledBillResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = cancelledOrder.PosOrderId });
        cancelledBillResponse.EnsureSuccessStatusCode();
        var cancelledBill = await cancelledBillResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(cancelledBill);

        var recordedPaymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{cancelledBill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 4m,
            referenceNumber = "",
            notes = "Recorded on active bill"
        });
        recordedPaymentResponse.EnsureSuccessStatusCode();
        var recordedBill = await recordedPaymentResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(recordedBill);
        var recordedPaymentId = recordedBill!.Payments[0].PaymentId;

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var trackedBill = await context.Bills.SingleAsync(entity => entity.BillId == cancelledBill.BillId);
            trackedBill.Status = BillStatus.Cancelled;
            trackedBill.CancelledAt = DateTimeOffset.UtcNow;
            trackedBill.CancelledByUserId = cashier.UserId;
            trackedBill.CancelReason = "Manual test state";
            await context.SaveChangesAsync();
        }

        var blockedCancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/payments/{recordedPaymentId}/cancel", new { reason = "Too late" });
        Assert.Equal(HttpStatusCode.BadRequest, blockedCancelResponse.StatusCode);

        await using var auditScope = fixture.Services.CreateAsyncScope();
        var auditContext = auditScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(1, await auditContext.AuditLogs.CountAsync(entity => entity.Action == "Payment.Cancelled"));
    }

    [Fact]
    public async Task Invalid_Status_And_Payment_Mode_Should_Return_Enum_Derived_Allowed_Values()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var statusResponse = await fixture.Client.GetAsync("/api/v1/billing/bills?status=Archived");
        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        var statusProblem = await statusResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(statusProblem);
        Assert.Equal($"Status filter must be one of: {string.Join(", ", Enum.GetNames<BillStatus>())}.", statusProblem!.Detail);

        var breakfast = await fixture.InsertCategoryAsync(admin.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(admin.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa");
        var order = await fixture.InsertConfirmedOrderAsync(admin.RestaurantId, admin.BranchId, breakfast, dosa, "ORD-20260612-0001", 2.50m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Wire",
            amount = 1m,
            referenceNumber = "",
            notes = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
        var paymentProblem = await paymentResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(paymentProblem);
        Assert.Equal($"Payment mode must be one of: {string.Join(", ", Enum.GetNames<PaymentMode>())}.", paymentProblem!.Detail);
    }

    [Fact]
    public async Task Cash_Payment_Should_Require_Open_Cashier_Shift()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = "",
            notes = "Cash payment without shift"
        });

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
        var problem = await paymentResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Open cashier shift is required for cash payments.", problem!.Detail);
    }

    [Fact]
    public async Task Cash_Payment_Should_Link_To_Open_Shift_Without_Changing_Expected_Cash()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashier.BranchId,
            openingCashAmount = 20m,
            openingNote = "Counter float"
        });
        openShiftResponse.EnsureSuccessStatusCode();
        var openedShift = await openShiftResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = "",
            notes = "Cash payment with shift"
        });
        paymentResponse.EnsureSuccessStatusCode();

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}");
        detailResponse.EnsureSuccessStatusCode();
        var shiftDetail = await detailResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(shiftDetail);
        Assert.Equal(20m, shiftDetail!.ExpectedClosingCashAmount);
        Assert.Equal(20m, shiftDetail.OpeningCashAmount);
    }

    [Fact]
    public async Task Cash_Payment_Should_Reject_Another_Cashiers_Open_Shift()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashierA = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashierA);
        var breakfast = await fixture.InsertCategoryAsync(cashierA.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashierA.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashierA.RestaurantId, cashierA.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var cashierB = await fixture.SeedRoleUserAsync(cashierA.RestaurantId, cashierA.BranchId, "Cashier", "90000005");
        await fixture.AuthenticateAsync(cashierB);
        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashierB.BranchId,
            openingCashAmount = 20m,
            openingNote = "Another cashier's shift"
        });
        openShiftResponse.EnsureSuccessStatusCode();

        await fixture.AuthenticateAsync(cashierA);
        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = "",
            notes = "Cash payment without current shift"
        });

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
        var problem = await paymentResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Open cashier shift is required for cash payments.", problem!.Detail);
    }

    [Fact]
    public async Task Cancelling_A_Cash_Payment_Should_Leave_Open_Shift_Expected_Cash_Unaffected()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashier.BranchId,
            openingCashAmount = 20m,
            openingNote = "Counter float"
        });
        openShiftResponse.EnsureSuccessStatusCode();
        var openedShift = await openShiftResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = "",
            notes = "Cash payment with shift"
        });
        paymentResponse.EnsureSuccessStatusCode();
        var paidBill = await paymentResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(paidBill);
        var paymentId = paidBill!.Payments[0].PaymentId;

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/payments/{paymentId}/cancel", new { reason = "Wrong amount entered" });
        cancelResponse.EnsureSuccessStatusCode();

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}");
        detailResponse.EnsureSuccessStatusCode();
        var shiftDetail = await detailResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(shiftDetail);
        Assert.Equal(20m, shiftDetail!.ExpectedClosingCashAmount);
        Assert.Equal(20m, shiftDetail.OpeningCashAmount);
    }

    [Fact]
    public async Task Cancelling_A_Cash_Payment_On_A_Closed_Shift_Should_Be_Rejected()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);
        var breakfast = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var dosa = await fixture.InsertItemAsync(cashier.RestaurantId, breakfast.MenuCategoryId, "Masala Dosa", basePrice: 5.00m, taxRate: 0m, sku: "DOSA-01");
        var order = await fixture.InsertConfirmedOrderAsync(cashier.RestaurantId, cashier.BranchId, breakfast, dosa, "ORD-20260612-0001", 5m, 0m, 1m);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var openShiftResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = cashier.BranchId,
            openingCashAmount = 20m,
            openingNote = "Counter float"
        });
        openShiftResponse.EnsureSuccessStatusCode();
        var openedShift = await openShiftResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var paymentResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/payments", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = "",
            notes = "Cash payment with shift"
        });
        paymentResponse.EnsureSuccessStatusCode();
        var paidBill = await paymentResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(paidBill);
        var paymentId = paidBill!.Payments[0].PaymentId;

        var closeResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}/close", new
        {
            countedCashAmount = 25m,
            closingNote = "Close shift"
        });
        closeResponse.EnsureSuccessStatusCode();

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/payments/{paymentId}/cancel", new { reason = "Too late" });
        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/billing/bills")]
    [InlineData("DELETE", "/api/v1/billing/payments/{id}")]
    public async Task Delete_Routes_Should_Not_Exist(string method, string pathTemplate)
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var path = pathTemplate.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase);
        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Receipt_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/billing/bills/{Guid.NewGuid():D}/receipt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_Should_Return_403_When_User_Lacks_Billing_Permission()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("KitchenUser");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync($"/api/v1/billing/bills/{Guid.NewGuid():D}/receipt");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_Should_Return_Snapshots_And_Not_Read_Current_Menu_State()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);

        var category = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(cashier.RestaurantId, category.MenuCategoryId, "Masala Dosa", basePrice: 25m, taxRate: 0m, sku: "DOSA-01");
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var branch = await context.Branches.SingleAsync(entity => entity.BranchId == cashier.BranchId);
            branch.Address = "12 Market Street, Singapore";
            await context.SaveChangesAsync();
        }

        var order = await fixture.InsertConfirmedOrderAsync(
            cashier.RestaurantId,
            cashier.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0001",
            unitPrice: 25m,
            taxRate: 0m,
            quantity: 1m,
            tableName: "Table 12",
            customerName: "Walk-in customer",
            customerMobile: "90000000");

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var bill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        await fixture.UpdateMenuItemPriceAsync(item.MenuItemId, 99m);

        var receiptResponse = await fixture.Client.GetAsync($"/api/v1/billing/bills/{bill!.BillId}/receipt");
        receiptResponse.EnsureSuccessStatusCode();
        var receipt = await receiptResponse.Content.ReadFromJsonAsync<BillReceiptDto>();
        var today = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(receipt);
        Assert.Equal(bill.BillId, receipt!.BillId);
        Assert.Equal("12 Market Street, Singapore", receipt.BranchAddress);
        Assert.Equal(bill.BusinessDate, receipt.BusinessDate);
        Assert.Equal($"BILL-{today}-0001", receipt.BillNumber);
        Assert.Equal("ORD-20260613-0001", receipt.OrderNumberSnapshot);
        Assert.Equal("EatIn", receipt.OrderTypeSnapshot);
        Assert.Equal("Table 12", receipt.OrderTableNameSnapshot);
        Assert.Equal("Walk-in customer", receipt.OrderCustomerNameSnapshot);
        Assert.Equal("90000000", receipt.OrderCustomerMobileSnapshot);
        Assert.Equal(25m, receipt.Lines[0].UnitPrice);
        Assert.Equal(25m, receipt.Lines[0].LineTotal);
        Assert.Equal(0, receipt.PrintCount);
        Assert.False(receipt.IsReprint);
        Assert.Empty(receipt.Payments);
    }

    [Fact]
    public async Task Receipt_Should_Return_Cancelled_Status_And_Cancel_Reason()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);

        var category = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(cashier.RestaurantId, category.MenuCategoryId, "Masala Dosa");
        var order = await fixture.InsertConfirmedOrderAsync(
            cashier.RestaurantId,
            cashier.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0004",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var bill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/cancel", new { reason = "Customer requested" });
        cancelResponse.EnsureSuccessStatusCode();

        var receiptResponse = await fixture.Client.GetAsync($"/api/v1/billing/bills/{bill.BillId}/receipt");
        receiptResponse.EnsureSuccessStatusCode();
        var receipt = await receiptResponse.Content.ReadFromJsonAsync<BillReceiptDto>();

        Assert.NotNull(receipt);
        Assert.Equal("Cancelled", receipt!.Status);
        Assert.Equal("Customer requested", receipt.CancelReason);
        Assert.Equal(cashier.UserId, receipt.CancelledByUserId);
        Assert.Equal(0, receipt.PrintCount);
        Assert.False(receipt.IsReprint);
        Assert.Equal(2.50m, receipt.Subtotal);
        Assert.Equal(2.50m, receipt.GrandTotal);
        Assert.Equal(0m, receipt.AmountPaid);
        Assert.Equal(2.50m, receipt.BalanceDue);
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public async Task Receipt_Get_Should_Not_Create_Print_Event()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);

        var category = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(cashier.RestaurantId, category.MenuCategoryId, "Masala Dosa");
        var order = await fixture.InsertConfirmedOrderAsync(
            cashier.RestaurantId,
            cashier.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0002",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var bill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var receiptResponse = await fixture.Client.GetAsync($"/api/v1/billing/bills/{bill!.BillId}/receipt");
        receiptResponse.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(0, await context.BillPrintEvents.CountAsync(entity => entity.BillId == bill.BillId));
        Assert.Equal(0, await context.AuditLogs.CountAsync(entity => entity.EntityType == "BillPrintEvent"));
    }

    [Fact]
    public async Task Receipt_Print_Should_Create_Print_Event_Sequence_And_Audit_Row()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(cashier);

        var category = await fixture.InsertCategoryAsync(cashier.RestaurantId, "Breakfast", 1);
        var item = await fixture.InsertItemAsync(cashier.RestaurantId, category.MenuCategoryId, "Masala Dosa");
        var order = await fixture.InsertConfirmedOrderAsync(
            cashier.RestaurantId,
            cashier.BranchId,
            category,
            item,
            orderNumber: "ORD-20260613-0003",
            unitPrice: 2.50m,
            taxRate: 0m,
            quantity: 1m);

        var createResponse = await fixture.Client.PostAsJsonAsync("/api/v1/billing/bills", new { posOrderId = order.PosOrderId });
        createResponse.EnsureSuccessStatusCode();
        var bill = await createResponse.Content.ReadFromJsonAsync<BillDetailDto>();
        Assert.NotNull(bill);

        var firstPrintResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill!.BillId}/receipt/print-events", new { });
        firstPrintResponse.EnsureSuccessStatusCode();
        var firstReceipt = await firstPrintResponse.Content.ReadFromJsonAsync<BillReceiptDto>();
        Assert.NotNull(firstReceipt);
        Assert.Equal(1, firstReceipt!.PrintCount);
        Assert.False(firstReceipt.IsReprint);

        var secondPrintResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/billing/bills/{bill.BillId}/receipt/print-events", new { });
        secondPrintResponse.EnsureSuccessStatusCode();
        var secondReceipt = await secondPrintResponse.Content.ReadFromJsonAsync<BillReceiptDto>();
        Assert.NotNull(secondReceipt);
        Assert.Equal(2, secondReceipt!.PrintCount);
        Assert.True(secondReceipt.IsReprint);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(2, await context.BillPrintEvents.CountAsync(entity => entity.BillId == bill.BillId));

        var trackedBill = await context.Bills.SingleAsync(entity => entity.BillId == bill.BillId);
        Assert.Equal(2.50m, trackedBill.GrandTotal);
        Assert.Equal(0m, trackedBill.AmountPaid);
        Assert.Equal(2.50m, trackedBill.BalanceDue);
        Assert.Empty(await context.Payments.Where(entity => entity.BillId == bill.BillId).ToListAsync());
        Assert.Empty(await context.CashierShifts.Where(entity => entity.BranchId == cashier.BranchId).ToListAsync());

        var auditEntries = (await context.AuditLogs
            .Where(entity => entity.Action == "Bill.ReceiptPrinted" && entity.EntityId != null && entity.EntityId != string.Empty)
            .ToListAsync())
            .OrderBy(entity => entity.CreatedAt)
            .ThenBy(entity => entity.AuditLogId)
            .ToList();

        Assert.Equal(2, auditEntries.Count);

        using var firstAuditJson = JsonDocument.Parse(auditEntries[0].NewValueJson!);
        using var secondAuditJson = JsonDocument.Parse(auditEntries[1].NewValueJson!);

        Assert.Equal(1, firstAuditJson.RootElement.GetProperty("PrintSequence").GetInt32());
        Assert.False(firstAuditJson.RootElement.GetProperty("isReprint").GetBoolean());
        Assert.Equal(2, secondAuditJson.RootElement.GetProperty("PrintSequence").GetInt32());
        Assert.True(secondAuditJson.RootElement.GetProperty("isReprint").GetBoolean());
    }

    [Fact]
    public async Task Receipt_Should_Return_404_For_Bill_In_Another_Restaurant()
    {
        await using var fixture = await BillingApiFactory.CreateAsync();
        var cashier = await fixture.SeedSystemUserAsync("Cashier");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(cashier);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var category = new MenuCategory
            {
                RestaurantId = foreign.RestaurantId,
                Name = "Foreign Category",
                DisplayOrder = 1,
                Status = MenuCategoryStatus.Active
            };
            var item = new MenuItem
            {
                RestaurantId = foreign.RestaurantId,
                MenuCategoryId = category.MenuCategoryId,
                Name = "Foreign Item",
                BasePrice = 10m,
                TaxRate = 0m,
                IsVegetarian = true,
                IsAvailableForEatIn = true,
                IsAvailableForParcel = true,
                Status = MenuItemStatus.Active
            };
            var line = new PosOrderLine
            {
                RestaurantId = foreign.RestaurantId,
                MenuItemId = item.MenuItemId,
                MenuCategoryId = category.MenuCategoryId,
                MenuItemNameSnapshot = item.Name,
                MenuCategoryNameSnapshot = category.Name,
                SkuSnapshot = "FOREIGN-1",
                UnitPrice = 10m,
                TaxRate = 0m,
                Quantity = 1m,
                LineSubtotal = 10m,
                LineTax = 0m,
                LineTotal = 10m,
                Notes = null,
                DisplayOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var bill = new Bill
            {
                RestaurantId = foreign.RestaurantId,
                BranchId = foreign.BranchId,
                BillNumber = "BILL-20260613-9999",
                Status = BillStatus.Unpaid,
                Subtotal = 10m,
                TaxTotal = 0m,
                GrandTotal = 10m,
                AmountPaid = 0m,
                BalanceDue = 10m,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var order = new PosOrder
            {
                RestaurantId = foreign.RestaurantId,
                BranchId = foreign.BranchId,
                OrderNumber = "ORD-20260613-9999",
                OrderType = PosOrderType.EatIn,
                Status = PosOrderStatus.Confirmed,
                Subtotal = 10m,
                TaxTotal = 0m,
                GrandTotal = 10m,
                ConfirmedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.MenuCategories.Add(category);
            context.MenuItems.Add(item);
            context.PosOrders.Add(order);
            line.PosOrderId = order.PosOrderId;
            bill.PosOrderId = order.PosOrderId;
            bill.BillLines.Add(new BillLine
            {
                RestaurantId = foreign.RestaurantId,
                PosOrderLineId = line.PosOrderLineId,
                MenuItemId = item.MenuItemId,
                MenuCategoryId = category.MenuCategoryId,
                MenuItemNameSnapshot = item.Name,
                MenuCategoryNameSnapshot = category.Name,
                SkuSnapshot = "FOREIGN-1",
                UnitPrice = 10m,
                TaxRate = 0m,
                Quantity = 1m,
                LineSubtotal = 10m,
                LineTax = 0m,
                LineTotal = 10m,
                DisplayOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });
            order.PosOrderLines.Add(line);
            context.Bills.Add(bill);
            await context.SaveChangesAsync();

            var response = await fixture.Client.GetAsync($"/api/v1/billing/bills/{bill.BillId}/receipt");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private sealed class BillingApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<BillingApiFactory> CreateAsync()
        {
            var factory = new BillingApiFactory();
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
                MobileNumber = mobileNumber,
                Status = UserStatus.Active
            };

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

        public async Task UpdateMenuItemPriceAsync(Guid menuItemId, decimal basePrice)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var item = await context.MenuItems.SingleAsync(entity => entity.MenuItemId == menuItemId);
            item.BasePrice = basePrice;
            item.MarkUpdated();
            await context.SaveChangesAsync();
        }

        public async Task<PosOrder> InsertOrderAsync(Guid restaurantId, Guid branchId, PosOrderStatus status, string orderNumber)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var order = new PosOrder
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderNumber = orderNumber,
                OrderType = PosOrderType.EatIn,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow
            };

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
            PosOrderType orderType = PosOrderType.EatIn,
            string? tableName = null,
            string? customerName = null,
            string? customerMobile = null)
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
                Status = PosOrderStatus.Confirmed,
                TableName = tableName,
                CustomerName = customerName,
                CustomerMobile = customerMobile,
                Subtotal = RoundMoney(unitPrice * quantity),
                TaxTotal = RoundMoney(RoundMoney(unitPrice * quantity) * taxRate / 100m),
                GrandTotal = RoundMoney(RoundMoney(unitPrice * quantity) + RoundMoney(RoundMoney(unitPrice * quantity) * taxRate / 100m)),
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
                LineSubtotal = RoundMoney(unitPrice * quantity),
                LineTax = RoundMoney(RoundMoney(unitPrice * quantity) * taxRate / 100m),
                LineTotal = RoundMoney(RoundMoney(unitPrice * quantity) + RoundMoney(RoundMoney(unitPrice * quantity) * taxRate / 100m)),
                CreatedAt = now
            });

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

    private sealed record BillListResponseDto(BillListItemDto[] Items);

    private sealed record BillListItemDto(
        Guid BillId,
        Guid BranchId,
        Guid PosOrderId,
        string BillNumber,
        DateTime BusinessDate,
        string Status,
        decimal GrandTotal,
        decimal AmountPaid,
        decimal BalanceDue,
        DateTimeOffset CreatedAt);

    private sealed record BillDetailDto(
        Guid BillId,
        Guid RestaurantId,
        Guid BranchId,
        Guid PosOrderId,
        string BillNumber,
        DateTime BusinessDate,
        string Status,
        decimal Subtotal,
        decimal TaxTotal,
        decimal GrandTotal,
        decimal AmountPaid,
        decimal BalanceDue,
        Guid? CreatedByUserId,
        Guid? CancelledByUserId,
        DateTimeOffset? CancelledAt,
        string? CancelReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        BillLineDto[] Lines,
        PaymentDetailDto[] Payments);

    private sealed record BillReceiptDto(
        Guid BillId,
        Guid RestaurantId,
        Guid BranchId,
        string RestaurantCode,
        string CountryCode,
        string CurrencyCode,
        string TimeZoneId,
        string RestaurantName,
        string BranchName,
        string? BranchAddress,
        Guid PosOrderId,
        DateTime BusinessDate,
        string? OrderNumberSnapshot,
        string? OrderTypeSnapshot,
        string? OrderTableNameSnapshot,
        string? OrderCustomerNameSnapshot,
        string? OrderCustomerMobileSnapshot,
        string BillNumber,
        string Status,
        Guid? CreatedByUserId,
        string CreatedByUserLabel,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset PrintedAt,
        Guid? CancelledByUserId,
        DateTimeOffset? CancelledAt,
        string? CancelReason,
        decimal Subtotal,
        decimal TaxTotal,
        decimal GrandTotal,
        decimal AmountPaid,
        decimal BalanceDue,
        int PrintCount,
        bool IsReprint,
        BillReceiptLineDto[] Lines,
        BillReceiptPaymentDto[] Payments);

    private sealed record BillReceiptLineDto(
        int DisplayOrder,
        string MenuItemNameSnapshot,
        string MenuCategoryNameSnapshot,
        string? SkuSnapshot,
        decimal Quantity,
        string? Notes,
        decimal UnitPrice,
        decimal LineSubtotal,
        decimal LineTax,
        decimal LineTotal);

    private sealed record BillReceiptPaymentDto(
        string PaymentNumber,
        string PaymentMode,
        string Status,
        decimal Amount,
        string? ReferenceNumber,
        string? Notes,
        Guid? RecordedByUserId,
        string RecordedByUserLabel,
        DateTimeOffset CreatedAt);

    private sealed record BillLineDto(
        Guid BillLineId,
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
        DateTimeOffset CreatedAt);

    private sealed record PaymentDetailDto(
        Guid PaymentId,
        Guid BillId,
        Guid BranchId,
        string PaymentNumber,
        string PaymentMode,
        string Status,
        decimal Amount,
        string? ReferenceNumber,
        string? Notes,
        Guid? RecordedByUserId,
        Guid? CancelledByUserId,
        DateTimeOffset? CancelledAt,
        string? CancelReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record CashierShiftDetailDto(
        Guid CashierShiftId,
        Guid RestaurantId,
        Guid BranchId,
        Guid CashierUserId,
        string CashierName,
        string BranchName,
        DateTime BusinessDate,
        string Status,
        DateTimeOffset OpenedAtUtc,
        decimal OpeningCashAmount,
        DateTimeOffset? ClosedAtUtc,
        decimal? DeclaredClosingCashAmount,
        decimal ExpectedClosingCashAmount,
        decimal? CashVarianceAmount,
        string? CloseNotes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Application.Reports;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Cashiering;
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

public sealed class DailyCashSalesReportEndpointTests
{
    [Fact]
    public async Task Report_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_403_When_User_Lacks_Report_View()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var waiter = await fixture.SeedSystemUserAsync("Waiter");
        await fixture.AuthenticateAsync(waiter);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_View_Constant_And_Seed_Mapping_Should_Include_Report_View()
    {
        Assert.Equal("Report.View", SystemPermissions.ReportView);
        Assert.Contains(SystemPermissions.Definitions, definition => definition.Code == SystemPermissions.ReportView);
        Assert.Contains(FoundationSeedData.RolePermissions, seed => seed.PermissionCode == SystemPermissions.ReportView);
    }

    [Fact]
    public async Task Report_Should_Return_Zero_Summary_When_No_Control_Data_Exists()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.Summary.TotalBills);
        Assert.Equal(0m, payload.Summary.GrossSales);
        Assert.Equal(0m, payload.Summary.GrossBillTotal);
        Assert.Equal(0m, payload.Summary.NetSales);
        Assert.Equal(0m, payload.Summary.TotalAmountPaid);
        Assert.Equal(0m, payload.Summary.TotalBalanceDue);
        Assert.Equal(0m, payload.Summary.CashPayments);
        Assert.Equal(0m, payload.Summary.UpiPayments);
        Assert.Equal(0m, payload.Summary.CardPayments);
        Assert.Equal(0m, payload.Summary.OtherPayments);
        Assert.Equal(0, payload.Summary.OpenShifts);
        Assert.Equal(0, payload.Summary.ClosedShifts);
        Assert.Equal(0m, payload.Summary.OpeningCashTotal);
        Assert.Equal(0m, payload.Summary.DeclaredClosingCashTotal);
        Assert.Equal(0m, payload.Summary.ExpectedCashTotal);
        Assert.Equal(0m, payload.Summary.CashVarianceTotal);
        Assert.Equal(4, payload.PaymentBreakdown.Count);
        Assert.All(payload.PaymentBreakdown, item =>
        {
            Assert.Equal(0m, item.RecordedAmount);
            Assert.Equal(0m, item.CancelledAmount);
            Assert.Equal(0m, item.NetAmount);
            Assert.Equal(0, item.PaymentCount);
            Assert.Equal(0, item.CancelledCount);
        });
        Assert.Empty(payload.CashShiftSummaries);
        Assert.Empty(payload.Exceptions.UnpaidBills);
        Assert.Empty(payload.Exceptions.CancelledBills);
        Assert.Empty(payload.Exceptions.CancelledPayments);
        Assert.Empty(payload.Exceptions.ReceiptReprints);
        Assert.Empty(payload.Exceptions.CashVariances);
        Assert.Empty(payload.Exceptions.OpenShifts);
    }

    [Fact]
    public async Task Report_Should_Be_Scoped_To_Current_Restaurant_And_Ignore_Foreign_Data()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var date = new DateTimeOffset(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0001", BillStatus.Paid, date.AddHours(2), 12m, 12m, 0m);
        await fixture.InsertBillAsync(admin.RestaurantId, admin.SecondBranchId, "BILL-20260613-0002", BillStatus.Unpaid, date.AddHours(3), 8m, 0m, 8m);

        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.InsertBillAsync(foreign.RestaurantId, foreign.BranchId, "BILL-20260613-0009", BillStatus.Paid, date.AddHours(4), 99m, 99m, 0m);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(2, payload.Summary.TotalBills);
        Assert.Equal(1, payload.Summary.PaidBills);
        Assert.Equal(1, payload.Summary.UnpaidBills);
        Assert.Equal(20m, payload.Summary.GrossSales);
        Assert.Equal(20m, payload.Summary.GrossBillTotal);
        Assert.Equal(20m, payload.Summary.NetSales);
        Assert.Equal(0m, payload.Summary.TotalAmountPaid);
        Assert.Equal(8m, payload.Summary.TotalBalanceDue);
        Assert.Equal(0, payload.Summary.OpenShifts);
        Assert.Equal(0, payload.Summary.ClosedShifts);
        Assert.All(payload.Exceptions.UnpaidBills, item => Assert.Contains(item.BranchId, new[] { admin.BranchId, admin.SecondBranchId }));
    }

    [Fact]
    public async Task Report_Should_Use_BusinessDate_Scoping_For_Bills_Payments_And_Shifts()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var billBusinessDate = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTimeOffset(new DateTime(2026, 6, 14, 8, 0, 0, DateTimeKind.Utc));

        var bill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260613-0001",
            BillStatus.PartiallyPaid,
            createdAt,
            25m,
            10m,
            15m);

        await fixture.UpdateBillBusinessDateAsync(bill.BillId, billBusinessDate);

        var shift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            createdAt,
            createdAt.AddHours(8),
            openingCashAmount: 50m,
            expectedCashAmount: 50m,
            countedCashAmount: 55m,
            varianceAmount: 5m,
            openedByUserId: admin.UserId);

        await fixture.UpdateCashierShiftBusinessDateAsync(shift.CashierShiftId, billBusinessDate);

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            createdAt.AddHours(1),
            10m,
            null,
            shift.CashierShiftId);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13&branchId=" + admin.BranchId);
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(payload);

        Assert.Equal("2026-06-13", payload!.BusinessDate);
        Assert.Equal(1, payload.Summary.TotalBills);
        Assert.Equal(1, payload.Summary.PartiallyPaidBills);
        Assert.Equal(25m, payload.Summary.GrossSales);
        Assert.Equal(25m, payload.Summary.GrossBillTotal);
        Assert.Equal(10m, payload.Summary.TotalAmountPaid);
        Assert.Equal(15m, payload.Summary.TotalBalanceDue);
        Assert.Equal(1, payload.Summary.ClosedShifts);
        Assert.Equal(50m, payload.Summary.OpeningCashTotal);
        Assert.Equal(55m, payload.Summary.DeclaredClosingCashTotal);
        Assert.Equal(60m, payload.Summary.ExpectedCashTotal);
        Assert.Equal(-5m, payload.Summary.CashVarianceTotal);
        Assert.Equal(10m, payload.Summary.CashPayments);
        Assert.Equal(0m, payload.Summary.UpiPayments);
        Assert.Equal(0m, payload.Summary.CardPayments);
        Assert.Equal(0m, payload.Summary.OtherPayments);
        Assert.Single(payload.PaymentBreakdown.Where(item => item.PaymentMode == "Cash"));
        Assert.Single(payload.CashShiftSummaries);
        Assert.Equal(50m, payload.CashShiftSummaries.Single().OpeningCashAmount);
        Assert.Equal(60m, payload.CashShiftSummaries.Single().ExpectedCashAmount);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var storedShift = await context.CashierShifts.SingleAsync(entity => entity.CashierShiftId == shift.CashierShiftId);
        Assert.Equal(50m, storedShift.ExpectedClosingCashAmount);
    }

    [Fact]
    public async Task BranchId_From_Other_Restaurant_Should_Return_404()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignRestaurantAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/daily-cash-sales?date=2026-06-13&branchId={foreign.BranchId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Date_Filter_Should_Include_Matching_Date_Exclude_Other_Date_And_Not_Mutate_Data()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0001", BillStatus.Paid, new DateTimeOffset(new DateTime(2026, 6, 13, 8, 0, 0, DateTimeKind.Utc)), 12m, 12m, 0m);
        await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260614-0001", BillStatus.Paid, new DateTimeOffset(new DateTime(2026, 6, 14, 8, 0, 0, DateTimeKind.Utc)), 14m, 14m, 0m);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var billCountBefore = await context.Bills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var paymentCountBefore = await context.Payments.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal("2026-06-13", payload!.BusinessDate);
        Assert.Equal(1, payload.Summary.TotalBills);
        Assert.Equal(12m, payload.Summary.GrossSales);
        Assert.Empty(payload.Exceptions.CancelledBills);

        Assert.Equal(billCountBefore, await context.Bills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(paymentCountBefore, await context.Payments.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
    }

    [Fact]
    public async Task Report_Should_Aggregate_Payments_Reprints_Variance_And_Open_Shifts()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var targetDate = new DateTimeOffset(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var paidBill = await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0001", BillStatus.Paid, targetDate.AddHours(1), 12m, 12m, 0m);
        var partialBill = await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0002", BillStatus.PartiallyPaid, targetDate.AddHours(2), 20m, 8m, 12m);
        var unpaidBill = await fixture.InsertBillAsync(admin.RestaurantId, admin.SecondBranchId, "BILL-20260613-0003", BillStatus.Unpaid, targetDate.AddHours(3), 15m, 0m, 15m);
        var cancelledBill = await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0004", BillStatus.Cancelled, targetDate.AddHours(4), 11m, 0m, 11m, "Customer cancelled");

        await fixture.InsertPaymentAsync(paidBill, admin.BranchId, PaymentMode.Cash, PaymentStatus.Recorded, targetDate.AddHours(1).AddMinutes(10), 12m, null, null);
        await fixture.InsertPaymentAsync(partialBill, admin.BranchId, PaymentMode.Card, PaymentStatus.Recorded, targetDate.AddHours(2).AddMinutes(10), 8m, null, null);
        await fixture.InsertPaymentAsync(unpaidBill, admin.SecondBranchId, PaymentMode.Upi, PaymentStatus.Cancelled, targetDate.AddHours(3).AddMinutes(10), 15m, "Duplicate payment", null);
        await fixture.InsertPaymentAsync(cancelledBill, admin.BranchId, PaymentMode.Other, PaymentStatus.Cancelled, targetDate.AddHours(4).AddMinutes(10), 11m, "Cancelled with bill", null);

        await fixture.InsertPrintEventAsync(paidBill, admin.BranchId, 1, targetDate.AddHours(1).AddMinutes(20));
        await fixture.InsertPrintEventAsync(paidBill, admin.BranchId, 2, targetDate.AddHours(1).AddMinutes(30));

        var closedShift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            targetDate.AddHours(2),
            targetDate.AddHours(10),
            openingCashAmount: 100m,
            expectedCashAmount: 115m,
            countedCashAmount: 120m,
            varianceAmount: 5m,
            openedByUserId: admin.UserId);

        await fixture.InsertPaymentAsync(partialBill, admin.BranchId, PaymentMode.Cash, PaymentStatus.Recorded, targetDate.AddHours(2).AddMinutes(20), 8m, null, closedShift.CashierShiftId);
        await fixture.InsertCashMovementAsync(closedShift, CashDrawerMovementType.CashIn, 10m, "Small change", admin.UserId);

        await fixture.InsertCashierShiftAsync(
            admin.SecondBranchId,
            CashierShiftStatus.Open,
            targetDate.AddHours(6),
            null,
            openingCashAmount: 50m,
            expectedCashAmount: 70m,
            countedCashAmount: null,
            varianceAmount: null,
            openedByUserId: admin.UserId);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(payload);

        Assert.Equal(4, payload!.Summary.TotalBills);
        Assert.Equal(1, payload.Summary.PaidBills);
        Assert.Equal(1, payload.Summary.PartiallyPaidBills);
        Assert.Equal(1, payload.Summary.UnpaidBills);
        Assert.Equal(1, payload.Summary.CancelledBills);
        Assert.Equal(58m, payload.Summary.GrossSales);
        Assert.Equal(47m, payload.Summary.GrossBillTotal);
        Assert.Equal(11m, payload.Summary.CancelledBillAmount);
        Assert.Equal(47m, payload.Summary.NetSales);
        Assert.Equal(28m, payload.Summary.TotalAmountPaid);
        Assert.Equal(27m, payload.Summary.TotalBalanceDue);
        Assert.Equal(20m, payload.Summary.CashPayments);
        Assert.Equal(8m, payload.Summary.NonCashPayments);
        Assert.Equal(0m, payload.Summary.UpiPayments);
        Assert.Equal(8m, payload.Summary.CardPayments);
        Assert.Equal(0m, payload.Summary.OtherPayments);
        Assert.Equal(1, payload.Summary.OpenShifts);
        Assert.Equal(1, payload.Summary.ClosedShifts);
        Assert.Equal(150m, payload.Summary.OpeningCashTotal);
        Assert.Equal(120m, payload.Summary.DeclaredClosingCashTotal);
        Assert.Equal(158m, payload.Summary.ExpectedCashTotal);
        Assert.Equal(2, payload.Summary.ReceiptPrints);
        Assert.Equal(1, payload.Summary.ReceiptReprints);
        Assert.Equal(12m, payload.Summary.CashVarianceTotal);

        Assert.Equal(4, payload.PaymentBreakdown.Count);
        Assert.Equal(2, payload.PaymentBreakdown.Single(item => item.PaymentMode == "Cash").PaymentCount);
        Assert.Equal(1, payload.PaymentBreakdown.Single(item => item.PaymentMode == "Upi").CancelledCount);
        Assert.Single(payload.Exceptions.CancelledBills);
        Assert.Equal(2, payload.Exceptions.CancelledPayments.Count);
        Assert.Single(payload.Exceptions.ReceiptReprints);
        Assert.Single(payload.Exceptions.CashVariances);
        Assert.Single(payload.Exceptions.OpenShifts);
        var receiptReprint = payload.Exceptions.ReceiptReprints.Single();
        var cashVariance = payload.Exceptions.CashVariances.Single();
        var openShift = payload.Exceptions.OpenShifts.Single();

        Assert.Equal("BILL-20260613-0001", receiptReprint.ReferenceNumber);
        Assert.Equal(2, receiptReprint.PrintCount);
        Assert.Equal(1, receiptReprint.ReprintCount);
        Assert.Equal(12m, cashVariance.VarianceAmount);
        Assert.Equal("Counted closing cash minus expected cash (opening cash + recorded cash payments).", cashVariance.Reason);
        Assert.Equal("Open", openShift.Status);
    }

    [Fact]
    public async Task Report_Should_Not_Expose_Export_Endpoint()
    {
        await using var fixture = await DailyCashSalesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales/export");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException($"Expected success, got {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    private sealed class DailyCashSalesReportApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<DailyCashSalesReportApiFactory> CreateAsync()
        {
            var factory = new DailyCashSalesReportApiFactory();
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
                Name = "Main Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var secondBranch = new Branch
            {
                Name = "North Branch",
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
                MobileNumber = "90000044",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.AddRange(branch, secondBranch);
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
                secondBranch.BranchId,
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

        public async Task<Bill> InsertBillAsync(
            Guid restaurantId,
            Guid branchId,
            string billNumber,
            BillStatus status,
            DateTimeOffset createdAt,
            decimal grandTotal,
            decimal amountPaid,
            decimal balanceDue,
            string? cancelReason = null,
            DateTime? businessDate = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var order = new PosOrder
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderNumber = $"ORD-{createdAt.UtcDateTime:yyyyMMdd}-{billNumber[^4..]}",
                OrderType = PosOrderType.EatIn,
                Status = PosOrderStatus.Confirmed,
                Subtotal = grandTotal,
                TaxTotal = 0m,
                GrandTotal = grandTotal,
                ConfirmedAt = createdAt,
                CreatedAt = createdAt
            };

            var bill = new Bill
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                PosOrderId = order.PosOrderId,
                BillNumber = billNumber,
                Status = status,
                Subtotal = grandTotal,
                TaxTotal = 0m,
                GrandTotal = grandTotal,
                AmountPaid = amountPaid,
                BalanceDue = balanceDue,
                CancelReason = cancelReason,
                BusinessDate = (businessDate ?? createdAt.UtcDateTime.Date).Date,
                CreatedAt = createdAt
            };

            context.PosOrders.Add(order);
            context.Bills.Add(bill);
            await context.SaveChangesAsync();

            return bill;
        }

        public async Task UpdateBillBusinessDateAsync(Guid billId, DateTime businessDate)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var bill = await context.Bills.SingleAsync(entity => entity.BillId == billId);
            bill.BusinessDate = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Utc);
            await context.SaveChangesAsync();
        }

        public async Task<Payment> InsertPaymentAsync(
            Bill bill,
            Guid branchId,
            PaymentMode paymentMode,
            PaymentStatus status,
            DateTimeOffset createdAt,
            decimal amount,
            string? cancelReason,
            Guid? cashierShiftId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var payment = new Payment
            {
                RestaurantId = bill.RestaurantId,
                BranchId = branchId,
                BillId = bill.BillId,
                CashierShiftId = cashierShiftId,
                PaymentNumber = $"PAY-{createdAt.UtcDateTime:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4]}",
                PaymentMode = paymentMode,
                Status = status,
                Amount = amount,
                CancelReason = cancelReason,
                CreatedAt = createdAt
            };

            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            return payment;
        }

        public async Task<BillPrintEvent> InsertPrintEventAsync(Bill bill, Guid branchId, int printSequence, DateTimeOffset createdAt)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var printEvent = new BillPrintEvent
            {
                RestaurantId = bill.RestaurantId,
                BranchId = branchId,
                BillId = bill.BillId,
                PrintSequence = printSequence,
                CreatedAt = createdAt
            };

            context.BillPrintEvents.Add(printEvent);
            await context.SaveChangesAsync();
            return printEvent;
        }

        public async Task<CashierShift> InsertCashierShiftAsync(
            Guid branchId,
            CashierShiftStatus status,
            DateTimeOffset openedAt,
            DateTimeOffset? closedAt,
            decimal openingCashAmount,
            decimal expectedCashAmount,
            decimal? countedCashAmount,
            decimal? varianceAmount,
            Guid openedByUserId,
            DateTime? businessDate = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var shift = new CashierShift
            {
                RestaurantId = (await context.Branches.SingleAsync(branch => branch.BranchId == branchId)).RestaurantId,
                BranchId = branchId,
                OpenedByUserId = openedByUserId,
                Status = status,
                OpeningCashAmount = openingCashAmount,
                ExpectedCashAmount = expectedCashAmount,
                CountedCashAmount = countedCashAmount,
                CashVarianceAmount = varianceAmount,
                BusinessDate = (businessDate ?? openedAt.UtcDateTime.Date).Date,
                OpenedAt = openedAt,
                ClosedAt = closedAt
            };

            context.CashierShifts.Add(shift);
            await context.SaveChangesAsync();
            return shift;
        }

        public async Task UpdateCashierShiftBusinessDateAsync(Guid shiftId, DateTime businessDate)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var shift = await context.CashierShifts.SingleAsync(entity => entity.CashierShiftId == shiftId);
            shift.BusinessDate = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Utc);
            await context.SaveChangesAsync();
        }

        public async Task<CashDrawerMovement> InsertCashMovementAsync(
            CashierShift shift,
            CashDrawerMovementType movementType,
            decimal amount,
            string reason,
            Guid createdByUserId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var movement = new CashDrawerMovement
            {
                RestaurantId = shift.RestaurantId,
                BranchId = shift.BranchId,
                CashierShiftId = shift.CashierShiftId,
                MovementType = movementType,
                Amount = amount,
                Reason = reason,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.CashDrawerMovements.Add(movement);
            await context.SaveChangesAsync();
            return movement;
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
        Guid SecondBranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName);

    private sealed record AuthLoginResponseDto(string AccessToken, string RefreshToken);
}

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

public sealed class CashReconciliationReportEndpointTests
{
    [Fact]
    public async Task Report_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/reports/cash-reconciliation?businessDate=2026-06-13");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_403_When_User_Lacks_Report_View()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var waiter = await fixture.SeedSystemUserAsync("Waiter");
        await fixture.AuthenticateAsync(waiter);

        var response = await fixture.Client.GetAsync("/api/v1/reports/cash-reconciliation?businessDate=2026-06-13");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_Zero_Summary_When_No_Shifts_Exist()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(0, payload.Totals.ShiftCount);
        Assert.Equal(0, payload.Totals.OpenShiftCount);
        Assert.Equal(0, payload.Totals.ClosedShiftCount);
        Assert.Equal(0m, payload.Totals.OpeningCashTotal);
        Assert.Equal(0m, payload.Totals.CashPaymentTotal);
        Assert.Equal(0m, payload.Totals.CashInTotal);
        Assert.Equal(0m, payload.Totals.CashOutTotal);
        Assert.Equal(0m, payload.Totals.AdjustmentTotal);
        Assert.Equal(0m, payload.Totals.ExpectedCashTotal);
        Assert.Equal(0m, payload.Totals.DeclaredCashTotal);
        Assert.Equal(0m, payload.Totals.VarianceTotal);
        Assert.Equal(0, payload.Totals.MajorVarianceCount);
        Assert.Equal(0, payload.Totals.MinorVarianceCount);
        Assert.Equal(0, payload.Totals.BalancedShiftCount);
        Assert.Empty(payload.Shifts);
    }

    [Fact]
    public async Task Report_Should_Calculate_Expected_Cash_From_Payments_And_Drawer_Movements()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var bill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260613-0001",
            BillStatus.PartiallyPaid,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 0, 0, DateTimeKind.Utc)),
            25m,
            0m,
            25m,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var shift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 100m,
            countedCashAmount: 130m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 30, 0, DateTimeKind.Utc)),
            25m,
            null,
            shift.CashierShiftId);

        await fixture.InsertCashMovementAsync(shift, CashDrawerMovementType.CashIn, 10m, "Cash top-up", admin.UserId);
        await fixture.InsertCashMovementAsync(shift, CashDrawerMovementType.CashOut, 5m, "Safe drop", admin.UserId);
        await fixture.InsertCashMovementAsync(shift, CashDrawerMovementType.Adjustment, -2m, "Drawer correction", admin.UserId);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Shifts);

        var row = payload.Shifts.Single();
        Assert.Equal(25m, row.CashPaymentTotal);
        Assert.Equal(10m, row.CashInTotal);
        Assert.Equal(5m, row.CashOutTotal);
        Assert.Equal(-2m, row.AdjustmentTotal);
        Assert.Equal(128m, row.ExpectedCashAmount);
        Assert.Equal(130m, row.DeclaredClosingCashAmount);
        Assert.Equal(2m, row.VarianceAmount);
        Assert.Equal("MinorVariance", row.VarianceStatus);
        Assert.Equal(1, row.PaymentCount);
        Assert.Equal(3, row.MovementCount);
        Assert.Equal(128m, payload.Totals.ExpectedCashTotal);
        Assert.Equal(130m, payload.Totals.DeclaredCashTotal);
        Assert.Equal(2m, payload.Totals.VarianceTotal);
        Assert.Equal(1, payload.Totals.MinorVarianceCount);
        Assert.Equal(0, payload.Totals.BalancedShiftCount);
    }

    [Fact]
    public async Task Report_Should_Exclude_Cancelled_And_NonCash_Payments_From_Cash_Total()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var bill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260613-0002",
            BillStatus.PartiallyPaid,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 0, 0, DateTimeKind.Utc)),
            47m,
            0m,
            47m,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var shift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 50m,
            countedCashAmount: 70m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 15, 0, DateTimeKind.Utc)),
            20m,
            null,
            shift.CashierShiftId);

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Cancelled,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 20, 0, DateTimeKind.Utc)),
            7m,
            "Cancelled duplicate",
            shift.CashierShiftId);

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Upi,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 25, 0, DateTimeKind.Utc)),
            9m,
            null,
            shift.CashierShiftId);

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Card,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 30, 0, DateTimeKind.Utc)),
            11m,
            null,
            shift.CashierShiftId);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        var row = Assert.Single(payload!.Shifts);
        Assert.Equal(20m, row.CashPaymentTotal);
        Assert.Equal(20m, payload.Totals.CashPaymentTotal);
        Assert.Equal(1, row.PaymentCount);
    }

    [Fact]
    public async Task Report_Should_Include_Cash_Payment_Linked_To_Report_Shift_When_Bill_BusinessDate_Is_Different()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var bill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260612-0003",
            BillStatus.PartiallyPaid,
            new DateTimeOffset(new DateTime(2026, 6, 12, 4, 0, 0, DateTimeKind.Utc)),
            30m,
            0m,
            30m,
            businessDate: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var shift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 75m,
            countedCashAmount: 105m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertPaymentAsync(
            bill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 6, 0, 0, DateTimeKind.Utc)),
            30m,
            null,
            shift.CashierShiftId);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);

        var row = Assert.Single(payload!.Shifts);
        Assert.Equal(30m, row.CashPaymentTotal);
        Assert.Equal(105m, row.ExpectedCashAmount);
        Assert.Equal(30m, payload.Totals.CashPaymentTotal);
        Assert.Equal(105m, payload.Totals.ExpectedCashTotal);
    }

    [Fact]
    public async Task Report_Should_Exclude_Payments_Outside_Selected_Shift_Or_Without_Shift()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var reportBill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260613-0004",
            BillStatus.PartiallyPaid,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 0, 0, DateTimeKind.Utc)),
            30m,
            0m,
            30m,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var priorBill = await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            "BILL-20260612-0004",
            BillStatus.PartiallyPaid,
            new DateTimeOffset(new DateTime(2026, 6, 12, 4, 0, 0, DateTimeKind.Utc)),
            40m,
            0m,
            40m,
            businessDate: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var reportShift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 75m,
            countedCashAmount: 105m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var priorShift = await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 12, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 50m,
            countedCashAmount: 90m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertPaymentAsync(
            reportBill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 6, 0, 0, DateTimeKind.Utc)),
            30m,
            null,
            reportShift.CashierShiftId);

        await fixture.InsertPaymentAsync(
            priorBill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 12, 6, 0, 0, DateTimeKind.Utc)),
            40m,
            null,
            priorShift.CashierShiftId);

        await fixture.InsertPaymentAsync(
            reportBill,
            admin.BranchId,
            PaymentMode.Cash,
            PaymentStatus.Recorded,
            new DateTimeOffset(new DateTime(2026, 6, 13, 7, 0, 0, DateTimeKind.Utc)),
            50m,
            null,
            null);

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);

        var row = Assert.Single(payload!.Shifts);
        Assert.Equal(30m, row.CashPaymentTotal);
        Assert.Equal(105m, row.ExpectedCashAmount);
        Assert.Equal(1, row.PaymentCount);
    }

    [Fact]
    public async Task Report_Should_Show_Open_Shift_With_OpenShift_Status_And_Null_Declared_And_Variance()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Open,
            new DateTimeOffset(new DateTime(2026, 6, 13, 8, 0, 0, DateTimeKind.Utc)),
            null,
            openingCashAmount: 40m,
            countedCashAmount: null,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        var row = Assert.Single(payload!.Shifts);
        Assert.Equal("Open", row.Status);
        Assert.Equal("OpenShift", row.VarianceStatus);
        Assert.Null(row.DeclaredClosingCashAmount);
        Assert.Null(row.VarianceAmount);
        Assert.Equal(1, payload.Totals.OpenShiftCount);
        Assert.Equal(0, payload.Totals.ClosedShiftCount);
    }

    [Fact]
    public async Task Report_Should_Label_Closed_Shifts_By_Variance_Threshold()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 100m,
            countedCashAmount: 100m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 3, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 11, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 100m,
            countedCashAmount: 200m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 100m,
            countedCashAmount: 201m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Totals.BalancedShiftCount);
        Assert.Equal(1, payload.Totals.MinorVarianceCount);
        Assert.Equal(1, payload.Totals.MajorVarianceCount);
        Assert.Contains(payload.Shifts, row => row.VarianceStatus == "Balanced");
        Assert.Contains(payload.Shifts, row => row.VarianceStatus == "MinorVariance");
        Assert.Contains(payload.Shifts, row => row.VarianceStatus == "MajorVariance");
    }

    [Fact]
    public async Task Report_Should_Not_Leak_Foreign_Branch_Shifts_And_Resolve_Missing_Cashier_Names_Safely()
    {
        await using var fixture = await CashReconciliationReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 2, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 60m,
            countedCashAmount: 60m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var foreign = await fixture.SeedSystemUserAsync("Cashier");

        await fixture.InsertCashierShiftAsync(
            admin.SecondBranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 4, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 90m,
            countedCashAmount: 90m,
            openedByUserId: admin.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertCashierShiftAsync(
            admin.BranchId,
            CashierShiftStatus.Closed,
            new DateTimeOffset(new DateTime(2026, 6, 13, 3, 0, 0, DateTimeKind.Utc)),
            new DateTimeOffset(new DateTime(2026, 6, 13, 11, 0, 0, DateTimeKind.Utc)),
            openingCashAmount: 70m,
            countedCashAmount: 70m,
            openedByUserId: foreign.UserId,
            businessDate: new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        var response = await fixture.Client.GetAsync($"/api/v1/reports/cash-reconciliation?businessDate=2026-06-13&branchId={admin.BranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CashReconciliationReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Shifts.Count);
        Assert.Equal(admin.BranchId, payload.BranchId);
        Assert.DoesNotContain(payload.Shifts, row => row.BranchId == admin.SecondBranchId);
        Assert.Contains(payload.Shifts, row => row.CashierUserId == foreign.UserId && row.CashierName == foreign.UserId.ToString());
    }

    private sealed class CashReconciliationReportApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<CashReconciliationReportApiFactory> CreateAsync()
        {
            var factory = new CashReconciliationReportApiFactory();
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

        public async Task<CashierShift> InsertCashierShiftAsync(
            Guid branchId,
            CashierShiftStatus status,
            DateTimeOffset openedAt,
            DateTimeOffset? closedAt,
            decimal openingCashAmount,
            decimal? countedCashAmount,
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
                ExpectedCashAmount = openingCashAmount,
                CountedCashAmount = countedCashAmount,
                CashVarianceAmount = countedCashAmount.HasValue ? countedCashAmount.Value - openingCashAmount : null,
                BusinessDate = (businessDate ?? openedAt.UtcDateTime.Date).Date,
                OpenedAt = openedAt,
                ClosedAt = closedAt
            };

            context.CashierShifts.Add(shift);
            await context.SaveChangesAsync();
            return shift;
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

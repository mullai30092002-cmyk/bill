using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Application.Dashboard;
using BillSoft.Application.Reports;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
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

public sealed class OwnerDashboardEndpointTests
{
    [Fact]
    public async Task Dashboard_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Should_Return_403_When_User_Lacks_Report_View()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var waiter = await fixture.SeedSystemUserAsync("Waiter");
        await fixture.AuthenticateAsync(waiter);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_View_Should_Load_For_Report_View_Users_And_Default_To_Today()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(admin.RestaurantCode, payload.RestaurantCode);
        Assert.Equal("SGD", payload.CurrencyCode);
        Assert.Equal(0, payload.Metrics.UnpaidBills);
        Assert.Equal(0, payload.Metrics.OpenShifts);
        Assert.NotNull(payload.InventoryAlerts);
        Assert.Equal(0, payload.InventoryAlerts.TotalAlertCount);
        Assert.Empty(payload.InventoryAlerts.CriticalItems);
        Assert.NotNull(payload.VendorDues);
        Assert.Equal(0m, payload.VendorDues.TotalVendorOutstanding);
        Assert.Equal(0, payload.VendorDues.OverdueVendorCount);
        Assert.Equal(0, payload.VendorDues.VendorsWithOutstandingCount);
        Assert.Empty(payload.VendorDues.CriticalVendors);
        Assert.Equal(ResolveExpectedBusinessDate().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), payload.BusinessDate);
    }

    [Fact]
    public async Task Dashboard_Should_Return_Inventory_Alert_Summary_And_Critical_Items()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        await fixture.SeedControlDataAsync(admin);

        await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Butter", "Dairy", "kg", 5m, true);
        await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Flour", "Bakery", "kg", 5m, true);

        var chili = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Chili", "Spices", "kg", 5m, true, updatedAtUtc: new DateTimeOffset(new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc)));
        var eggs = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Eggs", "Dairy", "pcs", 5m, true);
        var milk = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Milk", "Dairy", "l", 5m, true);
        var salt = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Salt", "Spices", "kg", 5m, true);
        var sugar = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Sugar", "Baking", "kg", 5m, true);

        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, chili.InventoryItemId, InventoryMovementType.StockIn, 1m, admin.UserId, "Manual purchase entry");
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, eggs.InventoryItemId, InventoryMovementType.StockIn, 2m, admin.UserId, "Manual purchase entry");
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, milk.InventoryItemId, InventoryMovementType.StockIn, 3m, admin.UserId, "Manual purchase entry");
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, salt.InventoryItemId, InventoryMovementType.StockIn, 4m, admin.UserId, "Manual purchase entry");
        await fixture.InsertInventoryMovementAsync(admin.RestaurantId, admin.BranchId, sugar.InventoryItemId, InventoryMovementType.StockIn, 8m, admin.UserId, "Manual purchase entry");

        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.InsertInventoryItemAsync(foreign.RestaurantId, foreign.BranchId, "Foreign Rice", "Grains", "kg", 5m, true);

        var response = await fixture.Client.GetAsync($"/api/v1/dashboard/owner?date=2026-06-13&branchId={admin.BranchId}");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);

        Assert.Equal(4, payload!.InventoryAlerts.LowStockCount);
        Assert.Equal(2, payload.InventoryAlerts.OutOfStockCount);
        Assert.Equal(6, payload.InventoryAlerts.TotalAlertCount);
        Assert.Equal(5, payload.InventoryAlerts.CriticalItems.Count);
        var criticalItems = payload.InventoryAlerts.CriticalItems.ToArray();
        Assert.Equal("Out of stock", criticalItems[0].Status);
        Assert.Equal("Out of stock", criticalItems[1].Status);
        Assert.Equal("Low stock", criticalItems[2].Status);
        Assert.Equal("Low stock", criticalItems[3].Status);
        Assert.Equal("Low stock", criticalItems[4].Status);
        Assert.Equal("Butter", criticalItems[0].Name);
        Assert.Equal("Flour", criticalItems[1].Name);
        Assert.Equal("Chili", criticalItems[2].Name);
        Assert.Equal("Eggs", criticalItems[3].Name);
        Assert.Equal("Milk", criticalItems[4].Name);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc)), criticalItems[2].LastUpdatedAt);
        Assert.DoesNotContain(criticalItems, item => item.Name == "Foreign Rice");
    }

    [Fact]
    public async Task Dashboard_Should_Be_Scoped_To_Current_Restaurant_And_Ignore_Foreign_Data()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var date = new DateTimeOffset(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        await fixture.InsertBillAsync(admin.RestaurantId, admin.BranchId, "BILL-20260613-0001", BillStatus.Paid, date.AddHours(2), 12m, 12m, 0m);
        await fixture.InsertBillAsync(admin.RestaurantId, admin.SecondBranchId, "BILL-20260613-0002", BillStatus.Unpaid, date.AddHours(3), 8m, 0m, 8m);

        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.InsertBillAsync(foreign.RestaurantId, foreign.BranchId, "BILL-20260613-0009", BillStatus.Paid, date.AddHours(4), 99m, 99m, 0m);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(1, payload.Metrics.UnpaidBills);
        Assert.Equal(20m, payload.Metrics.GrossSales);
        Assert.Equal(20m, payload.Metrics.NetSales);
    }

    [Fact]
    public async Task Dashboard_Should_Return_Vendor_Dues_Summary_And_Ignore_Foreign_Vendor_Bills()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var vendorA = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = "Fresh Rice",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010001",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });
        vendorA.EnsureSuccessStatusCode();
        var vendorAId = await vendorA.Content.ReadFromJsonAsync<VendorDetailDto>();
        Assert.NotNull(vendorAId);

        var vendorB = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = "Cool Water",
            vendorType = "Water",
            contactName = "Mohan",
            mobileNumber = "90010002",
            address = "Depot Road",
            notes = "Weekly vendor",
            isActive = true
        });
        vendorB.EnsureSuccessStatusCode();
        var vendorBId = await vendorB.Content.ReadFromJsonAsync<VendorDetailDto>();
        Assert.NotNull(vendorBId);

        var billA = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendorAId!.VendorId,
            branchId = admin.BranchId,
            billNumber = "VB-DUES-001",
            billDate = new DateTime(2026, 6, 12),
            dueDate = new DateTime(2026, 6, 15),
            notes = "Vendor A bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        billA.EnsureSuccessStatusCode();

        var billB = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendorBId!.VendorId,
            branchId = admin.BranchId,
            billNumber = "VB-DUES-002",
            billDate = new DateTime(2026, 6, 13),
            dueDate = new DateTime(2026, 6, 16),
            notes = "Vendor B bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Water",
                    quantity = 1m,
                    unitCost = 80m
                }
            }
        });
        billB.EnsureSuccessStatusCode();
        var billBPayload = await billB.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(billBPayload);

        var settlement = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{billBPayload!.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 20m,
            referenceNumber = (string?)null,
            notes = "Partial settlement",
            paidAtUtc = new DateTimeOffset(new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc))
        });
        settlement.EnsureSuccessStatusCode();

        var foreign = await fixture.SeedForeignRestaurantAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var foreignVendor = new Vendor
            {
                RestaurantId = foreign.RestaurantId,
                BranchId = foreign.BranchId,
                Name = "Foreign Vendor",
                NormalizedName = Vendor.NormalizeKey("Foreign Vendor"),
                VendorType = VendorType.Groceries,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var foreignBill = new VendorBill
            {
                RestaurantId = foreign.RestaurantId,
                BranchId = foreign.BranchId,
                VendorId = foreignVendor.VendorId,
                BillNumber = "VB-FOREIGN-001",
                BillDate = new DateTime(2026, 6, 12),
                DueDate = new DateTime(2026, 6, 15),
                Status = VendorBillStatus.Unpaid,
                TotalAmount = 999m,
                PaidAmount = 0m,
                BalanceAmount = 999m,
                Notes = "Foreign bill",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            context.Vendors.Add(foreignVendor);
            context.VendorBills.Add(foreignBill);
            await context.SaveChangesAsync();
        }

        var response = await fixture.Client.GetAsync($"/api/v1/dashboard/owner?date=2026-06-30");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.VendorDues);
        Assert.Equal(160m, payload.VendorDues.TotalVendorOutstanding);
        Assert.Equal(2, payload.VendorDues.VendorsWithOutstandingCount);
        Assert.Equal(2, payload.VendorDues.OverdueVendorCount);
        Assert.Single(payload.VendorDues.CriticalVendors, vendor => vendor.VendorName == "Fresh Rice");
        Assert.Equal("Fresh Rice", payload.VendorDues.CriticalVendors.First().VendorName);
        Assert.Equal(100m, payload.VendorDues.CriticalVendors.First().OutstandingAmount);
        Assert.DoesNotContain(payload.VendorDues.CriticalVendors, item => item.VendorName == "Foreign Vendor");
    }

    [Fact]
    public async Task Foreign_Branch_Should_Return_404()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignRestaurantAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/dashboard/owner?date=2026-06-13&branchId={foreign.BranchId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Metrics_Should_Mirror_Daily_Report_Summary_For_Same_Date()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        await fixture.SeedControlDataAsync(admin);

        var reportResponse = await fixture.Client.GetAsync("/api/v1/reports/daily-cash-sales?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(reportResponse);
        var report = await reportResponse.Content.ReadFromJsonAsync<DailyCashSalesReportResponse>();
        Assert.NotNull(report);

        var dashboardResponse = await fixture.Client.GetAsync("/api/v1/dashboard/owner?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(dashboardResponse);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(dashboard);

        Assert.Equal(report!.Summary.GrossSales, dashboard!.Metrics.GrossSales);
        Assert.Equal(report.Summary.NetSales, dashboard.Metrics.NetSales);
        Assert.Equal(report.Summary.CashPayments, dashboard.Metrics.CashPayments);
        Assert.Equal(report.Summary.NonCashPayments, dashboard.Metrics.NonCashPayments);
        Assert.Equal(report.Summary.TotalAmountPaid, dashboard.Metrics.TotalAmountPaid);
        Assert.Equal(report.Summary.TotalBalanceDue, dashboard.Metrics.TotalBalanceDue);
        Assert.Equal(report.Summary.UnpaidBills, dashboard.Metrics.UnpaidBills);
        Assert.Equal(report.Summary.CancelledBills, dashboard.Metrics.CancelledBills);
        Assert.Equal(report.Exceptions.CancelledPayments.Count, dashboard.Metrics.CancelledPayments);
        Assert.Equal(report.Summary.ReceiptReprints, dashboard.Metrics.ReceiptReprints);
        Assert.Equal(report.Summary.CashVarianceTotal, dashboard.Metrics.CashVarianceTotal);
        Assert.Equal(report.Exceptions.OpenShifts.Count, dashboard.Metrics.OpenShifts);
    }

    [Fact]
    public async Task Dashboard_Should_Return_Unpaid_Cancelled_Reprint_Variance_And_OpenShift_Alerts()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        await fixture.SeedControlDataAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);

        Assert.Contains(payload!.Alerts, alert => alert.Type == "UnpaidBills" && alert.Severity == "High");
        Assert.Contains(payload.Alerts, alert => alert.Type == "CancelledActivity" && alert.Severity == "Medium");
        Assert.Contains(payload.Alerts, alert => alert.Type == "ReceiptReprints" && alert.Severity == "Low");
        var cashVariance = Assert.Single(payload.Alerts.Where(alert => alert.Type == "CashVariance"));
        Assert.Equal("High", cashVariance.Severity);
        Assert.Equal("Closed shift variance needs review", cashVariance.Title);
        Assert.Contains("Closed shift variance total is", cashVariance.Message);
        Assert.Contains("Counted closing cash minus expected cash (opening cash + recorded cash payments).", cashVariance.Message);
        Assert.Contains(payload.Alerts, alert => alert.Type == "OpenShift" && alert.Severity == "Medium");
    }

    [Fact]
    public async Task Dashboard_Should_Return_Quick_Links_And_Not_Mutate_Data()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        await fixture.SeedControlDataAsync(admin);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var billCountBefore = await context.Bills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var paymentCountBefore = await context.Payments.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var printCountBefore = await context.BillPrintEvents.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var shiftCountBefore = await context.CashierShifts.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner?date=2026-06-13");
        await EnsureSuccessStatusCodeAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<OwnerDashboardResponse>();
        Assert.NotNull(payload);

        Assert.Equal(5, payload!.QuickLinks.Count);
        Assert.Contains(payload.QuickLinks, item => item.Label == "Daily Report" && item.Path == "/reports/daily-cash-sales");
        Assert.Contains(payload.QuickLinks, item => item.Label == "Billing" && item.Path == "/billing");
        Assert.Contains(payload.QuickLinks, item => item.Label == "Cashier Shifts" && item.Path == "/cashier/shifts");
        Assert.Contains(payload.QuickLinks, item => item.Label == "Kitchen Tickets" && item.Path == "/kitchen/tickets");
        Assert.Contains(payload.QuickLinks, item => item.Label == "POS Orders" && item.Path == "/pos/orders");

        Assert.Equal(billCountBefore, await context.Bills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(paymentCountBefore, await context.Payments.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(printCountBefore, await context.BillPrintEvents.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(shiftCountBefore, await context.CashierShifts.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
    }

    [Fact]
    public async Task Dashboard_Should_Not_Expose_Export_Endpoint()
    {
        await using var fixture = await OwnerDashboardApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/dashboard/owner/export");

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

    private static DateTime ResolveExpectedBusinessDate()
    {
        var timeZone = ResolveTimeZone("Asia/Singapore");
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).Date;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveFallbackTimeZone(timeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveFallbackTimeZone(timeZoneId);
        }
    }

    private static TimeZoneInfo ResolveFallbackTimeZone(string timeZoneId)
    {
        var mappedId = timeZoneId.Trim().ToUpperInvariant() switch
        {
            "ASIA/SINGAPORE" => "Singapore Standard Time",
            "ASIA/KOLKATA" => "India Standard Time",
            _ => "UTC"
        };

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(mappedId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private sealed class OwnerDashboardApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<OwnerDashboardApiFactory> CreateAsync()
        {
            var factory = new OwnerDashboardApiFactory();
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

        public async Task<DailyCashSalesSeed> SeedControlDataAsync(SeedResult seed)
        {
            var targetDate = new DateTimeOffset(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

            var paidBill = await InsertBillAsync(seed.RestaurantId, seed.BranchId, "BILL-20260613-0001", BillStatus.Paid, targetDate.AddHours(1), 12m, 12m, 0m);
            var partialBill = await InsertBillAsync(seed.RestaurantId, seed.BranchId, "BILL-20260613-0002", BillStatus.PartiallyPaid, targetDate.AddHours(2), 20m, 8m, 12m);
            var unpaidBill = await InsertBillAsync(seed.RestaurantId, seed.SecondBranchId, "BILL-20260613-0003", BillStatus.Unpaid, targetDate.AddHours(3), 15m, 0m, 15m);
            var cancelledBill = await InsertBillAsync(seed.RestaurantId, seed.BranchId, "BILL-20260613-0004", BillStatus.Cancelled, targetDate.AddHours(4), 11m, 0m, 11m, "Customer cancelled");

            await InsertPaymentAsync(paidBill, seed.BranchId, PaymentMode.Cash, PaymentStatus.Recorded, targetDate.AddHours(1).AddMinutes(10), 12m, null, null);
            await InsertPaymentAsync(partialBill, seed.BranchId, PaymentMode.Card, PaymentStatus.Recorded, targetDate.AddHours(2).AddMinutes(10), 8m, null, null);
            await InsertPaymentAsync(unpaidBill, seed.SecondBranchId, PaymentMode.Upi, PaymentStatus.Cancelled, targetDate.AddHours(3).AddMinutes(10), 15m, "Duplicate payment", null);
            await InsertPaymentAsync(cancelledBill, seed.BranchId, PaymentMode.Other, PaymentStatus.Cancelled, targetDate.AddHours(4).AddMinutes(10), 11m, "Cancelled with bill", null);

            await InsertPrintEventAsync(paidBill, seed.BranchId, 1, targetDate.AddHours(1).AddMinutes(20));
            await InsertPrintEventAsync(paidBill, seed.BranchId, 2, targetDate.AddHours(1).AddMinutes(30));

            var closedShift = await InsertCashierShiftAsync(
                seed.BranchId,
                CashierShiftStatus.Closed,
                targetDate.AddHours(2),
                targetDate.AddHours(10),
                openingCashAmount: 100m,
                expectedCashAmount: 115m,
                countedCashAmount: 120m,
                varianceAmount: 5m,
                openedByUserId: seed.UserId);

            await InsertPaymentAsync(partialBill, seed.BranchId, PaymentMode.Cash, PaymentStatus.Recorded, targetDate.AddHours(2).AddMinutes(20), 8m, null, closedShift.CashierShiftId);
            await InsertCashMovementAsync(closedShift, CashDrawerMovementType.CashIn, 10m, "Small change", seed.UserId);

            await InsertCashierShiftAsync(
                seed.SecondBranchId,
                CashierShiftStatus.Open,
                targetDate.AddHours(6),
                null,
                openingCashAmount: 50m,
                expectedCashAmount: 70m,
                countedCashAmount: null,
                varianceAmount: null,
                openedByUserId: seed.UserId);

            return new DailyCashSalesSeed(paidBill, partialBill, unpaidBill, cancelledBill, closedShift);
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
                CreatedAt = createdAt,
                BusinessDate = (businessDate ?? createdAt.UtcDateTime.Date).Date
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
                OpenedAt = openedAt,
                ClosedAt = closedAt,
                BusinessDate = (businessDate ?? openedAt.UtcDateTime.Date).Date
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

        public async Task<InventoryItem> InsertInventoryItemAsync(
            Guid restaurantId,
            Guid branchId,
            string name,
            string category,
            string unitOfMeasure,
            decimal lowStockThreshold,
            bool isActive,
            DateTimeOffset? updatedAtUtc = null)
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
                CreatedAtUtc = now,
                UpdatedAtUtc = updatedAtUtc
            };

            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            return item;
        }

        public async Task<InventoryMovement> InsertInventoryMovementAsync(
            Guid restaurantId,
            Guid branchId,
            Guid inventoryItemId,
            InventoryMovementType movementType,
            decimal quantity,
            Guid recordedByUserId,
            string? reason = null,
            DateTimeOffset? movementDate = null,
            DateTimeOffset? createdAtUtc = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = createdAtUtc ?? DateTimeOffset.UtcNow;

            var movement = new InventoryMovement
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                MovementType = movementType,
                Quantity = quantity,
                Reason = reason,
                MovementDate = movementDate ?? now,
                RecordedByUserId = recordedByUserId,
                CreatedAtUtc = now
            };

            context.InventoryMovements.Add(movement);
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

    private sealed record DailyCashSalesSeed(
        Bill PaidBill,
        Bill PartialBill,
        Bill UnpaidBill,
        Bill CancelledBill,
        CashierShift ClosedShift);

    private sealed record AuthLoginResponseDto(string AccessToken, string RefreshToken);

    private sealed record VendorDetailDto(
        Guid VendorId,
        Guid RestaurantId,
        Guid? BranchId,
        string Name,
        string NormalizedName,
        string VendorType,
        string? ContactName,
        string? MobileNumber,
        string? Address,
        string? Notes,
        bool IsActive,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record VendorBillDetailDto(
        Guid VendorBillId,
        Guid RestaurantId,
        Guid BranchId,
        Guid VendorId,
        string VendorName,
        string VendorType,
        string? BillNumber,
        DateTime BillDate,
        DateTime? DueDate,
        string Status,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal BalanceAmount,
        string? Notes,
        DateTimeOffset? CancelledAtUtc,
        Guid? CancelledByUserId,
        string? CancellationReason,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}

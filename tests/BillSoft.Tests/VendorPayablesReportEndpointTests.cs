using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Application.Reports;
using BillSoft.Domain.Inventory;
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

public sealed class VendorPayablesReportEndpointTests
{
    [Fact]
    public async Task Report_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_403_When_User_Lacks_Report_View()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var waiter = await fixture.SeedSystemUserAsync(["Cashier"]);
        await fixture.AuthenticateAsync(waiter);

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_Zero_Summary_When_No_Data_Exists()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorPayablesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(admin.RestaurantId, payload!.RestaurantId);
        Assert.Equal(0, payload.Summary.TotalVendorBills);
        Assert.Equal(0m, payload.Summary.TotalPurchaseAmount);
        Assert.Equal(0m, payload.Summary.TotalPaidAmount);
        Assert.Equal(0m, payload.Summary.TotalOutstandingAmount);
        Assert.Equal(0, payload.Summary.UnpaidBillCount);
        Assert.Equal(0, payload.Summary.PartiallyPaidBillCount);
        Assert.Equal(0, payload.Summary.PaidBillCount);
        Assert.Equal(0, payload.Summary.CancelledBillCount);
        Assert.Equal(0, payload.Summary.OverdueBillCount);
        Assert.Empty(payload.VendorBalances);
        Assert.Empty(payload.OverdueBills);
        Assert.Empty(payload.RecentSettlements);
        Assert.Empty(payload.InventoryPurchaseTotals);
    }

    [Fact]
    public async Task Report_Should_Be_Scoped_To_Branch_And_Date_Range()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"], branchless: true);
        var branchOne = await fixture.InsertBranchAsync(admin.RestaurantId, "Main Branch");
        var branchTwo = await fixture.InsertBranchAsync(admin.RestaurantId, "Second Branch");
        await fixture.AuthenticateAsync(admin);

        var vendorOne = await fixture.InsertVendorAsync(admin.RestaurantId, branchOne.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var vendorTwo = await fixture.InsertVendorAsync(admin.RestaurantId, branchTwo.BranchId, "Cool Water", VendorType.Water, true);

        await fixture.InsertBillAsync(
            admin.RestaurantId,
            branchOne.BranchId,
            vendorOne.VendorId,
            "VB-100",
            admin.UserId,
            new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.PartiallyPaid,
            100m,
            40m,
            60m,
            new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc),
            new[]
            {
                new VendorBillLineSeed("Rice", 10m, 10m, null),
            },
            new[]
            {
                new VendorSettlementSeed(VendorSettlementPaymentMode.Cash, 40m, null, new DateTimeOffset(new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc)), VendorSettlementStatus.Active),
            });

        await fixture.InsertBillAsync(
            admin.RestaurantId,
            branchTwo.BranchId,
            vendorTwo.VendorId,
            "VB-200",
            admin.UserId,
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.Unpaid,
            80m,
            0m,
            80m,
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
            new[]
            {
                new VendorBillLineSeed("Water", 8m, 10m, null),
            },
            Array.Empty<VendorSettlementSeed>());

        var response = await fixture.Client.GetAsync($"/api/v1/reports/vendor-payables?branchId={branchOne.BranchId}&fromDate=2026-06-13&toDate=2026-06-18");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorPayablesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(branchOne.BranchId, payload!.BranchId);
        Assert.Equal("2026-06-13", payload.FromDate);
        Assert.Equal("2026-06-18", payload.ToDate);
        Assert.Equal(1, payload.Summary.TotalVendorBills);
        Assert.Equal(100m, payload.Summary.TotalPurchaseAmount);
        Assert.Equal(40m, payload.Summary.TotalPaidAmount);
        Assert.Equal(60m, payload.Summary.TotalOutstandingAmount);
        Assert.Equal(0, payload.Summary.UnpaidBillCount);
        Assert.Equal(1, payload.Summary.PartiallyPaidBillCount);
        Assert.Equal(0, payload.Summary.PaidBillCount);
        Assert.Equal(0, payload.Summary.CancelledBillCount);
        Assert.Equal(1, payload.Summary.OverdueBillCount);
        Assert.Single(payload.VendorBalances);
        Assert.Equal("Fresh Rice", payload.VendorBalances.Single().VendorName);
        Assert.Single(payload.OverdueBills);
        Assert.Single(payload.RecentSettlements);
        Assert.Single(payload.InventoryPurchaseTotals);
        Assert.Equal("Rice", payload.InventoryPurchaseTotals.Single().InventoryItemName);
    }

    [Fact]
    public async Task Report_Should_Exclude_Cancelled_Bills_From_Payable_Totals()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            vendor.VendorId,
            "VB-300",
            admin.UserId,
            new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.Cancelled,
            250m,
            0m,
            250m,
            new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new[]
            {
                new VendorBillLineSeed("Rice", 25m, 10m, null),
            },
            Array.Empty<VendorSettlementSeed>());

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables?fromDate=2026-06-01&toDate=2026-06-30");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorPayablesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Summary.TotalVendorBills);
        Assert.Equal(0m, payload.Summary.TotalPurchaseAmount);
        Assert.Equal(0m, payload.Summary.TotalPaidAmount);
        Assert.Equal(0m, payload.Summary.TotalOutstandingAmount);
        Assert.Equal(0, payload.Summary.OverdueBillCount);
        Assert.Single(payload.VendorBalances);
        Assert.Equal(0m, payload.VendorBalances.Single().OutstandingAmount);
        Assert.Empty(payload.OverdueBills);
    }

    [Fact]
    public async Task Report_Should_Handle_Overdue_Bills_And_Masked_Settlement_References()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            vendor.VendorId,
            "VB-400",
            admin.UserId,
            new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.PartiallyPaid,
            100m,
            30m,
            70m,
            new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
            new[]
            {
                new VendorBillLineSeed("Rice", 10m, 10m, null),
            },
            new[]
            {
                new VendorSettlementSeed(VendorSettlementPaymentMode.UPI, 30m, "UPI-REF-123456", new DateTimeOffset(new DateTime(2026, 6, 12, 9, 15, 0, DateTimeKind.Utc)), VendorSettlementStatus.Active),
            });

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables?fromDate=2026-06-01&toDate=2026-06-30");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorPayablesReportResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload!.OverdueBills);
        Assert.Equal("VB-400", payload.OverdueBills.Single().BillNumber);
        Assert.Single(payload.RecentSettlements);
        Assert.Equal("****3456", payload.RecentSettlements.Single().ReferenceNumberMasked);
    }

    [Fact]
    public async Task Report_Should_Not_Leak_Foreign_Restaurant_Data_Or_Mutate_Records()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignRestaurantAsync();

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var foreignVendor = await fixture.InsertVendorAsync(foreign.RestaurantId, foreign.BranchId, "Foreign Rice", VendorType.Groceries, true);

        await fixture.InsertBillAsync(
            admin.RestaurantId,
            admin.BranchId,
            vendor.VendorId,
            "VB-500",
            admin.UserId,
            new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.Unpaid,
            50m,
            0m,
            50m,
            null,
            new[]
            {
                new VendorBillLineSeed("Rice", 5m, 10m, null),
            },
            Array.Empty<VendorSettlementSeed>());

        await fixture.InsertBillAsync(
            foreign.RestaurantId,
            foreign.BranchId,
            foreignVendor.VendorId,
            "VB-999",
            admin.UserId,
            new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            VendorBillStatus.Paid,
            900m,
            900m,
            0m,
            null,
            Array.Empty<VendorBillLineSeed>(),
            Array.Empty<VendorSettlementSeed>());

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var billCountBefore = await context.VendorBills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var settlementCountBefore = await context.VendorSettlements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);
        var movementCountBefore = await context.InventoryMovements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId);

        var response = await fixture.Client.GetAsync("/api/v1/reports/vendor-payables?fromDate=2026-06-01&toDate=2026-06-30");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorPayablesReportResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Summary.TotalVendorBills);
        Assert.Equal(50m, payload.Summary.TotalPurchaseAmount);
        Assert.Equal(billCountBefore, await context.VendorBills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(settlementCountBefore, await context.VendorSettlements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(movementCountBefore, await context.InventoryMovements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
    }

    [Fact]
    public async Task BranchId_From_Other_Restaurant_Should_Return_404()
    {
        await using var fixture = await VendorPayablesReportApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"], branchless: true);
        await fixture.AuthenticateAsync(admin);
        var foreign = await fixture.SeedForeignRestaurantAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/reports/vendor-payables?branchId={foreign.BranchId}");

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

    private sealed class VendorPayablesReportApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<VendorPayablesReportApiFactory> CreateAsync()
        {
            var factory = new VendorPayablesReportApiFactory();
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

        public async Task<SeedResult> SeedSystemUserAsync(IReadOnlyCollection<string> roleNames, bool branchless = false)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = "Vendor Report Test Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTVR01");
            restaurant.SetCountryProfile("SG");

            var branch = branchless
                ? null
                : new Branch
                {
                    Name = "Main Branch",
                    RestaurantId = restaurant.RestaurantId,
                    Status = BranchStatus.Active,
                    Timezone = "Asia/Singapore",
                    Currency = "SGD"
                };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch?.BranchId,
                FullName = "Vendor Admin",
                MobileNumber = branchless ? "90001000" : "90001001",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            context.Restaurants.Add(restaurant);
            if (branch is not null)
            {
                context.Branches.Add(branch);
            }

            context.Users.Add(user);
            foreach (var roleName in roleNames)
            {
                var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = assignedRole.RoleId,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }

            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                branch?.BranchId ?? Guid.Empty,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<string> AuthenticateAsync(SeedResult seed)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                restaurantCode = seed.RestaurantCode,
                mobileNumber = seed.MobileNumber,
                password = seed.Password
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponseDto>();
            Assert.NotNull(payload);

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
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
                Name = "Foreign Report Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTVR02");

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

        public async Task<Vendor> InsertVendorAsync(Guid restaurantId, Guid? branchId, string name, VendorType vendorType, bool isActive)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var vendor = new Vendor
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                Name = name.Trim(),
                NormalizedName = Vendor.NormalizeKey(name),
                VendorType = vendorType,
                IsActive = isActive,
                CreatedAtUtc = now
            };

            context.Vendors.Add(vendor);
            await context.SaveChangesAsync();
            return vendor;
        }

        public async Task<InventoryItem> InsertInventoryItemAsync(Guid restaurantId, Guid branchId, string name)
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
                Category = "Ingredients",
                UnitOfMeasure = "kg",
                LowStockThreshold = 1m,
                IsActive = true,
                CreatedAtUtc = now
            };

            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            return item;
        }

        public async Task<VendorBill> InsertBillAsync(
            Guid restaurantId,
            Guid branchId,
            Guid vendorId,
            string billNumber,
            Guid recordedByUserId,
            DateTime billDate,
            VendorBillStatus status,
            decimal totalAmount,
            decimal paidAmount,
            decimal balanceAmount,
            DateTime? dueDate,
            IReadOnlyCollection<VendorBillLineSeed> lines,
            IReadOnlyCollection<VendorSettlementSeed> settlements)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var bill = new VendorBill
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                VendorId = vendorId,
                BillNumber = billNumber,
                BillDate = billDate,
                DueDate = dueDate,
                Status = status,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                BalanceAmount = balanceAmount,
                CreatedAtUtc = now
            };

            context.VendorBills.Add(bill);
            await context.SaveChangesAsync();

            foreach (var line in lines)
            {
                context.VendorBillLines.Add(new VendorBillLine
                {
                    RestaurantId = restaurantId,
                    BranchId = branchId,
                    VendorBillId = bill.VendorBillId,
                    InventoryItemId = line.InventoryItemId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    LineTotal = line.Quantity * line.UnitCost,
                    CreatedAtUtc = now
                });
            }

            foreach (var settlement in settlements)
            {
                context.VendorSettlements.Add(new VendorSettlement
                {
                    RestaurantId = restaurantId,
                    BranchId = branchId,
                    VendorBillId = bill.VendorBillId,
                    PaymentMode = settlement.PaymentMode,
                    Amount = settlement.Amount,
                    ReferenceNumber = settlement.ReferenceNumber,
                    PaidAtUtc = settlement.PaidAtUtc,
                    RecordedByUserId = recordedByUserId,
                    Status = settlement.Status,
                    CreatedAtUtc = settlement.PaidAtUtc,
                });
            }

            await context.SaveChangesAsync();
            return bill;
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

    private sealed record VendorBillLineSeed(string Description, decimal Quantity, decimal UnitCost, Guid? InventoryItemId);

    private sealed record VendorSettlementSeed(
        VendorSettlementPaymentMode PaymentMode,
        decimal Amount,
        string? ReferenceNumber,
        DateTimeOffset PaidAtUtc,
        VendorSettlementStatus Status);
}

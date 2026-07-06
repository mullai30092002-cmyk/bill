using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class VendorFoundationEndpointTests
{
    [Fact]
    public async Task Create_Vendor_Should_Create_And_Reject_Duplicate_Name_In_Same_Scope_Case_Insensitive()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
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

        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = " fresh rice ",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010001",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Contains(await context.AuditLogs.Select(log => log.Action).ToListAsync(), action => action == "Vendor.Created");
    }

    [Fact]
    public async Task Create_Vendor_Should_Require_Mobile_And_Reject_Duplicate_Mobile_In_Same_Restaurant_Only()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var missingMobile = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = "Fresh Rice",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = (string?)null,
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingMobile.StatusCode);
        var missingMobileBody = await missingMobile.Content.ReadAsStringAsync();
        Assert.Contains("Mobile number is required.", missingMobileBody);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = "Fresh Rice",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = " 90010001 ",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        first.EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var legacyVendor = await context.Vendors.SingleAsync(entity =>
                entity.RestaurantId == admin.RestaurantId &&
                entity.MobileNumber == "90010001");

            legacyVendor.NormalizedMobileNumber = null;
            await context.SaveChangesAsync();
        }

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = admin.BranchId,
            name = "Second Vendor",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010001",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Contains("Vendor mobile number already exists.", secondBody);

        await using var foreignFixture = await VendorApiFactory.CreateAsync();
        var foreignAdmin = await foreignFixture.SeedUserAsync(["Admin"]);
        await foreignFixture.AuthenticateAsync(foreignAdmin);

        var foreignRestaurant = await foreignFixture.SeedForeignRestaurantAsync();
        await foreignFixture.InsertVendorAsync(
            foreignRestaurant.RestaurantId,
            foreignRestaurant.BranchId,
            "Foreign Vendor",
            VendorType.Groceries,
            true,
            "90010001");

        var foreignAllowed = await foreignFixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = foreignAdmin.BranchId,
            name = "Foreign Vendor 2",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010001",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        foreignAllowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Vendor_Should_Allow_Same_Name_In_Different_Branch_When_Branch_Scoped()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var owner = await fixture.SeedUserAsync(["Admin"], branchless: true);
        var firstBranch = await fixture.InsertBranchAsync(owner.RestaurantId, "First Branch");
        var secondBranch = await fixture.InsertBranchAsync(owner.RestaurantId, "Second Branch");
        await fixture.AuthenticateAsync(owner);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = firstBranch.BranchId,
            name = "Fresh Rice",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010001",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });
        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/vendors", new
        {
            branchId = secondBranch.BranchId,
            name = "Fresh Rice",
            vendorType = "Groceries",
            contactName = "Kumar",
            mobileNumber = "90010002",
            address = "Market Road",
            notes = "Daily vendor",
            isActive = true
        });

        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_VendorBill_Should_Reject_Duplicate_Bill_Number_For_Same_Vendor_And_Allow_Same_Number_For_Different_Vendor()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(accounts);

        var firstVendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Fresh Rice", VendorType.Groceries, true, "90010001");
        var secondVendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Cool Water", VendorType.Water, true, "90010002");

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = firstVendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = " vb-001 ",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Morning purchase",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });

        first.EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var legacyBill = await context.VendorBills.SingleAsync(entity =>
                entity.RestaurantId == accounts.RestaurantId &&
                entity.VendorId == firstVendor.VendorId &&
                entity.BillNumber == "vb-001");

            legacyBill.NormalizedBillNumber = null;
            await context.SaveChangesAsync();
        }

        var duplicate = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = firstVendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-001",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Duplicate bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("This bill number already exists for the selected vendor.", duplicateBody);

        var sameBillNumberDifferentVendor = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = secondVendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-001",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Same bill number on a different vendor",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Water",
                    quantity = 1m,
                    unitCost = 25m
                }
            }
        });

        sameBillNumberDifferentVendor.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_VendorBill_With_Inventory_Line_Should_Create_And_Link_StockIn_Movement()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var inventoryItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = admin.BranchId,
            billNumber = "VB-001",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Morning purchase",
            lines = new[]
            {
                new
                {
                    inventoryItemId = inventoryItem.InventoryItemId,
                    description = "Rice",
                    quantity = 10m,
                    unitCost = 10m
                }
            }
        });

        if (!response.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(await response.Content.ReadAsStringAsync());
        }
        var payload = await response.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(100m, payload!.TotalAmount);
        Assert.Equal(100m, payload.BalanceAmount);
        Assert.Single(payload.Lines);
        Assert.NotEqual(Guid.Empty, payload.Lines[0].InventoryMovementId ?? Guid.Empty);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var movements = await context.InventoryMovements.Where(entity => entity.InventoryItemId == inventoryItem.InventoryItemId).ToListAsync();
        Assert.Single(movements);
        Assert.Equal(10m, movements[0].Quantity);
        Assert.Equal(10m, movements[0].UnitCost);
    }

    [Fact]
    public async Task Record_Settlement_Should_Reduce_Balance_And_Reject_Overpayment()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(accounts);

        var vendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Fresh Rice", VendorType.Groceries, true);

        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-002",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Week one purchase",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        if (!billResponse.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(await billResponse.Content.ReadAsStringAsync());
        }
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);
        Assert.Equal(100m, bill!.BalanceAmount);

        var firstSettlement = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 40m,
            referenceNumber = (string?)null,
            notes = "Opening settlement",
            paidAtUtc = DateTimeOffset.UtcNow
        });
        firstSettlement.EnsureSuccessStatusCode();
        var afterFirstSettlement = await firstSettlement.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(afterFirstSettlement);
        Assert.Equal(60m, afterFirstSettlement!.BalanceAmount);
        Assert.Equal("PartiallyPaid", afterFirstSettlement.Status);
        var firstRecordedSettlement = Assert.Single(afterFirstSettlement.Settlements);
        Assert.Equal("Opening settlement", firstRecordedSettlement.Notes);
        Assert.Equal(100m, firstRecordedSettlement.PreviousOutstandingAmount);
        Assert.Equal(60m, firstRecordedSettlement.NewOutstandingAmount);

        var finalSettlement = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 60m,
            referenceNumber = (string?)null,
            notes = "Closing settlement",
            paidAtUtc = DateTimeOffset.UtcNow
        });
        finalSettlement.EnsureSuccessStatusCode();
        var paidBill = await finalSettlement.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(paidBill);
        Assert.Equal(0m, paidBill!.BalanceAmount);
        Assert.Equal("Paid", paidBill.Status);
        Assert.Equal(2, paidBill.Settlements.Length);
        Assert.Equal("Closing settlement", paidBill.Settlements.OrderBy(settlement => settlement.CreatedAtUtc).Last().Notes);
        Assert.Equal(0m, paidBill.Settlements.OrderBy(settlement => settlement.CreatedAtUtc).Last().NewOutstandingAmount);

        var overpayment = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 1m,
            referenceNumber = (string?)null,
            paidAtUtc = DateTimeOffset.UtcNow
        });
        Assert.Equal(HttpStatusCode.BadRequest, overpayment.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(2, await context.AuditLogs.CountAsync(log => log.Action == "VendorSettlement.Created"));
    }

    [Fact]
    public async Task Record_Settlement_Should_Reject_Invalid_Amount()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(accounts);

        var vendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-010",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Invalid amount test",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill!.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 0m,
            referenceNumber = (string?)null,
            notes = "Invalid",
            paidAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Record_Settlement_Should_Reject_Missing_Payment_Mode()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(accounts);

        var vendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-011",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Missing payment mode test",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill!.VendorBillId}/settlements", new
        {
            paymentMode = (string?)null,
            amount = 10m,
            referenceNumber = (string?)null,
            notes = "Invalid",
            paidAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Record_Settlement_Should_Require_Reference_For_Non_Cash_Modes()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"]);
        await fixture.AuthenticateAsync(accounts);

        var vendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-012",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Reference test",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill!.VendorBillId}/settlements", new
        {
            paymentMode = "UPI",
            amount = 10m,
            referenceNumber = (string?)null,
            notes = "Invalid",
            paidAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Vendor_Statement_Should_Return_Bills_Settlements_And_Running_Balance()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var accounts = await fixture.SeedUserAsync(["Admin", "AccountsUser"], branchless: true);
        var branch = await fixture.InsertBranchAsync(accounts.RestaurantId, "Statement Branch");
        await fixture.AuthenticateAsync(accounts);

        var vendor = await fixture.InsertVendorAsync(accounts.RestaurantId, branch.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var otherVendor = await fixture.InsertVendorAsync(accounts.RestaurantId, accounts.BranchId, "Other Vendor", VendorType.Groceries, true);

        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = branch.BranchId,
            billNumber = "VB-020",
            billDate = new DateTime(2026, 6, 12),
            dueDate = new DateTime(2026, 6, 14),
            notes = "Statement bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Bulk rice",
                    quantity = 1m,
                    unitCost = 100m
                }
            }
        });
        billResponse.EnsureSuccessStatusCode();
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);

        var settlementResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill!.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 40m,
            referenceNumber = (string?)null,
            notes = "Statement settlement",
            paidAtUtc = new DateTimeOffset(new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc))
        });
        settlementResponse.EnsureSuccessStatusCode();

        await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = otherVendor.VendorId,
            branchId = accounts.BranchId,
            billNumber = "VB-021",
            billDate = new DateTime(2026, 6, 12),
            dueDate = (DateTime?)null,
            notes = "Other vendor bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Tea",
                    quantity = 1m,
                    unitCost = 25m
                }
            }
        });

        var response = await fixture.Client.GetAsync($"/api/v1/vendors/{vendor.VendorId}/statement?branchId={branch.BranchId}&fromDate=2026-06-01&toDate=2026-06-30");
        await EnsureSuccessStatusCodeAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<VendorStatementResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(accounts.RestaurantId, payload!.RestaurantId);
        Assert.Equal(branch.BranchId, payload.BranchId);
        Assert.Equal("Statement Branch", payload.BranchName);
        Assert.Equal(vendor.VendorId, payload.VendorId);
        Assert.Equal(100m, payload.Summary.TotalBillAmount);
        Assert.Equal(40m, payload.Summary.TotalSettlementAmount);
        Assert.Equal(1, payload.Summary.PayableBillCount);
        Assert.Equal(1, payload.Summary.SettlementCount);
        Assert.Equal(1, payload.PayableBills.Length);
        Assert.Equal(60m, payload.CurrentOutstandingAmount);
        Assert.Equal(0m, payload.OpeningOutstandingAmount);
        Assert.Single(payload.Settlements);
        Assert.Null(payload.Settlements.Single().ReferenceNumberMasked);
        Assert.Equal(2, payload.Timeline.Length);
        Assert.Contains(payload.Timeline, item => item.EntryType == "Bill");
        Assert.Contains(payload.Timeline, item => item.EntryType == "Settlement" && item.CreditAmount == 40m);
        Assert.DoesNotContain(payload.PayableBills, item => item.BillNumber == "VB-021");
    }

    [Fact]
    public async Task Cancelled_Vendor_Bill_Should_Reject_New_Settlements()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);

        var billResponse = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = admin.BranchId,
            billNumber = "VB-003",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "No stock movement bill",
            lines = new[]
            {
                new
                {
                    inventoryItemId = (Guid?)null,
                    description = "Service fee",
                    quantity = 1m,
                    unitCost = 25m
                }
            }
        });
        if (!billResponse.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(await billResponse.Content.ReadAsStringAsync());
        }
        var bill = await billResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);

        var cancelResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill!.VendorBillId}/cancel", new { reason = "Supplier error" });
        cancelResponse.EnsureSuccessStatusCode();

        var settlement = await fixture.Client.PostAsJsonAsync($"/api/v1/vendor-bills/{bill.VendorBillId}/settlements", new
        {
            paymentMode = "Cash",
            amount = 5m,
            referenceNumber = (string?)null,
            paidAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, settlement.StatusCode);
    }

    [Fact]
    public async Task Cross_Branch_Inventory_Item_On_Vendor_Bill_Should_Be_Rejected()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin"]);
        var otherBranch = await fixture.InsertBranchAsync(admin.RestaurantId, "Other Branch");
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var foreignItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, otherBranch.BranchId, "Rice", "Grains", "kg", 10m, true);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/vendor-bills", new
        {
            vendorId = vendor.VendorId,
            branchId = admin.BranchId,
            billNumber = "VB-004",
            billDate = DateTime.UtcNow.Date,
            dueDate = (DateTime?)null,
            notes = "Wrong branch item",
            lines = new[]
            {
                new
                {
                    inventoryItemId = foreignItem.InventoryItemId,
                    description = "Rice",
                    quantity = 10m,
                    unitCost = 10m
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cross_Restaurant_Vendor_Bill_Access_Should_Be_Rejected()
    {
        await using var fixture = await VendorApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync(["Admin"]);
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(foreign.RestaurantId, foreign.BranchId, "Foreign Vendor", VendorType.Groceries, true);
        var bill = await fixture.InsertVendorBillAsync(foreign.RestaurantId, foreign.BranchId, vendor.VendorId, "VB-FOREIGN-1");

        var response = await fixture.Client.GetAsync($"/api/v1/vendor-bills/{bill.VendorBillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class VendorApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<VendorApiFactory> CreateAsync()
        {
            var factory = new VendorApiFactory();
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

        public async Task<SeedResult> SeedUserAsync(IReadOnlyCollection<string> roleNames, bool branchless = false)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = "Vendor Test Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTV01");
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
                MobileNumber = branchless ? "90000044" : "90000045",
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
            restaurant.SetRestaurantCode("RESTFOREIGN02");

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

        public async Task<Vendor> InsertVendorAsync(
            Guid restaurantId,
            Guid branchId,
            string name,
            VendorType vendorType,
            bool isActive,
            string? mobileNumber = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var now = DateTimeOffset.UtcNow;

            var vendor = new Vendor
            {
                RestaurantId = restaurantId,
                BranchId = branchId == Guid.Empty ? null : branchId,
                Name = name.Trim(),
                NormalizedName = Vendor.NormalizeKey(name),
                VendorType = vendorType,
                MobileNumber = string.IsNullOrWhiteSpace(mobileNumber) ? null : mobileNumber.Trim(),
                NormalizedMobileNumber = string.IsNullOrWhiteSpace(mobileNumber) ? null : mobileNumber.Trim().ToUpperInvariant(),
                IsActive = isActive,
                CreatedAtUtc = now
            };

            context.Vendors.Add(vendor);
            await context.SaveChangesAsync();
            return vendor;
        }

        public async Task<InventoryItem> InsertInventoryItemAsync(
            Guid restaurantId,
            Guid branchId,
            string name,
            string category,
            string unitOfMeasure,
            decimal lowStockThreshold,
            bool isActive)
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

        public async Task<VendorBill> InsertVendorBillAsync(Guid restaurantId, Guid branchId, Guid vendorId, string billNumber)
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
                NormalizedBillNumber = string.IsNullOrWhiteSpace(billNumber) ? null : billNumber.Trim().ToUpperInvariant(),
                BillDate = DateTime.UtcNow.Date,
                Status = VendorBillStatus.Unpaid,
                TotalAmount = 25m,
                PaidAmount = 0m,
                BalanceAmount = 25m,
                CreatedAtUtc = now
            };

            context.VendorBills.Add(bill);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var trackedEntities = string.Join(", ", ex.Entries.Select(entry => entry.Entity.GetType().Name));
                var restaurantExists = await context.Restaurants.AnyAsync(entity => entity.RestaurantId == restaurantId);
                var branchExists = await context.Branches.AnyAsync(entity => entity.BranchId == branchId);
                var vendorExists = await context.Vendors.AnyAsync(entity => entity.VendorId == vendorId);
                throw new Xunit.Sdk.XunitException(
                    $"{trackedEntities}: {ex.GetBaseException().Message}; restaurant={restaurantExists}; branch={branchExists}; vendor={vendorExists}; bill.RestaurantId={bill.RestaurantId}; bill.BranchId={bill.BranchId}; bill.VendorId={bill.VendorId}");
            }
            return bill;
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

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException($"Expected success, got {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
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
        DateTimeOffset? UpdatedAtUtc,
        VendorBillLineDetailDto[] Lines,
        VendorSettlementDetailDto[] Settlements);

    private sealed record VendorBillLineDetailDto(
        Guid VendorBillLineId,
        Guid? InventoryItemId,
        string? InventoryItemName,
        Guid? InventoryMovementId,
        string Description,
        decimal Quantity,
        decimal UnitCost,
        decimal LineTotal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record VendorSettlementDetailDto(
        Guid VendorSettlementId,
        string PaymentMode,
        string Status,
        decimal Amount,
        string? ReferenceNumber,
        DateTimeOffset PaidAtUtc,
        Guid RecordedByUserId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc,
        DateTimeOffset? CancelledAtUtc,
        Guid? CancelledByUserId,
        string? CancellationReason,
        string? Notes,
        decimal PreviousOutstandingAmount,
        decimal NewOutstandingAmount);

    private sealed record VendorStatementResponseDto(
        Guid RestaurantId,
        Guid? BranchId,
        string? BranchName,
        Guid VendorId,
        string VendorName,
        string VendorType,
        string CurrencyCode,
        DateTime FromDate,
        DateTime ToDate,
        DateTimeOffset GeneratedAt,
        decimal OpeningOutstandingAmount,
        decimal CurrentOutstandingAmount,
        VendorStatementSummaryDto Summary,
        VendorStatementBillItemDto[] PayableBills,
        VendorStatementSettlementItemDto[] Settlements,
        VendorStatementTimelineItemDto[] Timeline);

    private sealed record VendorStatementSummaryDto(
        decimal TotalBillAmount,
        decimal TotalSettlementAmount,
        int PayableBillCount,
        int SettlementCount,
        int OverdueBillCount);

    private sealed record VendorStatementBillItemDto(
        Guid VendorBillId,
        Guid BranchId,
        string? BranchName,
        string? BillNumber,
        DateTime BillDate,
        DateTime? DueDate,
        string Status,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal OutstandingAmount,
        string? Notes,
        DateTimeOffset CreatedAtUtc);

    private sealed record VendorStatementSettlementItemDto(
        Guid VendorSettlementId,
        Guid VendorBillId,
        Guid BranchId,
        string? BranchName,
        string? BillNumber,
        DateTimeOffset PaidAtUtc,
        string PaymentMode,
        decimal Amount,
        string? ReferenceNumberMasked,
        string? Notes,
        decimal PreviousOutstandingAmount,
        decimal NewOutstandingAmount,
        string Status);

    private sealed record VendorStatementTimelineItemDto(
        string EntryType,
        DateTimeOffset TimestampUtc,
        string? BillNumber,
        string? Reference,
        string? Description,
        decimal DebitAmount,
        decimal CreditAmount,
        decimal RunningBalance,
        string? PaymentMode,
        string? Status);
}

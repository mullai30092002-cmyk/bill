using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Auth;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using BillSoft.Infrastructure.Vendors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BillSoft.Tests;

public sealed class VendorBillOcrDraftEndpointTests
{
    [Fact]
    public async Task Upload_Should_Reject_Unsupported_File_Type()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        using var response = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "bill.txt",
            contentType: "text/plain",
            contentText: "Vendor: Fresh Rice");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Should_Create_Draft_And_Allow_Review_And_Confirm_With_Inventory_Link()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var inventoryItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        using var uploadResponse = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "vendor-bill.pdf",
            contentType: "application/pdf",
            contentText: """
Vendor: Fresh Rice
BillNumber: OCR-100
BillDate: 2026-06-18
TotalAmount: 100
Line: Rice|10|10
""");

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var draft = await uploadResponse.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);
        Assert.Equal("Extracted", draft!.Status);
        Assert.Equal("Fresh Rice", draft.ExtractedVendorName);
        Assert.Equal("OCR-100", draft.ExtractedBillNumber);
        Assert.Equal(1, draft.Lines.Length);

        using var updateResponse = await fixture.UpdateDraftAsync(
            draft.VendorBillOcrDraftId,
            new
            {
                reviewedVendorId = vendor.VendorId,
                reviewedBillNumber = "OCR-100-REV",
                reviewedBillDate = "2026-06-19",
                reviewedTotalAmount = 100m,
                lines = new[]
                {
                    new
                    {
                        vendorBillOcrDraftLineId = draft.Lines[0].VendorBillOcrDraftLineId,
                        reviewedDescription = "Rice",
                        reviewedQuantity = 10m,
                        reviewedUnitCost = 10m,
                        reviewedLineTotal = 100m,
                        selectedInventoryItemId = inventoryItem.InventoryItemId,
                        isIgnored = false
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(updatedDraft);
        Assert.Equal(vendor.VendorId, updatedDraft!.ReviewedVendorId);
        Assert.Equal("OCR-100-REV", updatedDraft.ReviewedBillNumber);
        Assert.Equal(10m, updatedDraft.Lines.Single().ReviewedQuantity);
        Assert.False(updatedDraft.Lines.Single().IsIgnored);

        using var confirmResponse = await fixture.ConfirmDraftAsync(draft.VendorBillOcrDraftId);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var bill = await confirmResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);
        Assert.Equal("OCR-100-REV", bill!.BillNumber);
        Assert.Equal(100m, bill.TotalAmount);
        Assert.Equal(0m, bill.PaidAmount);
        Assert.Equal(100m, bill.BalanceAmount);
        Assert.Single(bill.Lines);
        Assert.Equal(inventoryItem.InventoryItemId, bill.Lines[0].InventoryItemId);
        Assert.NotEqual(Guid.Empty, bill.Lines[0].InventoryMovementId ?? Guid.Empty);
        Assert.Empty(bill.Settlements);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(1, await context.VendorBills.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));
        Assert.Equal(0, await context.VendorSettlements.CountAsync(entity => entity.RestaurantId == admin.RestaurantId));

        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "VendorBillOcrDraft" || log.EntityType == "VendorBill")
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("VendorBillOcrDraft.Uploaded", actions);
        Assert.Contains("VendorBillOcrDraft.Updated", actions);
        Assert.Contains("VendorBillOcrDraft.Confirmed", actions);
        Assert.Contains("VendorBill.Created", actions);
    }

    [Fact]
    public async Task Drafts_Should_Be_Scoped_To_Restaurant_And_Branch()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var owner = await fixture.SeedSystemUserAsync(["Admin"], branchless: true);
        var firstBranch = await fixture.InsertBranchAsync(owner.RestaurantId, "First Branch");
        var secondBranch = await fixture.InsertBranchAsync(owner.RestaurantId, "Second Branch");
        await fixture.AuthenticateAsync(owner);

        await fixture.UploadDraftAsync(firstBranch.BranchId, "branch-1.pdf", "application/pdf", "Vendor: First Branch Vendor");
        await fixture.UploadDraftAsync(secondBranch.BranchId, "branch-2.pdf", "application/pdf", "Vendor: Second Branch Vendor");

        using var response = await fixture.Client.GetAsync($"/api/v1/vendor-bill-ocr/drafts?branchId={firstBranch.BranchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<VendorBillOcrDraftListResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items);
        Assert.Equal(firstBranch.BranchId, payload.Items.Single().BranchId);
    }

    [Fact]
    public async Task Confirm_Should_Reject_Unmapped_Stock_Lines_And_Allow_Explicit_Ignored_Lines()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var vendor = await fixture.InsertVendorAsync(admin.RestaurantId, admin.BranchId, "Fresh Rice", VendorType.Groceries, true);
        var inventoryItem = await fixture.InsertInventoryItemAsync(admin.RestaurantId, admin.BranchId, "Rice", "Grains", "kg", 10m, true);

        using var uploadResponse = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "vendor-bill.pdf",
            contentType: "application/pdf",
            contentText: """
Vendor: Fresh Rice
BillNumber: OCR-101
BillDate: 2026-06-18
TotalAmount: 50
Line: Rice|5|10
""");

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var draft = await uploadResponse.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);

        using var blockedUpdate = await fixture.UpdateDraftAsync(
            draft!.VendorBillOcrDraftId,
            new
            {
                reviewedVendorId = vendor.VendorId,
                reviewedBillNumber = "OCR-101",
                reviewedBillDate = "2026-06-18",
                reviewedTotalAmount = 50m,
                lines = new[]
                {
                    new
                    {
                        vendorBillOcrDraftLineId = draft.Lines[0].VendorBillOcrDraftLineId,
                        reviewedDescription = "Rice",
                        reviewedQuantity = 5m,
                        reviewedUnitCost = 10m,
                        reviewedLineTotal = 50m,
                        selectedInventoryItemId = (string?)null,
                        isIgnored = false
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, blockedUpdate.StatusCode);

        using var blockedConfirm = await fixture.ConfirmDraftAsync(draft.VendorBillOcrDraftId);
        Assert.Equal(HttpStatusCode.BadRequest, blockedConfirm.StatusCode);
        var blockedDetail = await blockedConfirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(blockedDetail.TryGetProperty("detail", out var blockedMessage));
        Assert.Contains("mapped to an inventory item or marked ignored", blockedMessage.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        using var ignoredUpdate = await fixture.UpdateDraftAsync(
            draft.VendorBillOcrDraftId,
            new
            {
                reviewedVendorId = vendor.VendorId,
                reviewedBillNumber = "OCR-101",
                reviewedBillDate = "2026-06-18",
                reviewedTotalAmount = 50m,
                lines = new[]
                {
                    new
                    {
                        vendorBillOcrDraftLineId = draft.Lines[0].VendorBillOcrDraftLineId,
                        reviewedDescription = "Rice",
                        reviewedQuantity = 5m,
                        reviewedUnitCost = 10m,
                        reviewedLineTotal = 50m,
                        selectedInventoryItemId = inventoryItem.InventoryItemId,
                        isIgnored = true
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, ignoredUpdate.StatusCode);
        var ignoredDraft = await ignoredUpdate.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(ignoredDraft);
        Assert.True(ignoredDraft!.Lines.Single().IsIgnored);

        using var confirmResponse = await fixture.ConfirmDraftAsync(draft.VendorBillOcrDraftId);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var bill = await confirmResponse.Content.ReadFromJsonAsync<VendorBillDetailDto>();
        Assert.NotNull(bill);
        Assert.Single(bill!.Lines);
        Assert.Null(bill.Lines.Single().InventoryItemId);
        Assert.Null(bill.Lines.Single().InventoryMovementId);
    }

    [Fact]
    public async Task Duplicate_Receipt_Should_Warn_And_Block_Confirmation_Without_Override_Permission()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var inventoryUser = await fixture.SeedSystemUserAsync(["InventoryUser"], stripVendorBillOverridePermission: true);
        await fixture.AuthenticateAsync(inventoryUser);

        var vendor = await fixture.InsertVendorAsync(inventoryUser.RestaurantId, inventoryUser.BranchId, "Fresh Rice", VendorType.Groceries, true);
        await fixture.InsertVendorBillAsync(
            inventoryUser.RestaurantId,
            inventoryUser.BranchId,
            vendor.VendorId,
            "DUP-101",
            new DateTime(2026, 6, 18),
            50m);

        using var uploadResponse = await fixture.UploadDraftAsync(
            branchId: inventoryUser.BranchId,
            fileName: "duplicate.pdf",
            contentType: "application/pdf",
            contentText: """
Vendor: Fresh Rice
BillNumber: DUP-101
BillDate: 2026-06-18
TotalAmount: 50
Line: Rice|5|10
""");

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var draft = await uploadResponse.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);

        using var updateResponse = await fixture.UpdateDraftAsync(
            draft!.VendorBillOcrDraftId,
            new
            {
                reviewedVendorId = vendor.VendorId,
                reviewedBillNumber = "DUP-101",
                reviewedBillDate = "2026-06-18",
                reviewedTotalAmount = 50m,
                lines = new[]
                {
                    new
                    {
                        vendorBillOcrDraftLineId = draft.Lines[0].VendorBillOcrDraftLineId,
                        reviewedDescription = "Rice",
                        reviewedQuantity = 5m,
                        reviewedUnitCost = 10m,
                        reviewedLineTotal = 50m,
                        selectedInventoryItemId = (string?)null,
                        isIgnored = true
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(updatedDraft);
        Assert.True(updatedDraft!.HasDuplicateReceipt);
        Assert.NotNull(updatedDraft.DuplicateReceiptWarning);

        using var confirmResponse = await fixture.ConfirmDraftAsync(draft.VendorBillOcrDraftId);
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
        var problem = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("detail", out var detail));
        Assert.Contains("matching vendor bill already exists", detail.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cross_Restaurant_Draft_Access_Should_Be_Rejected()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(admin);

        var uploadResponse = await fixture.UploadDraftAsync(foreign.BranchId, "foreign.pdf", "application/pdf", "Vendor: Foreign Vendor");
        Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_Failure_Should_Not_Create_Vendor_Bill_And_Should_Write_Audit()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        var beforeBills = await fixture.CountVendorBillsAsync(admin.RestaurantId);

        using var response = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "broken.pdf",
            contentType: "application/pdf",
            contentText: "Vendor: FAIL");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);
        Assert.Equal("ExtractionFailed", draft!.Status);
        Assert.NotNull(draft.SafeErrorMessage);

        Assert.Equal(beforeBills, await fixture.CountVendorBillsAsync(admin.RestaurantId));

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "VendorBillOcrDraft")
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("VendorBillOcrDraft.ExtractionFailed", actions);
    }

    [Fact]
    public async Task Upload_Should_Use_Configured_Ocr_MaxUploadBytes_Limit()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync(maxUploadBytes: 1);
        using (var scope = fixture.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptions<OcrOptions>>().Value;
            Assert.Equal(1, options.MaxUploadBytes);
        }

        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        using var response = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "too-large.pdf",
            contentType: "application/pdf",
            contentText: "ab");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("detail", out var detail));
        Assert.Contains("too large", detail.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Partial_Extraction_Should_Store_Warnings_And_Stay_Reviewable()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        using var response = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "partial.pdf",
            contentType: "application/pdf",
            contentText: """
BillNumber: OCR-200
Line: Rice|2|5
Warning: image was slightly blurred
""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);
        Assert.Equal("Extracted", draft!.Status);
        Assert.NotEmpty(draft.ProviderWarnings);
        Assert.Contains(draft.ProviderWarnings, warning => warning.Contains("blurred", StringComparison.OrdinalIgnoreCase));
        Assert.Null(draft.SafeErrorMessage);
    }

    [Fact]
    public async Task Provider_Exception_Should_Be_Sanitized_In_Draft_And_Audit()
    {
        await using var fixture = await VendorBillOcrDraftApiFactory.CreateAsync(useThrowingProvider: true);
        var admin = await fixture.SeedSystemUserAsync(["Admin"]);
        await fixture.AuthenticateAsync(admin);

        using var response = await fixture.UploadDraftAsync(
            branchId: admin.BranchId,
            fileName: "throwing.pdf",
            contentType: "application/pdf",
            contentText: "Vendor: Example");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<VendorBillOcrDraftDetailDto>();
        Assert.NotNull(draft);
        Assert.Equal("ExtractionFailed", draft!.Status);
        Assert.DoesNotContain("RAW-AZURE-123", draft.SafeErrorMessage ?? string.Empty, StringComparison.Ordinal);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var auditReason = await context.AuditLogs
            .Where(log => log.EntityType == "VendorBillOcrDraft" && log.Action == "VendorBillOcrDraft.ExtractionFailed")
            .Select(log => log.Reason)
            .SingleAsync();

        Assert.DoesNotContain("RAW-AZURE-123", auditReason ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed class VendorBillOcrDraftApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;
        private readonly long _maxUploadBytes;
        private readonly bool _useThrowingProvider;

        public HttpClient Client => _client ??= CreateClient();

        private VendorBillOcrDraftApiFactory(long maxUploadBytes, bool useThrowingProvider)
        {
            _maxUploadBytes = maxUploadBytes;
            _useThrowingProvider = useThrowingProvider;
        }

        public VendorBillOcrDraftApiFactory() : this(10 * 1024 * 1024, false)
        {
        }

        public static async Task<VendorBillOcrDraftApiFactory> CreateAsync(long maxUploadBytes = 10 * 1024 * 1024, bool useThrowingProvider = false)
        {
            var factory = new VendorBillOcrDraftApiFactory(maxUploadBytes, useThrowingProvider);
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

        public async Task<SeedResult> SeedSystemUserAsync(
            IReadOnlyCollection<string> roleNames,
            bool branchless = false,
            bool stripVendorBillOverridePermission = false)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            if (stripVendorBillOverridePermission)
            {
                var overridePermission = await context.Permissions.SingleAsync(permission => permission.Code == SystemPermissions.VendorBillOverrideOcr);
                var targetRoles = roleNames
                    .Select(roleName => roleName.Trim())
                    .ToArray();

                var targetRoleIds = await context.Roles
                    .Where(role => role.RestaurantId == null && targetRoles.Contains(role.Name))
                    .Select(role => role.RoleId)
                    .ToListAsync();

                var rolePermissions = await context.RolePermissions
                    .Where(rolePermission => targetRoleIds.Contains(rolePermission.RoleId) && rolePermission.PermissionId == overridePermission.PermissionId)
                    .ToListAsync();

                context.RolePermissions.RemoveRange(rolePermissions);
                await context.SaveChangesAsync();
            }

            var restaurant = new Restaurant
            {
                Name = "OCR Test Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTOCR01");
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
                FullName = "OCR Admin",
                MobileNumber = branchless ? "90002000" : "90002001",
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

        public async Task<SeedResult> SeedForeignRestaurantAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = new Restaurant
            {
                Name = "Foreign OCR Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTOCR02");

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

        public async Task<Vendor> InsertVendorAsync(Guid restaurantId, Guid branchId, string name, VendorType vendorType, bool isActive)
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
                IsActive = isActive,
                CreatedAtUtc = now
            };

            context.Vendors.Add(vendor);
            await context.SaveChangesAsync();
            return vendor;
        }

        public async Task<VendorBill> InsertVendorBillAsync(
            Guid restaurantId,
            Guid branchId,
            Guid vendorId,
            string? billNumber,
            DateTime billDate,
            decimal totalAmount)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var bill = new VendorBill
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                VendorId = vendorId,
                BillNumber = billNumber,
                BillDate = DateTime.SpecifyKind(billDate.Date, DateTimeKind.Utc),
                Status = VendorBillStatus.Unpaid,
                TotalAmount = totalAmount,
                PaidAmount = 0m,
                BalanceAmount = totalAmount,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            context.VendorBills.Add(bill);
            await context.SaveChangesAsync();
            return bill;
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

        public async Task<int> CountVendorBillsAsync(Guid restaurantId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            return await context.VendorBills.CountAsync(entity => entity.RestaurantId == restaurantId);
        }

        public async Task<HttpResponseMessage> UploadDraftAsync(Guid? branchId, string fileName, string contentType, string contentText)
        {
            using var content = new MultipartFormDataContent();
            if (branchId.HasValue)
            {
                content.Add(new StringContent(branchId.Value.ToString()), "branchId");
            }

            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(contentText));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);

            return await Client.PostAsync("/api/v1/vendor-bill-ocr/drafts", content);
        }

        public async Task<HttpResponseMessage> UpdateDraftAsync(Guid draftId, object request)
        {
            return await Client.PutAsJsonAsync($"/api/v1/vendor-bill-ocr/drafts/{draftId}", request);
        }

        public async Task<HttpResponseMessage> ConfirmDraftAsync(Guid draftId)
        {
            return await Client.PostAsync($"/api/v1/vendor-bill-ocr/drafts/{draftId}/confirm", null);
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
                    ["Jwt:RefreshTokenLifetimeDays"] = "7",
                    ["Ocr:Provider"] = "Fake",
                    ["Ocr:MaxUploadBytes"] = _maxUploadBytes.ToString(CultureInfo.InvariantCulture),
                    ["Ocr:StorageRootPath"] = Path.Combine(Path.GetTempPath(), "billsoft-ocr-tests")
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BillSoftDbContext>>();
                services.RemoveAll<BillSoftDbContext>();
                services.RemoveAll<IOptions<OcrOptions>>();
                services.RemoveAll<IVendorBillOcrProvider>();
                services.AddSingleton<IOptions<OcrOptions>>(
                    Options.Create(new OcrOptions
                    {
                        Provider = OcrProviderNames.Fake,
                        MaxUploadBytes = _maxUploadBytes,
                        StorageRootPath = Path.Combine(Path.GetTempPath(), "billsoft-ocr-tests")
                    }));
                services.AddScoped<IVendorBillOcrProvider, FakeVendorBillOcrProvider>();
                if (_useThrowingProvider)
                {
                    services.RemoveAll<IVendorBillOcrProvider>();
                    services.AddScoped<IVendorBillOcrProvider, ThrowingVendorBillOcrProvider>();
                }
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

    private sealed record VendorBillOcrDraftListResponseDto(VendorBillOcrDraftListItemDto[] Items);

    private sealed record VendorBillOcrDraftListItemDto(
        Guid VendorBillOcrDraftId,
        Guid RestaurantId,
        Guid BranchId,
        string OriginalFileName,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record VendorBillOcrDraftDetailDto(
        Guid VendorBillOcrDraftId,
        Guid RestaurantId,
        Guid BranchId,
        Guid UploadedByUserId,
        string OriginalFileName,
        string ContentType,
        long FileSizeBytes,
        string Status,
        string? ExtractedVendorName,
        string? ExtractedBillNumber,
        DateTime? ExtractedBillDate,
        decimal? ExtractedTotalAmount,
        decimal? ExtractedConfidenceScore,
        string[] ProviderWarnings,
        bool HasDuplicateReceipt,
        string? DuplicateReceiptWarning,
        bool CanOverrideDuplicateReceipt,
        Guid? ReviewedVendorId,
        string? ReviewedBillNumber,
        DateTime? ReviewedBillDate,
        decimal? ReviewedTotalAmount,
        string? SafeErrorMessage,
        Guid? ConfirmedVendorBillId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset? ConfirmedAtUtc,
        VendorBillOcrDraftLineDetailDto[] Lines);

    private sealed record VendorBillOcrDraftLineDetailDto(
        Guid VendorBillOcrDraftLineId,
        int LineNumber,
        string ExtractedDescription,
        decimal? ExtractedQuantity,
        decimal? ExtractedUnitCost,
        decimal? ExtractedLineTotal,
        decimal? ConfidenceScore,
        Guid? SelectedInventoryItemId,
        bool IsIgnored,
        string? ReviewedDescription,
        decimal? ReviewedQuantity,
        decimal? ReviewedUnitCost,
        decimal? ReviewedLineTotal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

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
        string? CancellationReason);

    private sealed class ThrowingVendorBillOcrProvider : IVendorBillOcrProvider
    {
        public Task<VendorBillOcrProviderResult> ExtractAsync(VendorBillOcrProviderRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("RAW-AZURE-123");
        }
    }
}

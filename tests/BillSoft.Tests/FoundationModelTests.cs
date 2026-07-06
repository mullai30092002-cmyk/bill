using System.Reflection;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Common;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class FoundationModelTests
{
    private static BillSoftDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BillSoftDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BillSoftDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Foundation_Entities_Should_Initialize_NonEmpty_Ids_And_Timestamps()
    {
        var restaurant = new Restaurant();
        var branch = new Branch();
        var user = new User();
        var role = new Role();
        var permission = new Permission();
        var userRole = new UserRole();
        var rolePermission = new RolePermission();
        var auditLog = new AuditLog();
        var inventoryItem = new InventoryItem();
        var inventoryMovement = new InventoryMovement();
        var recipeIngredient = new MenuItemRecipeIngredient();
        var kitchenTicketDeduction = new KitchenTicketInventoryDeduction();

        Assert.NotEqual(Guid.Empty, restaurant.RestaurantId);
        Assert.NotEqual(default, restaurant.CreatedAt);

        Assert.NotEqual(Guid.Empty, branch.BranchId);
        Assert.NotEqual(default, branch.CreatedAt);

        Assert.NotEqual(Guid.Empty, user.UserId);
        Assert.NotEqual(default, user.CreatedAt);

        Assert.NotEqual(Guid.Empty, role.RoleId);
        Assert.NotEqual(default, role.CreatedAt);

        Assert.NotEqual(Guid.Empty, permission.PermissionId);

        Assert.NotEqual(Guid.Empty, userRole.UserRoleId);
        Assert.NotEqual(default, userRole.AssignedAt);

        Assert.NotEqual(Guid.Empty, rolePermission.RolePermissionId);
        Assert.NotEqual(default, rolePermission.CreatedAt);

        Assert.NotEqual(Guid.Empty, auditLog.AuditLogId);
        Assert.NotEqual(default, auditLog.CreatedAt);

        Assert.NotEqual(Guid.Empty, inventoryItem.InventoryItemId);
        Assert.NotEqual(default, inventoryItem.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, inventoryMovement.InventoryMovementId);
        Assert.NotEqual(default, inventoryMovement.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, recipeIngredient.MenuItemRecipeIngredientId);
        Assert.NotEqual(default, recipeIngredient.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, kitchenTicketDeduction.KitchenTicketInventoryDeductionId);
        Assert.NotEqual(default, kitchenTicketDeduction.CreatedAtUtc);
    }

    [Fact]
    public void Restaurant_Should_Normalize_Code()
    {
        var restaurant = new Restaurant();

        var setCode = typeof(Restaurant).GetMethod("SetRestaurantCode", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(setCode);

        setCode!.Invoke(restaurant, new object?[] { "  ab-123  " });

        var restaurantCode = typeof(Restaurant).GetProperty("RestaurantCode")!.GetValue(restaurant);
        var normalizedRestaurantCode = typeof(Restaurant).GetProperty("NormalizedRestaurantCode")!.GetValue(restaurant);

        Assert.Equal("ab-123", restaurantCode);
        Assert.Equal("AB-123", normalizedRestaurantCode);
    }

    [Fact]
    public void User_Should_Normalize_Email()
    {
        var user = new User();

        var setEmail = typeof(User).GetMethod("SetEmail", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(setEmail);

        setEmail!.Invoke(user, new object?[] { "  user@example.com  " });

        var email = typeof(User).GetProperty(nameof(User.Email))!.GetValue(user);
        var normalizedEmail = typeof(User).GetProperty("NormalizedEmail")!.GetValue(user);

        Assert.Equal("user@example.com", email);
        Assert.Equal("USER@EXAMPLE.COM", normalizedEmail);
    }

    [Fact]
    public void RestaurantCode_Should_Have_A_Unique_Index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Restaurant));
        Assert.NotNull(entityType);

        var index = entityType!.GetIndexes()
            .SingleOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual(["NormalizedRestaurantCode"], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void User_Email_Unique_Index_Should_Be_Per_Restaurant_And_Filtered()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);

        var index = entityType!.GetIndexes()
            .SingleOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(User.RestaurantId), "NormalizedEmail"], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("[NormalizedEmail] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void AuditLog_Should_Expose_Snapshot_Fields_With_Expected_Lengths()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(entityType);

        var restaurantNameSnapshot = entityType!.FindProperty("RestaurantNameSnapshot");
        var branchNameSnapshot = entityType.FindProperty("BranchNameSnapshot");
        var userNameSnapshot = entityType.FindProperty("UserNameSnapshot");
        var userMobileSnapshot = entityType.FindProperty("UserMobileSnapshot");

        Assert.NotNull(restaurantNameSnapshot);
        Assert.NotNull(branchNameSnapshot);
        Assert.NotNull(userNameSnapshot);
        Assert.NotNull(userMobileSnapshot);

        Assert.Equal(160, restaurantNameSnapshot!.GetMaxLength());
        Assert.Equal(160, branchNameSnapshot!.GetMaxLength());
        Assert.Equal(160, userNameSnapshot!.GetMaxLength());
        Assert.Equal(32, userMobileSnapshot!.GetMaxLength());
    }

    [Fact]
    public void SystemPermissions_Should_Have_Unique_Deterministic_Ids_And_Codes()
    {
        var permissionIdProperty = typeof(PermissionDefinition).GetProperty("PermissionId");

        Assert.NotNull(permissionIdProperty);

        var permissionIds = SystemPermissions.Definitions
            .Select(definition => (Guid)permissionIdProperty!.GetValue(definition)!)
            .ToArray();

        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Equal(permissionIds.Length, permissionIds.Distinct().Count());
        Assert.DoesNotContain(permissionIds, id => id == Guid.Empty);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SystemRoles_Should_Have_Unique_Deterministic_Ids_And_Names()
    {
        var roleIdProperty = typeof(RoleDefinition).GetProperty("RoleId");

        Assert.NotNull(roleIdProperty);

        var roleIds = SystemRoles.Definitions
            .Select(definition => (Guid)roleIdProperty!.GetValue(definition)!)
            .ToArray();

        var names = SystemRoles.Definitions.Select(definition => definition.Name).ToArray();

        Assert.Equal(roleIds.Length, roleIds.Distinct().Count());
        Assert.DoesNotContain(roleIds, id => id == Guid.Empty);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Seed_Catalog_Should_Not_Contain_Duplicate_RolePermission_Pairs()
    {
        var pairs = FoundationSeedData.RolePermissions
            .Select(item => (item.RoleName, item.PermissionCode))
            .ToArray();

        var ids = FoundationSeedData.RolePermissions.Select(item => item.RolePermissionId).ToArray();

        Assert.Equal(pairs.Length, pairs.Distinct().Count());
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.DoesNotContain(ids, id => id == Guid.Empty);
    }

    [Fact]
    public void RolePermission_Seed_Id_Should_Be_Deterministic_For_A_Pair()
    {
        var first = FoundationSeedData.RolePermissions
            .Single(item => item.RoleName == "SuperAdmin" && item.PermissionCode == "Bill.Create");

        var expected = DeterministicGuid.FromString("RolePermission:SUPERADMIN:BILL.CREATE");

        Assert.Equal(expected, first.RolePermissionId);

        var second = DeterministicGuid.FromString("RolePermission:SUPERADMIN:BILL.CREATE");

        Assert.Equal(first.RolePermissionId, second);
    }

    [Fact]
    public void InventoryItem_Should_Have_A_Unique_Index_Per_Branch_And_Normalized_Name()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(InventoryItem));
        Assert.NotNull(entityType);

        var index = entityType!.GetIndexes()
            .SingleOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InventoryItem.RestaurantId), nameof(InventoryItem.BranchId), nameof(InventoryItem.NormalizedName)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void Recipe_And_Deduction_Entities_Should_Be_Tracked_By_DbContext()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.MenuItemRecipeIngredients);
        Assert.NotNull(context.KitchenTicketInventoryDeductions);

        var recipeType = context.Model.FindEntityType(typeof(MenuItemRecipeIngredient));
        var deductionType = context.Model.FindEntityType(typeof(KitchenTicketInventoryDeduction));

        Assert.NotNull(recipeType);
        Assert.NotNull(deductionType);

        Assert.Contains(recipeType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemRecipeIngredient.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(recipeType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemRecipeIngredient.BranchId)], StringComparer.Ordinal));

        Assert.Contains(recipeType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemRecipeIngredient.MenuItemId)], StringComparer.Ordinal));

        Assert.Contains(recipeType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemRecipeIngredient.InventoryItemId)], StringComparer.Ordinal));

        var recipeIndex = recipeType.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(MenuItemRecipeIngredient.RestaurantId),
                        nameof(MenuItemRecipeIngredient.BranchId),
                        nameof(MenuItemRecipeIngredient.MenuItemId),
                        nameof(MenuItemRecipeIngredient.InventoryItemId)
                    ], StringComparer.Ordinal));

        Assert.NotNull(recipeIndex);
        Assert.True(recipeIndex!.IsUnique);

        Assert.Contains(deductionType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketInventoryDeduction.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(deductionType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketInventoryDeduction.BranchId)], StringComparer.Ordinal));

        Assert.Contains(deductionType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketInventoryDeduction.KitchenTicketId)], StringComparer.Ordinal));

        Assert.Contains(deductionType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketInventoryDeduction.InventoryItemId)], StringComparer.Ordinal));

        Assert.Contains(deductionType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketInventoryDeduction.InventoryMovementId)], StringComparer.Ordinal));

        var deductionIndex = deductionType.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(KitchenTicketInventoryDeduction.RestaurantId),
                        nameof(KitchenTicketInventoryDeduction.KitchenTicketId),
                        nameof(KitchenTicketInventoryDeduction.InventoryItemId)
                    ], StringComparer.Ordinal));

        Assert.NotNull(deductionIndex);
        Assert.True(deductionIndex!.IsUnique);
    }

    [Fact]
    public void InventoryMovementType_Should_Include_Consumption_For_Kitchen_Deduction()
    {
        Assert.Contains(nameof(InventoryMovementType.Consumption), Enum.GetNames<InventoryMovementType>());
    }
}

using BillSoft.Domain.Menu;
using BillSoft.Domain.Localization;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Seed;

public sealed class DemoLoginSeedService : IDemoLoginSeedService
{
    private readonly BillSoftDbContext _context;
    private readonly IFoundationSeedService _foundationSeedService;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DemoLoginSeedService(
        BillSoftDbContext context,
        IFoundationSeedService foundationSeedService,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _foundationSeedService = foundationSeedService ?? throw new ArgumentNullException(nameof(foundationSeedService));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<DemoLoginSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;

        await _foundationSeedService.SeedAsync(cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var (restaurant, restaurantCreated) = await GetOrCreateRestaurantAsync(cancellationToken);

        var (branch, branchCreated) = await GetOrCreateBranchAsync(restaurant, cancellationToken);

        var (user, userCreated) = await GetOrCreateUserAsync(
            restaurant,
            branch,
            DemoLoginSeedData.FullName,
            DemoLoginSeedData.Email,
            DemoLoginSeedData.MobileCountryCode,
            DemoLoginSeedData.MobileNumber,
            DemoLoginSeedData.MobileE164,
            DemoLoginSeedData.Password,
            cancellationToken);

        var roleAssignmentCreated = await EnsureRoleAssignmentAsync(user, DemoLoginSeedData.RoleName, cancellationToken);

        var (inventoryUser, _) = await GetOrCreateUserAsync(
            restaurant,
            branch,
            DemoLoginSeedData.InventoryQaFullName,
            DemoLoginSeedData.InventoryQaEmail,
            DemoLoginSeedData.InventoryQaMobileCountryCode,
            DemoLoginSeedData.InventoryQaMobileNumber,
            DemoLoginSeedData.InventoryQaMobileE164,
            DemoLoginSeedData.InventoryQaPassword,
            cancellationToken);

        await EnsureRoleAssignmentAsync(inventoryUser, DemoLoginSeedData.InventoryQaRoleName, cancellationToken);

        await SeedMenuCatalogAsync(restaurant, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DemoLoginSeedResult(
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            RestaurantCode: DemoLoginSeedData.RestaurantCode,
            RestaurantCreated: restaurantCreated,
            BranchCreated: branchCreated,
            UserCreated: userCreated,
            RoleAssignmentCreated: roleAssignmentCreated);
    }

    private async Task SeedMenuCatalogAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        var categoryLookup = new Dictionary<string, MenuCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var categorySeed in DemoLoginSeedData.MenuCategories.OrderBy(category => category.DisplayOrder).ThenBy(category => category.Name))
        {
            var category = await GetOrCreateMenuCategoryAsync(restaurant.RestaurantId, categorySeed, cancellationToken);
            categoryLookup[categorySeed.Name] = category;
        }

        foreach (var itemSeed in DemoLoginSeedData.MenuItems)
        {
            if (!categoryLookup.TryGetValue(itemSeed.CategoryName, out var category))
            {
                throw new InvalidOperationException($"Demo menu category '{itemSeed.CategoryName}' is missing.");
            }

            await GetOrCreateMenuItemAsync(restaurant.RestaurantId, category.MenuCategoryId, itemSeed, cancellationToken);
        }
    }

    private async Task<(Restaurant Restaurant, bool Created)> GetOrCreateRestaurantAsync(CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeRestaurantCode(DemoLoginSeedData.RestaurantCode);
        var restaurant = await _context.Restaurants
            .SingleOrDefaultAsync(entity => entity.NormalizedRestaurantCode == normalizedCode, cancellationToken);

        if (restaurant is not null)
        {
            restaurant.SetRestaurantCode(DemoLoginSeedData.RestaurantCode);
            restaurant.Name = DemoLoginSeedData.RestaurantName;
            restaurant.SetCountryProfile(DemoLoginSeedData.RestaurantCountryCode);
            restaurant.Phone = null;
            restaurant.Email = null;
            restaurant.Address = null;
            restaurant.Status = RestaurantStatus.Active;
            restaurant.MarkUpdated();
            return (restaurant, false);
        }

        restaurant = new Restaurant
        {
            Name = DemoLoginSeedData.RestaurantName,
            Phone = null,
            Email = null,
            Address = null,
            CountryCode = DemoLoginSeedData.RestaurantCountryCode,
            CurrencyCode = DemoLoginSeedData.RestaurantCurrencyCode,
            TimeZoneId = DemoLoginSeedData.RestaurantTimeZoneId,
            Status = RestaurantStatus.Active
        };
        restaurant.SetRestaurantCode(DemoLoginSeedData.RestaurantCode);
        await _context.Restaurants.AddAsync(restaurant, cancellationToken);

        return (restaurant, true);
    }

    private async Task<MenuCategory> GetOrCreateMenuCategoryAsync(
        Guid restaurantId,
        DemoLoginSeedData.DemoMenuCategorySeed categorySeed,
        CancellationToken cancellationToken)
    {
        var category = await _context.MenuCategories
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.Name == categorySeed.Name,
                cancellationToken);

        if (category is not null)
        {
            category.UpdateProfile(categorySeed.Name, categorySeed.DisplayOrder);
            if (category.Status != MenuCategoryStatus.Active)
            {
                category.Activate();
            }

            return category;
        }

        category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = categorySeed.Name,
            DisplayOrder = categorySeed.DisplayOrder,
            Status = MenuCategoryStatus.Active
        };

        await _context.MenuCategories.AddAsync(category, cancellationToken);
        return category;
    }

    private async Task<MenuItem> GetOrCreateMenuItemAsync(
        Guid restaurantId,
        Guid categoryId,
        DemoLoginSeedData.DemoMenuItemSeed itemSeed,
        CancellationToken cancellationToken)
    {
        var item = await _context.MenuItems
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.Sku == itemSeed.Sku,
                cancellationToken);

        if (item is not null)
        {
            item.UpdateProfile(
                categoryId,
                itemSeed.Name,
                itemSeed.Description,
                itemSeed.Sku,
                itemSeed.BasePrice,
                itemSeed.TaxRate,
                itemSeed.IsVegetarian,
                itemSeed.IsAvailableForEatIn,
                itemSeed.IsAvailableForParcel,
                MenuItemInventoryDeductionMode.RecipeOnServe,
                DateTimeOffset.UtcNow);

            if (item.Status != MenuItemStatus.Active)
            {
                item.Activate();
            }

            return item;
        }

        item = new MenuItem
        {
            RestaurantId = restaurantId,
            MenuCategoryId = categoryId,
            Name = itemSeed.Name,
            Description = itemSeed.Description,
            Sku = itemSeed.Sku,
            BasePrice = itemSeed.BasePrice,
            TaxRate = itemSeed.TaxRate,
            IsVegetarian = itemSeed.IsVegetarian,
            IsAvailableForEatIn = itemSeed.IsAvailableForEatIn,
            IsAvailableForParcel = itemSeed.IsAvailableForParcel,
            Status = MenuItemStatus.Active
        };

        await _context.MenuItems.AddAsync(item, cancellationToken);
        return item;
    }

    private async Task<(Branch Branch, bool Created)> GetOrCreateBranchAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurant.RestaurantId &&
                entity.Name == DemoLoginSeedData.BranchName,
                cancellationToken);

        if (branch is not null)
        {
            branch.CountryCode = DemoLoginSeedData.BranchCountryCode;
            branch.UpdateProfile(
                DemoLoginSeedData.BranchName,
                branch.Address,
                branch.Phone,
                DemoLoginSeedData.BranchTimezone,
                DemoLoginSeedData.BranchCurrency);

            if (branch.Status != BranchStatus.Active)
            {
                branch.Activate();
            }

            return (branch, false);
        }

        branch = new Branch
        {
            RestaurantId = restaurant.RestaurantId,
            Name = DemoLoginSeedData.BranchName,
            CountryCode = DemoLoginSeedData.BranchCountryCode,
            TimeZoneId = DemoLoginSeedData.BranchTimezone,
            CurrencyCode = DemoLoginSeedData.BranchCurrency,
            Status = BranchStatus.Active
        };
        await _context.Branches.AddAsync(branch, cancellationToken);

        return (branch, true);
    }

    private async Task<(User User, bool Created)> GetOrCreateUserAsync(
        Restaurant restaurant,
        Branch branch,
        string fullName,
        string email,
        string mobileCountryCode,
        string mobileNumber,
        string mobileE164,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var userByEmail = await _context.Users
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurant.RestaurantId &&
                entity.NormalizedEmail == normalizedEmail,
                cancellationToken);

        var userByMobile = await _context.Users
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurant.RestaurantId &&
                entity.MobileE164 == mobileE164,
                cancellationToken);

        if (userByEmail is not null && userByMobile is not null && userByEmail.UserId != userByMobile.UserId)
        {
            throw new InvalidOperationException(
                "Demo login seed found separate users for the canonical demo email and mobile number. " +
                "Resolve the duplicate restaurant user records before reseeding.");
        }

        var user = userByEmail ?? userByMobile;

        if (user is not null)
        {
            var normalizedMobileNumber = MobileNumberNormalizer.Normalize(
                mobileCountryCode,
                mobileNumber);

            user.BranchId = branch.BranchId;
            user.FullName = fullName;
            user.SetMobileNumber(normalizedMobileNumber);
            user.SetEmail(email);
            user.Status = UserStatus.Active;

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
            }

            user.MarkUpdated();
            return (user, false);
        }

        user = new User
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            FullName = fullName,
            Status = UserStatus.Active
        };
        user.SetMobileNumber(MobileNumberNormalizer.Normalize(
            mobileCountryCode,
            mobileNumber));
        user.SetEmail(email);
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        await _context.Users.AddAsync(user, cancellationToken);
        return (user, true);
    }

    private async Task<bool> EnsureRoleAssignmentAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.SingleAsync(
            entity => entity.RestaurantId == null && entity.Name == roleName,
            cancellationToken);

        var existingAssignment = await _context.UserRoles.SingleOrDefaultAsync(
            entity => entity.UserId == user.UserId && entity.RoleId == role.RoleId,
            cancellationToken);

        if (existingAssignment is not null)
        {
            return false;
        }

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return true;
    }

    private static string NormalizeRestaurantCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}

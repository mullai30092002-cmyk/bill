using System.Globalization;
using System.Text;
using BillSoft.Application.Auth;
using BillSoft.Application.Menu;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace BillSoft.Infrastructure.Menu;

public sealed class MenuImportAdminService : IMenuImportAdminService
{
    private const string ImportAuditReason = "Menu import confirmed from CSV.";
    private const string CategoryImportReason = "Menu category imported from CSV.";
    private const string ItemImportReason = "Menu item imported from CSV.";
    private const string ItemUpdateReason = "Menu item updated from CSV import.";
    private const string PriceHistoryReason = "Price updated from CSV import.";

    private readonly BillSoftDbContext _context;

    public MenuImportAdminService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MenuImportResponse> PreviewAsync(AuthUserContext currentUser, MenuImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        _ = await LoadRestaurantNameAsync(restaurantId, cancellationToken);
        var parsed = await ParseAsync(restaurantId, request, cancellationToken);
        return BuildResponse(parsed, importedRows: 0, updatedRows: 0, skippedRows: 0, failedRows: 0);
    }

    public async Task<MenuImportResponse> ConfirmAsync(AuthUserContext currentUser, MenuImportConfirmRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = MenuServiceSupport.RequireRestaurantScope(currentUser);
        var restaurantName = await LoadRestaurantNameAsync(restaurantId, cancellationToken);
        var parsed = await ParseAsync(restaurantId, new MenuImportPreviewRequest(request.CsvText, request.ImportName), cancellationToken);

        var blockingErrors = parsed.Rows.Where(row => row.Errors.Count > 0).ToArray();
        if (blockingErrors.Length > 0)
        {
            throw new InvalidOperationException(BuildErrorMessage(blockingErrors));
        }

        var decisionLookup = new Dictionary<int, string>();
        foreach (var decision in request.Decisions ?? Array.Empty<MenuImportRowDecision>())
        {
            if (decision.RowNumber <= 0 || string.IsNullOrWhiteSpace(decision.Action))
            {
                continue;
            }

            decisionLookup[decision.RowNumber] = decision.Action.Trim();
        }

        var duplicateRows = parsed.Rows.Where(row => row.IsDuplicate).ToArray();
        foreach (var row in duplicateRows)
        {
            if (!decisionLookup.ContainsKey(row.RowNumber))
            {
                throw new InvalidOperationException($"Row {row.RowNumber} is a duplicate and requires an explicit Skip or Update decision.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var resultRows = new List<ParsedMenuImportRow>(parsed.Rows.Count);
        var importedRows = 0;
        var updatedRows = 0;
        var skippedRows = 0;
        var failedRows = 0;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var categoryCache = await LoadCategoryCacheAsync(restaurantId, cancellationToken);
            var itemCache = await LoadItemCacheAsync(restaurantId, cancellationToken);
            var nextCategoryDisplayOrder = categoryCache.Count == 0
                ? 1
                : categoryCache.Values.Max(category => category.DisplayOrder) + 1;

            foreach (var row in parsed.Rows.OrderBy(row => row.RowNumber))
            {
                if (row.Errors.Count > 0)
                {
                    failedRows++;
                    resultRows.Add(row with { Status = "Failed", Message = JoinMessages(row.Errors, row.Warnings) });
                    continue;
                }

                var decision = ResolveDecision(row, decisionLookup);
                if (decision == "Skip")
                {
                    skippedRows++;
                    resultRows.Add(row with { Status = "Skipped", Message = "Skipped by user choice." });
                    continue;
                }

                if (row.IsDuplicate && decision != "Update")
                {
                    throw new InvalidOperationException($"Row {row.RowNumber} is a duplicate and must be marked Skip or Update.");
                }

                var category = await GetOrCreateCategoryAsync(
                    restaurantId,
                    restaurantName,
                    currentUser,
                    row.Category,
                    categoryCache,
                    ref nextCategoryDisplayOrder,
                    now);

                if (category.Status == MenuCategoryStatus.Inactive)
                {
                    throw new InvalidOperationException($"Row {row.RowNumber} targets inactive category '{category.Name}'. Activate the category before importing items.");
                }

                var normalizedKey = BuildItemKey(category.Name, row.ItemName);
                if (decision == "Update")
                {
                    if (!itemCache.TryGetValue(normalizedKey, out var existingItem))
                    {
                        throw new InvalidOperationException($"Row {row.RowNumber} was marked Update, but the menu item no longer exists.");
                    }

                    var before = ToItemSnapshot(existingItem, category.Name);
                    var priceChanged = row.EatInPrice.HasValue && existingItem.BasePrice != row.EatInPrice.Value;

                    existingItem.MenuCategoryId = category.MenuCategoryId;
                    existingItem.Name = row.ItemName;
                    if (row.Description is not null)
                    {
                        existingItem.Description = row.Description;
                    }

                    if (row.EatInPrice.HasValue)
                    {
                        existingItem.BasePrice = row.EatInPrice.Value;
                    }

                    if (row.Available.HasValue)
                    {
                        if (row.Available.Value)
                        {
                            existingItem.IsAvailableForEatIn = true;
                            existingItem.IsAvailableForParcel = true;
                            existingItem.Status = MenuItemStatus.Active;
                        }
                        else
                        {
                            existingItem.Status = MenuItemStatus.Inactive;
                        }
                    }

                    existingItem.MarkUpdated(now);
                    var after = ToItemSnapshot(existingItem, category.Name);

                    AddItemAudit(
                        currentUser,
                        restaurantId,
                        restaurantName,
                        null,
                        "MenuItem.Updated",
                        ItemUpdateReason,
                        existingItem.MenuItemId,
                        before,
                        after,
                        now);

                    if (priceChanged)
                    {
                        _context.MenuItemPriceHistory.Add(new MenuItemPriceHistory
                        {
                            MenuItemId = existingItem.MenuItemId,
                            RestaurantId = restaurantId,
                            OldPrice = before.BasePrice,
                            NewPrice = after.BasePrice,
                            ChangedByUserId = MenuServiceSupport.ResolveActorUserId(currentUser),
                            ChangedAt = now,
                            Reason = PriceHistoryReason
                        });

                        AddItemAudit(
                            currentUser,
                            restaurantId,
                            restaurantName,
                            null,
                            "MenuItem.PriceChanged",
                            PriceHistoryReason,
                        existingItem.MenuItemId,
                            new { BasePrice = before.BasePrice },
                            new { BasePrice = after.BasePrice },
                            now);
                    }

                    updatedRows++;
                    resultRows.Add(row with { Status = "Updated", Message = "Updated existing menu item." });
                    continue;
                }

                if (itemCache.ContainsKey(normalizedKey))
                {
                    throw new InvalidOperationException($"Row {row.RowNumber} duplicates an existing menu item. Mark it Update or Skip.");
                }

                var item = new MenuItem
                {
                    RestaurantId = restaurantId,
                    MenuCategoryId = category.MenuCategoryId,
                    Name = row.ItemName,
                    Description = row.Description,
                    BasePrice = row.EatInPrice ?? 0m,
                    TaxRate = 0m,
                    IsVegetarian = false,
                    IsAvailableForEatIn = true,
                    IsAvailableForParcel = true,
                    Status = row.Available.HasValue && !row.Available.Value ? MenuItemStatus.Inactive : MenuItemStatus.Active
                };

                if (row.Available.HasValue && row.Available.Value)
                {
                    item.IsAvailableForEatIn = true;
                    item.IsAvailableForParcel = true;
                }

                _context.MenuItems.Add(item);
                itemCache[normalizedKey] = item;

                AddItemAudit(
                    currentUser,
                    restaurantId,
                    restaurantName,
                    null,
                    "MenuItem.Created",
                    ItemImportReason,
                    item.MenuItemId,
                    null,
                    ToItemSnapshot(item, category.Name),
                    now);

                importedRows++;
                resultRows.Add(row with { Status = "Imported", Message = "Imported new menu item." });
            }

            var summary = new
            {
                ImportName = parsed.ImportName,
                Summary = new
                {
                    TotalRows = parsed.Rows.Count,
                    ReadyRows = parsed.Rows.Count(row => row.Errors.Count == 0 && !row.IsDuplicate),
                    DuplicateRows = parsed.Rows.Count(row => row.IsDuplicate),
                    InvalidRows = parsed.Rows.Count(row => row.Errors.Count > 0),
                    ImportedRows = importedRows,
                    UpdatedRows = updatedRows,
                    SkippedRows = skippedRows,
                    FailedRows = failedRows
                }
            };

            _context.AuditLogs.Add(new AuditLog
            {
                RestaurantId = restaurantId,
                BranchId = null,
                UserId = MenuServiceSupport.ResolveActorUserId(currentUser),
                Action = "MenuImport.Confirmed",
                EntityType = "MenuImport",
                EntityId = Guid.NewGuid().ToString("N"),
                Reason = ImportAuditReason,
                OldValueJson = null,
                NewValueJson = MenuServiceSupport.Serialize(summary),
                RestaurantNameSnapshot = restaurantName,
                UserNameSnapshot = currentUser.FullName,
                UserMobileSnapshot = currentUser.MobileNumber,
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new MenuImportResponse(
                parsed.ImportName,
                new MenuImportSummary(parsed.Rows.Count, parsed.Rows.Count(row => row.Errors.Count == 0 && !row.IsDuplicate), parsed.Rows.Count(row => row.IsDuplicate), parsed.Rows.Count(row => row.Errors.Count > 0), importedRows, updatedRows, skippedRows, failedRows),
                resultRows.Select(row => row.ToPreviewRow()).ToArray());
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ParsedMenuImportDocument> ParseAsync(
        Guid restaurantId,
        MenuImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CsvText))
        {
            throw new InvalidOperationException("CSV text is required.");
        }

        var categoryLookup = await LoadCategoryLookupAsync(restaurantId, cancellationToken);
        var itemLookup = await LoadItemLookupAsync(restaurantId, cancellationToken);
        var branchLookup = await LoadBranchLookupAsync(restaurantId, cancellationToken);

        var rows = ReadCsvRows(request.CsvText)
            .Select((fields, index) => new { fields, rowNumber = index + 2 })
            .ToArray();

        if (rows.Length < 2)
        {
            throw new InvalidOperationException("CSV must contain a header row and at least one data row.");
        }

        var headers = rows[0].fields;
        var headerIndex = BuildHeaderIndex(headers);
        ValidateRequiredHeaders(headerIndex);

        var parsedRows = new List<ParsedMenuImportRow>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in rows.Skip(1))
        {
            if (entry.fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var parsed = ParseRow(entry.rowNumber, headerIndex, entry.fields, categoryLookup, itemLookup, branchLookup);
            var key = BuildImportKey(parsed.Category, parsed.ItemName);
            if (!seenKeys.Add(key))
            {
                parsed.Errors.Add("Duplicate row in CSV import.");
            }

            parsedRows.Add(parsed);
        }

        if (parsedRows.Count == 0)
        {
            throw new InvalidOperationException("CSV must contain at least one non-empty data row.");
        }

        return new ParsedMenuImportDocument(request.ImportName, parsedRows);
    }

    private static void ValidateRequiredHeaders(IReadOnlyDictionary<string, int> headerIndex)
    {
        var requiredHeaders = new[] { "Category", "ItemName", "EatInPrice" };
        var missingHeaders = requiredHeaders.Where(header => !headerIndex.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new InvalidOperationException($"CSV is missing required columns: {string.Join(", ", missingHeaders)}.");
        }
    }

    private static IReadOnlyDictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index].Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (!map.ContainsKey(header))
            {
                map[header] = index;
            }
        }

        return map;
    }

    private static ParsedMenuImportRow ParseRow(
        int rowNumber,
        IReadOnlyDictionary<string, int> headerIndex,
        string[] fields,
        IReadOnlyDictionary<string, MenuCategory> categoryLookup,
        IReadOnlyDictionary<string, MenuItem> itemLookup,
        IReadOnlyDictionary<string, Branch> branchLookup)
    {
        var category = ReadText(fields, headerIndex, "Category");
        var itemName = ReadText(fields, headerIndex, "ItemName");
        var description = ReadOptionalText(fields, headerIndex, "Description");
        var branchName = ReadOptionalText(fields, headerIndex, "BranchName")
            ?? ReadOptionalText(fields, headerIndex, "BranchCode");
        var eatInPriceText = ReadText(fields, headerIndex, "EatInPrice");
        var availableText = ReadOptionalText(fields, headerIndex, "Available");

        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(category))
        {
            errors.Add("Category is required.");
        }

        if (string.IsNullOrWhiteSpace(itemName))
        {
            errors.Add("Item name is required.");
        }

        var eatInPrice = ParseDecimal(eatInPriceText, "EatInPrice", errors);
        var available = ParseNullableBoolean(availableText, "Available", errors);

        MenuCategory? existingCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            categoryLookup.TryGetValue(NormalizeKey(category), out existingCategory);
        }

        MenuItem? existingItem = null;
        if (existingCategory is not null && !string.IsNullOrWhiteSpace(itemName))
        {
            itemLookup.TryGetValue(BuildImportKey(existingCategory.Name, itemName), out existingItem);
        }

        Branch? branch = null;
        if (!string.IsNullOrWhiteSpace(branchName))
        {
            branchLookup.TryGetValue(NormalizeKey(branchName), out branch);
            if (branch is null)
            {
                errors.Add("BranchName must match an existing branch in the current restaurant.");
            }
        }

        if (existingCategory is not null && existingCategory.Status == MenuCategoryStatus.Inactive)
        {
            warnings.Add("The target category is inactive. Imported items will remain unusable until the category is activated.");
        }

        var isDuplicate = existingItem is not null;
        var status = errors.Count > 0
            ? "Invalid"
            : isDuplicate
                ? "Duplicate"
                : warnings.Count > 0
                    ? "Warning"
                : "Ready";

        var message = errors.Count > 0
            ? string.Join(" ", errors)
            : isDuplicate
                ? "Existing menu item found. Choose Skip or Update."
                : warnings.Count > 0
                    ? string.Join(" ", warnings)
                    : "Ready for import.";

        return new ParsedMenuImportRow(
            rowNumber,
            category?.Trim() ?? string.Empty,
            itemName?.Trim() ?? string.Empty,
            description,
            eatInPrice,
            available,
            branch?.Name,
            status,
            message,
            errors,
            warnings,
            isDuplicate,
            existingCategory?.Name,
            existingItem?.MenuItemId.ToString(),
            isDuplicate ? "Skip" : "Import",
            existingCategory,
            existingItem,
            branch);
    }

    private static decimal? ParseDecimal(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return null;
        }

        if (!decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            errors.Add($"{fieldName} must be numeric and greater than or equal to zero.");
            return null;
        }

        return parsed;
    }

    private static bool? ParseNullableBoolean(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        errors.Add($"{fieldName} must be Yes/No, TRUE/FALSE, 1/0, or blank.");
        return null;
    }

    private static string ReadText(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string headerName)
    {
        return ReadOptionalText(fields, headerIndex, headerName) ?? string.Empty;
    }

    private static string? ReadOptionalText(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string headerName)
    {
        if (!headerIndex.TryGetValue(headerName, out var index) || index >= fields.Length)
        {
            return null;
        }

        var value = fields[index]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeKey(string value) =>
        value.Trim().ToUpperInvariant();

    private static string BuildImportKey(string category, string itemName) =>
        $"{NormalizeKey(category)}|{NormalizeKey(itemName)}";

    private static string ResolveDecision(ParsedMenuImportRow row, IReadOnlyDictionary<int, string> decisions)
    {
        if (decisions.TryGetValue(row.RowNumber, out var decision))
        {
            var normalized = decision.Trim();
            if (string.Equals(normalized, "Import", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Update", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Skip", StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
            }

            throw new InvalidOperationException($"Row {row.RowNumber} decision must be Import, Update, or Skip.");
        }

        return row.IsDuplicate ? "Skip" : "Import";
    }

    private Task<MenuCategory> GetOrCreateCategoryAsync(
        Guid restaurantId,
        string restaurantName,
        AuthUserContext actor,
        string categoryName,
        IDictionary<string, MenuCategory> categoryCache,
        ref int nextDisplayOrder,
        DateTimeOffset now)
    {
        var key = NormalizeKey(categoryName);
        if (categoryCache.TryGetValue(key, out var category))
        {
            return Task.FromResult(category);
        }

        category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = categoryName,
            DisplayOrder = nextDisplayOrder++,
            Status = MenuCategoryStatus.Active
        };

        _context.MenuCategories.Add(category);
        categoryCache[key] = category;

        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurantId,
            BranchId = null,
            UserId = MenuServiceSupport.ResolveActorUserId(actor),
            Action = "MenuCategory.Created",
            EntityType = "MenuCategory",
            EntityId = category.MenuCategoryId.ToString(),
            Reason = CategoryImportReason,
            OldValueJson = null,
            NewValueJson = MenuServiceSupport.Serialize(new
            {
                category.MenuCategoryId,
                category.RestaurantId,
                category.Name,
                category.DisplayOrder,
                Status = category.Status.ToString()
            }),
            RestaurantNameSnapshot = restaurantName,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = now
        });

        return Task.FromResult(category);
    }

    private async Task<Dictionary<string, MenuCategory>> LoadCategoryLookupAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var categories = await _context.MenuCategories
            .AsNoTracking()
            .Where(category => category.RestaurantId == restaurantId)
            .ToListAsync(cancellationToken);

        return categories.ToDictionary(category => NormalizeKey(category.Name), category => category, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, MenuItem>> LoadItemLookupAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var items = await (
            from item in _context.MenuItems
            join category in _context.MenuCategories on item.MenuCategoryId equals category.MenuCategoryId
            where item.RestaurantId == restaurantId && category.RestaurantId == restaurantId
            select new { item, category })
            .ToListAsync(cancellationToken);

        return items.ToDictionary(
            entry => BuildImportKey(entry.category.Name, entry.item.Name),
            entry => entry.item,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Branch>> LoadBranchLookupAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.RestaurantId == restaurantId)
            .ToListAsync(cancellationToken);

        return branches.ToDictionary(branch => NormalizeKey(branch.Name), branch => branch, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, MenuCategory>> LoadCategoryCacheAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var categories = await _context.MenuCategories
            .ToListAsync(cancellationToken);

        return categories
            .Where(category => category.RestaurantId == restaurantId)
            .ToDictionary(category => NormalizeKey(category.Name), category => category, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, MenuItem>> LoadItemCacheAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var items = await (
            from item in _context.MenuItems
            join category in _context.MenuCategories on item.MenuCategoryId equals category.MenuCategoryId
            where item.RestaurantId == restaurantId && category.RestaurantId == restaurantId
            select new { item, category })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items.ToDictionary(
            entry => BuildImportKey(entry.category.Name, entry.item.Name),
            entry => entry.item,
            StringComparer.OrdinalIgnoreCase);
    }

    private static MenuImportResponse BuildResponse(ParsedMenuImportDocument parsed, int importedRows, int updatedRows, int skippedRows, int failedRows)
    {
        return new MenuImportResponse(
            parsed.ImportName,
            new MenuImportSummary(
                parsed.Rows.Count,
                parsed.Rows.Count(row => row.Errors.Count == 0 && !row.IsDuplicate),
                parsed.Rows.Count(row => row.IsDuplicate),
                parsed.Rows.Count(row => row.Errors.Count > 0),
                importedRows,
                updatedRows,
                skippedRows,
                failedRows),
            parsed.Rows
                .Select(row => row.ToPreviewRow())
                .ToArray());
    }

    private static string BuildErrorMessage(IEnumerable<ParsedMenuImportRow> rows)
    {
        var parts = rows
            .Select(row => $"Row {row.RowNumber}: {string.Join(" ", row.Errors)}")
            .ToArray();

        return string.Join(" ", parts);
    }

    private static string JoinMessages(IReadOnlyCollection<string> errors, IReadOnlyCollection<string> warnings)
    {
        var parts = new List<string>(errors.Count + warnings.Count);
        parts.AddRange(errors);
        parts.AddRange(warnings);
        return string.Join(" ", parts);
    }

    private async Task<string> LoadRestaurantNameAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        return await _context.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.RestaurantId == restaurantId)
            .Select(restaurant => restaurant.Name)
            .SingleAsync(cancellationToken);
    }

    private static IReadOnlyList<string[]> ReadCsvRows(string csvText)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvText));
            using var parser = new TextFieldParser(stream)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            var rows = new List<string[]>();
            while (!parser.EndOfData)
            {
                rows.Add(parser.ReadFields() ?? Array.Empty<string>());
            }

            return rows;
        }
        catch (MalformedLineException ex)
        {
            throw new InvalidOperationException($"CSV is malformed: {ex.Message}");
        }
    }

    private static string BuildItemKey(string categoryName, string itemName) =>
        BuildImportKey(categoryName, itemName);

    private static MenuItemSnapshot ToItemSnapshot(MenuItem item, string categoryName)
    {
        return new MenuItemSnapshot(
            item.MenuItemId,
            item.RestaurantId,
            item.MenuCategoryId,
            categoryName,
            item.Name,
            item.Description,
            item.Sku,
            item.BasePrice,
            item.TaxRate,
            item.IsVegetarian,
            item.IsAvailableForEatIn,
            item.IsAvailableForParcel,
            item.Status.ToString(),
            item.CreatedAt,
            item.UpdatedAt);
    }

    private void AddItemAudit(
        AuthUserContext actor,
        Guid restaurantId,
        string restaurantName,
        Branch? branch,
        string action,
        string reason,
        Guid itemId,
        object? oldValue,
        object? newValue,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurantId,
            BranchId = branch?.BranchId,
            UserId = MenuServiceSupport.ResolveActorUserId(actor),
            Action = action,
            EntityType = "MenuItem",
            EntityId = itemId.ToString(),
            Reason = reason,
            OldValueJson = MenuServiceSupport.Serialize(oldValue),
            NewValueJson = MenuServiceSupport.Serialize(newValue),
            RestaurantNameSnapshot = restaurantName,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private sealed record ParsedMenuImportDocument(
        string? ImportName,
        IReadOnlyCollection<ParsedMenuImportRow> Rows);

    private sealed record ParsedMenuImportRow(
        int RowNumber,
        string Category,
        string ItemName,
        string? Description,
        decimal? EatInPrice,
        bool? Available,
        string? BranchName,
        string Status,
        string Message,
        List<string> Errors,
        List<string> Warnings,
        bool IsDuplicate,
        string? ExistingCategoryName,
        string? ExistingMenuItemId,
        string SuggestedAction,
        MenuCategory? ExistingCategory,
        MenuItem? ExistingItem,
        Branch? Branch)
    {
        public MenuImportPreviewRow ToPreviewRow()
        {
            return new MenuImportPreviewRow(
                RowNumber,
                Category,
                ItemName,
                Description,
                EatInPrice,
                Available,
                BranchName,
                Status,
                Message,
                Errors.ToArray(),
                Warnings.ToArray(),
                IsDuplicate,
                ExistingCategoryName,
                ExistingMenuItemId,
                SuggestedAction);
        }
    }

    private sealed record MenuItemSnapshot(
        Guid MenuItemId,
        Guid RestaurantId,
        Guid MenuCategoryId,
        string CategoryName,
        string Name,
        string? Description,
        string? Sku,
        decimal BasePrice,
        decimal TaxRate,
        bool IsVegetarian,
        bool IsAvailableForEatIn,
        bool IsAvailableForParcel,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}

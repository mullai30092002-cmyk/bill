using BillSoft.Application.Auth;
using BillSoft.Application.Dashboard;
using BillSoft.Application.Billing;
using BillSoft.Application.Cashiering;
using BillSoft.Application.Inventory;
using BillSoft.Application.Kitchen;
using BillSoft.Application.Menu;
using BillSoft.Application.Orders;
using BillSoft.Application.Setup;
using BillSoft.Application.Reports;
using BillSoft.Application.Restaurants;
using BillSoft.Application.Security;
using BillSoft.Application.Users;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Auth;
using BillSoft.Infrastructure.Dashboard;
using BillSoft.Infrastructure.Billing;
using BillSoft.Infrastructure.Cashiering;
using BillSoft.Infrastructure.Inventory;
using BillSoft.Infrastructure.Kitchen;
using BillSoft.Infrastructure.Menu;
using BillSoft.Infrastructure.Orders;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Reports;
using BillSoft.Infrastructure.Restaurants;
using BillSoft.Infrastructure.Setup;
using BillSoft.Infrastructure.Seed;
using BillSoft.Infrastructure.Security;
using BillSoft.Infrastructure.Users;
using BillSoft.Infrastructure.Vendors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var databaseOptions = BuildDatabaseOptions(configuration);

        if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                "BillSoft database connection string is missing. Configure Database:ConnectionString in appsettings or environment variables.");
        }

        if (SqliteBootstrapper.IsSqliteProvider(databaseOptions.Provider)
            && !SqliteBootstrapper.IsLocalSqliteEnvironment(environment))
        {
            throw new InvalidOperationException(
                "SQLite database provider is only enabled for Development, Test, or Testing environments.");
        }

        services.AddSingleton(databaseOptions);

        services.AddDbContext<BillSoftDbContext>(options =>
        {
            ConfigureDatabase(options, databaseOptions);

            if (databaseOptions.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (databaseOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOwnerDashboardService, OwnerDashboardService>();
        services.AddScoped<ICashierShiftService, CashierShiftService>();
        services.AddScoped<ICashReconciliationReportService, CashReconciliationReportService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<InventoryLotAllocationService>();
        services.AddScoped<IKitchenTicketService, KitchenTicketService>();
        services.AddScoped<IDailyCashSalesReportService, DailyCashSalesReportService>();
        services.AddScoped<IPreparedStockReportService, PreparedStockReportService>();
        services.AddScoped<IExpiryStockReportService, ExpiryStockReportService>();
        services.AddScoped<IVendorPayablesReportService, VendorPayablesReportService>();
        services.AddScoped<ISetupChecklistService, SetupChecklistService>();
        services.AddScoped<IBranchAdminReadService, BranchAdminReadService>();
        services.AddScoped<IBranchAdminMutationService, BranchAdminMutationService>();
        services.AddScoped<IMenuCategoryAdminService, MenuCategoryAdminService>();
        services.AddScoped<IMenuImportAdminService, MenuImportAdminService>();
        services.AddScoped<IMenuItemAdminService, MenuItemAdminService>();
        services.AddScoped<IPosOrderService, PosOrderService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IVendorService, VendorService>();
        var ocrOptions = BuildOcrOptions(configuration);

        OcrOptionsValidator.Validate(ocrOptions);

        services.AddSingleton<IOptions<OcrOptions>>(Options.Create(ocrOptions));
        services.AddScoped<IUploadedDocumentStorage, LocalUploadedDocumentStorage>();

        if (string.Equals(ocrOptions.Provider, OcrProviderNames.AzureDocumentIntelligence, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IAzureDocumentIntelligenceAnalyzer, AzureDocumentIntelligenceAnalyzer>();
            services.AddScoped<IVendorBillOcrProvider, AzureDocumentIntelligenceVendorBillOcrProvider>();
        }
        else if (string.Equals(ocrOptions.Provider, OcrProviderNames.Fake, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IVendorBillOcrProvider, FakeVendorBillOcrProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported OCR provider '{ocrOptions.Provider}'. BillSoft currently supports Fake and AzureDocumentIntelligence.");
        }

        services.AddScoped<IVendorBillOcrService, VendorBillOcrService>();
        services.AddScoped<IRolePermissionReadService, RolePermissionReadService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFoundationSeedService, FoundationSeedService>();
        services.AddScoped<IDemoLoginSeedService, DemoLoginSeedService>();

        return services;
    }

    internal static DatabaseOptions BuildDatabaseOptions(IConfiguration configuration)
    {
        var provider = configuration[$"{DatabaseOptions.SectionName}:Provider"];
        var connectionString = configuration[$"{DatabaseOptions.SectionName}:ConnectionString"];
        var enableSensitiveDataLogging = ParseBoolean(
            configuration[$"{DatabaseOptions.SectionName}:EnableSensitiveDataLogging"],
            defaultValue: false);
        var enableDetailedErrors = ParseBoolean(
            configuration[$"{DatabaseOptions.SectionName}:EnableDetailedErrors"],
            defaultValue: true);

        return new DatabaseOptions
        {
            Provider = string.IsNullOrWhiteSpace(provider) ? "SqlServer" : provider,
            ConnectionString = connectionString ?? string.Empty,
            EnableSensitiveDataLogging = enableSensitiveDataLogging,
            EnableDetailedErrors = enableDetailedErrors
        };
    }

    internal static void ConfigureDatabase(DbContextOptionsBuilder options, DatabaseOptions databaseOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(databaseOptions);

        if (string.Equals(databaseOptions.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(databaseOptions.ConnectionString);
            return;
        }

        if (SqliteBootstrapper.IsSqliteProvider(databaseOptions.Provider))
        {
            options.UseSqlite(databaseOptions.ConnectionString);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider '{databaseOptions.Provider}'. BillSoft currently supports SqlServer and Sqlite for local development.");
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }

    internal static OcrOptions BuildOcrOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration[$"{OcrOptions.SectionName}:Provider"];
        var maxUploadBytesValue = configuration[$"{OcrOptions.SectionName}:MaxUploadBytes"];
        var storageRootPath = configuration[$"{OcrOptions.SectionName}:StorageRootPath"] ?? string.Empty;

        var azureSection = configuration.GetSection($"{OcrOptions.SectionName}:AzureDocumentIntelligence");

        var options = new OcrOptions
        {
            Provider = string.IsNullOrWhiteSpace(provider) ? OcrProviderNames.Fake : provider.Trim(),
            MaxUploadBytes = long.TryParse(maxUploadBytesValue, out var parsedMaxUploadBytes) && parsedMaxUploadBytes > 0
                ? parsedMaxUploadBytes
                : 10 * 1024 * 1024,
            StorageRootPath = storageRootPath,
            AzureDocumentIntelligence = new AzureDocumentIntelligenceOptions
            {
                Endpoint = azureSection["Endpoint"] ?? string.Empty,
                ApiKey = azureSection["ApiKey"] ?? string.Empty,
                ModelId = azureSection["ModelId"]?.Trim() ?? string.Empty
            }
        };

        return options;
    }
}

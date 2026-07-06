using System.Text.Json;
using BillSoft.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillSoft.Infrastructure.Persistence;

public sealed class BillSoftDbContextFactory : IDesignTimeDbContextFactory<BillSoftDbContext>
{
    private const string ExplicitConnectionStringRequiredMessage =
        "EF design-time tools require an explicit database connection string. " +
        "Set the Database__ConnectionString environment variable before running EF commands:\n\n" +
        "  Database__ConnectionString=\"<your connection string>\" dotnet ef database update " +
        "--project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api\n\n" +
        "Do not rely on the implicit LocalDB fallback — it targets a different database than the " +
        "running API and will silently apply migrations to the wrong instance. " +
        "After applying, verify __EFMigrationsHistory on the target database to confirm.";

    public BillSoftDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillSoftDbContext>();
        var databaseOptions = ResolveDatabaseOptions()
            ?? throw new InvalidOperationException(ExplicitConnectionStringRequiredMessage);

        DependencyInjection.ConfigureDatabase(optionsBuilder, databaseOptions);

        return new BillSoftDbContext(optionsBuilder.Options);
    }

    private static DatabaseOptions? ResolveDatabaseOptions()
    {
        var environmentDatabaseOptions = ResolveEnvironmentDatabaseOptions();
        if (environmentDatabaseOptions is not null)
        {
            return environmentDatabaseOptions;
        }

        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var productionSettingsPath = Path.Combine(
                currentDirectory.FullName,
                "src",
                "api",
                "BillSoft.Api",
                "appsettings.json");

            var productionDatabaseOptions = TryReadDatabaseOptions(productionSettingsPath);
            if (productionDatabaseOptions is not null)
            {
                return productionDatabaseOptions;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static DatabaseOptions? ResolveEnvironmentDatabaseOptions()
    {
        var environmentProvider = Environment.GetEnvironmentVariable($"{DatabaseOptions.SectionName}__Provider");
        var environmentConnectionString = Environment.GetEnvironmentVariable($"{DatabaseOptions.SectionName}__ConnectionString");

        if (string.IsNullOrWhiteSpace(environmentProvider) && string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return null;
        }

        return CreateDatabaseOptions(environmentProvider, environmentConnectionString);
    }

    private static DatabaseOptions? TryReadDatabaseOptions(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        using var stream = File.OpenRead(settingsPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty(DatabaseOptions.SectionName, out var databaseSection))
        {
            return null;
        }

        var provider = databaseSection.TryGetProperty(nameof(DatabaseOptions.Provider), out var providerElement)
            ? providerElement.GetString()
            : null;

        var connectionString = databaseSection.TryGetProperty(nameof(DatabaseOptions.ConnectionString), out var connectionStringElement)
            ? connectionStringElement.GetString()
            : null;

        return CreateDatabaseOptions(provider, connectionString);
    }

    private static DatabaseOptions? CreateDatabaseOptions(
        string? provider,
        string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var resolvedProvider = string.IsNullOrWhiteSpace(provider) ? "SqlServer" : provider;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(ExplicitConnectionStringRequiredMessage);
        }

        return new DatabaseOptions
        {
            Provider = resolvedProvider,
            ConnectionString = connectionString
        };
    }
}

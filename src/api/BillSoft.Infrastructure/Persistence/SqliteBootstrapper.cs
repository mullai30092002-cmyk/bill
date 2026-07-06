using Microsoft.Extensions.Hosting;

namespace BillSoft.Infrastructure.Persistence;

public static class SqliteBootstrapper
{
    public static bool ShouldBootstrapSqlite(DatabaseOptions databaseOptions, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(environment);

        return IsSqliteProvider(databaseOptions.Provider) && IsLocalSqliteEnvironment(environment);
    }

    public static bool IsSqliteProvider(string? provider)
    {
        return string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLocalSqliteEnvironment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("LocalDevelopment")
            || environment.IsEnvironment("Test")
            || environment.IsEnvironment("Testing");
    }
}

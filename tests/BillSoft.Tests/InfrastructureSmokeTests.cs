using BillSoft.Infrastructure;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BillSoft.Tests;

[CollectionDefinition("Infrastructure smoke", DisableParallelization = true)]
public sealed class InfrastructureSmokeTestsCollection
{
}

[Collection("Infrastructure smoke")]
public sealed class InfrastructureSmokeTests
{
    [Fact]
    public void BillSoftDbContext_Can_Be_Constructed_With_Empty_Options()
    {
        var options = new DbContextOptionsBuilder<BillSoftDbContext>().Options;

        using var context = new BillSoftDbContext(options);

        Assert.NotNull(context);
    }

    [Fact]
    public void AddInfrastructure_Defaults_Missing_Provider_To_SqlServer()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    [Fact]
    public void AddInfrastructure_Uses_Explicit_SqlServer_Provider_Path()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    [Fact]
    public void SqlServer_Migration_Script_Removes_Deprecated_Branch_Timezone_Columns()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoftCleanPilotRehearsalTest;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var script = context.GetService<IMigrator>().GenerateScript(fromMigration: "0");

        Assert.Contains("DROP COLUMN [Currency]", script);
        Assert.Contains("DROP COLUMN [Timezone]", script);
    }

    [Fact]
    public void SqlServer_Migration_Script_Backfills_Normalized_Duplicate_Columns_And_Prechecks_Conflicts()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoftCleanPilotRehearsalTest;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var script = context.GetService<IMigrator>().GenerateScript(fromMigration: "0");

        Assert.Contains("NormalizedMobileNumber", script);
        Assert.Contains("NormalizedBillNumber", script);
        Assert.Contains("NormalizedPhone", script);
        Assert.Contains("THROW 50000", script);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void AddInfrastructure_Allows_Explicit_Sqlite_Only_In_Local_Environments(string environmentName)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "Sqlite",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Data Source=:memory:"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(environmentName));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
    }

    [Fact]
    public void AddInfrastructure_Rejects_Sqlite_In_Production()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "Sqlite",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Data Source=:memory:"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, CreateEnvironment(Environments.Production)));

        Assert.Contains("SQLite database provider is only enabled", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_Throws_For_Unsupported_Provider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "Oracle",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Data Source=:memory:"
            })
            .Build();

        services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<BillSoftDbContext>());

        Assert.Contains("Unsupported database provider 'Oracle'", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_Throws_Clear_Error_When_Database_ConnectionString_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development)));

        Assert.Contains("Database:ConnectionString", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_Rejects_Azure_Ocr_When_Endpoint_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Ocr:Provider"] = "AzureDocumentIntelligence",
                ["Ocr:AzureDocumentIntelligence:ApiKey"] = "unit-test-api-key",
                ["Ocr:AzureDocumentIntelligence:ModelId"] = "prebuilt-invoice"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development)));

        Assert.Contains("Ocr:AzureDocumentIntelligence:Endpoint", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_Rejects_Azure_Ocr_When_ApiKey_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Ocr:Provider"] = "AzureDocumentIntelligence",
                ["Ocr:AzureDocumentIntelligence:Endpoint"] = "https://example.invalid/",
                ["Ocr:AzureDocumentIntelligence:ModelId"] = "prebuilt-invoice"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development)));

        Assert.Contains("Ocr:AzureDocumentIntelligence:ApiKey", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_Rejects_Azure_Ocr_When_ModelId_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:Provider"] = "SqlServer",
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Ocr:Provider"] = "AzureDocumentIntelligence",
                ["Ocr:AzureDocumentIntelligence:Endpoint"] = "https://example.invalid/",
                ["Ocr:AzureDocumentIntelligence:ApiKey"] = "unit-test-api-key"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration, CreateEnvironment(Environments.Development)));

        Assert.Contains("Ocr:AzureDocumentIntelligence:ModelId", exception.Message);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    public void SqliteBootstrapper_Only_Bootstraps_In_Local_Environments(string environmentName, bool expected)
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=:memory:"
        };

        var shouldBootstrap = SqliteBootstrapper.ShouldBootstrapSqlite(databaseOptions, CreateEnvironment(environmentName));

        Assert.Equal(expected, shouldBootstrap);
    }

    [Fact]
    public void SqliteBootstrapper_Does_Not_Bootstrap_For_SqlServer()
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = "SqlServer",
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;"
        };

        var shouldBootstrap = SqliteBootstrapper.ShouldBootstrapSqlite(databaseOptions, CreateEnvironment(Environments.Development));

        Assert.False(shouldBootstrap);
    }

    [Fact]
    public void BillSoftDbContextFactory_Throws_When_No_Explicit_Config_Is_Present()
    {
        using var scope = CreateDirectoryScope();

        var factory = new BillSoftDbContextFactory();
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateDbContext(Array.Empty<string>()));

        Assert.Contains("Database__ConnectionString", exception.Message);
        Assert.Contains("dotnet ef database update", exception.Message);
    }

    [Fact]
    public void BillSoftDbContextFactory_Throws_When_AppSettings_Has_Empty_ConnectionString()
    {
        using var scope = CreateDirectoryScope();

        var settingsDirectory = Path.Combine(scope.State, "src", "api", "BillSoft.Api");
        Directory.CreateDirectory(settingsDirectory);

        File.WriteAllText(
            Path.Combine(settingsDirectory, "appsettings.json"),
            """
            {
              "Database": {
                "Provider": "SqlServer",
                "ConnectionString": ""
              }
            }
            """);

        var factory = new BillSoftDbContextFactory();
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateDbContext(Array.Empty<string>()));

        Assert.Contains("Database__ConnectionString", exception.Message);
        Assert.Contains("__EFMigrationsHistory", exception.Message);
    }

    [Fact]
    public void BillSoftDbContextFactory_Throws_When_Only_Provider_Env_Var_Is_Set_Without_ConnectionString()
    {
        using var directoryScope = CreateDirectoryScope();
        using var providerScope = SetEnvironmentVariable($"{DatabaseOptions.SectionName}__Provider", "SqlServer");

        var factory = new BillSoftDbContextFactory();
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateDbContext(Array.Empty<string>()));

        Assert.Contains("Database__ConnectionString", exception.Message);
    }

    [Fact]
    public void BillSoftDbContextFactory_Ignores_Development_Settings_When_Selecting_DesignTime_Provider()
    {
        using var scope = CreateDirectoryScope();

        var settingsDirectory = Path.Combine(scope.State, "src", "api", "BillSoft.Api");
        Directory.CreateDirectory(settingsDirectory);

        File.WriteAllText(
            Path.Combine(settingsDirectory, "appsettings.Development.json"),
            """
            {
              "Database": {
                "Provider": "Sqlite",
                "ConnectionString": "Data Source=:memory:"
              }
            }
            """);

        var factory = new BillSoftDbContextFactory();
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateDbContext(Array.Empty<string>()));

        Assert.Contains("Database__ConnectionString", exception.Message);
    }

    [Fact]
    public void BillSoftDbContextFactory_Uses_Sqlite_When_Environment_Variables_Specify_It()
    {
        using var directoryScope = CreateDirectoryScope();
        using var providerScope = SetEnvironmentVariable($"{DatabaseOptions.SectionName}__Provider", "Sqlite");
        using var connectionStringScope = SetEnvironmentVariable($"{DatabaseOptions.SectionName}__ConnectionString", "Data Source=:memory:");

        var factory = new BillSoftDbContextFactory();
        using var context = factory.CreateDbContext(Array.Empty<string>());

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
    }

    [Fact]
    public void BillSoftDbContextFactory_Uses_SqlServer_When_Environment_Variable_Provides_ConnectionString()
    {
        using var directoryScope = CreateDirectoryScope();
        using var connectionStringScope = SetEnvironmentVariable(
            $"{DatabaseOptions.SectionName}__ConnectionString",
            "Server=localhost;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;");

        var factory = new BillSoftDbContextFactory();
        using var context = factory.CreateDbContext(Array.Empty<string>());

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestHostEnvironment
        {
            EnvironmentName = environmentName
        };
    }

    private static DisposableScope<string> CreateDirectoryScope()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.SetCurrentDirectory(root);

        return new DisposableScope<string>(
            root,
            _ =>
            {
                Directory.SetCurrentDirectory(originalDirectory);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            });
    }

    private static DisposableScope<string?> SetEnvironmentVariable(string name, string? value)
    {
        var originalValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);

        return new DisposableScope<string?>(
            originalValue,
            previousValue => Environment.SetEnvironmentVariable(name, previousValue));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "BillSoft.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class DisposableScope<T> : IDisposable
    {
        private readonly Action<T> _dispose;
        private bool _disposed;

        public DisposableScope(T state, Action<T> dispose)
        {
            State = state;
            _dispose = dispose;
        }

        public T State { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose(State);
            _disposed = true;
        }
    }
}

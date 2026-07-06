using BillSoft.Infrastructure.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BillSoft.Infrastructure.Setup;

public sealed record FoundationSeedExecutionOptions(
    bool RunFoundationSeed,
    bool ExitAfterSeed,
    bool TriggeredByCommandLine)
{
    public bool ShouldRun => RunFoundationSeed || TriggeredByCommandLine;

    public bool ShouldExitAfterSeed => ExitAfterSeed || TriggeredByCommandLine;
}

public static class FoundationSeedRuntime
{
    public const string RunFoundationSeedConfigKey = "Setup:RunFoundationSeed";
    public const string ExitAfterSeedConfigKey = "Setup:ExitAfterSeed";
    public const string SeedFoundationArgument = "--seed-foundation";

    public static FoundationSeedExecutionOptions ParseOptions(IConfiguration configuration, string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);

        var triggeredByCommandLine = args.Any(argument =>
            string.Equals(argument, SeedFoundationArgument, StringComparison.OrdinalIgnoreCase));

        return new FoundationSeedExecutionOptions(
            RunFoundationSeed: ParseBoolean(configuration[RunFoundationSeedConfigKey], defaultValue: false),
            ExitAfterSeed: ParseBoolean(configuration[ExitAfterSeedConfigKey], defaultValue: false),
            TriggeredByCommandLine: triggeredByCommandLine);
    }

    public static async Task<FoundationSeedResult?> ExecuteAsync(
        IServiceProvider services,
        ILogger logger,
        FoundationSeedExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ShouldRun)
        {
            return null;
        }

        logger.LogInformation(
            "Foundation seed execution started. runFoundationSeed={RunFoundationSeed} exitAfterSeed={ExitAfterSeed} triggeredByCommandLine={TriggeredByCommandLine}",
            options.RunFoundationSeed,
            options.ExitAfterSeed,
            options.TriggeredByCommandLine);

        await using var scope = services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<IFoundationSeedService>();
        var result = await seedService.SeedAsync(cancellationToken);

        logger.LogInformation(
            "Foundation seed execution completed. permissionsInserted={PermissionsInserted} rolesInserted={RolesInserted} rolePermissionsInserted={RolePermissionsInserted} startedAtUtc={StartedAtUtc:o} completedAtUtc={CompletedAtUtc:o}",
            result.PermissionsInserted,
            result.RolesInserted,
            result.RolePermissionsInserted,
            result.StartedAtUtc,
            result.CompletedAtUtc);

        return result;
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }
}

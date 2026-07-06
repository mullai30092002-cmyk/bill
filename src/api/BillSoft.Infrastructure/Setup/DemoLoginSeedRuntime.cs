using BillSoft.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BillSoft.Infrastructure.Setup;

public sealed record DemoLoginSeedExecutionOptions(bool TriggeredByCommandLine)
{
    public bool ShouldRun => TriggeredByCommandLine;

    public bool ShouldExitAfterSeed => TriggeredByCommandLine;
}

public static class DemoLoginSeedRuntime
{
    public const string SeedDemoLoginArgument = "--seed-demo-login";

    public static DemoLoginSeedExecutionOptions ParseOptions(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var triggeredByCommandLine = args.Any(argument =>
            string.Equals(argument, SeedDemoLoginArgument, StringComparison.OrdinalIgnoreCase));

        return new DemoLoginSeedExecutionOptions(triggeredByCommandLine);
    }

    public static async Task<DemoLoginSeedResult?> ExecuteAsync(
        IServiceProvider services,
        ILogger logger,
        DemoLoginSeedExecutionOptions options,
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
            "Demo login seed execution started. triggeredByCommandLine={TriggeredByCommandLine}",
            options.TriggeredByCommandLine);

        await using var scope = services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<IDemoLoginSeedService>();
        var result = await seedService.SeedAsync(cancellationToken);

        logger.LogInformation(
            "Demo login seed execution completed. restaurantCode={RestaurantCode} restaurantCreated={RestaurantCreated} branchCreated={BranchCreated} userCreated={UserCreated} roleAssignmentCreated={RoleAssignmentCreated} startedAtUtc={StartedAtUtc:o} completedAtUtc={CompletedAtUtc:o}",
            result.RestaurantCode,
            result.RestaurantCreated,
            result.BranchCreated,
            result.UserCreated,
            result.RoleAssignmentCreated,
            result.StartedAtUtc,
            result.CompletedAtUtc);

        return result;
    }
}

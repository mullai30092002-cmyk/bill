namespace BillSoft.Infrastructure.Seed;

public sealed record DemoLoginSeedResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string RestaurantCode,
    bool RestaurantCreated,
    bool BranchCreated,
    bool UserCreated,
    bool RoleAssignmentCreated);

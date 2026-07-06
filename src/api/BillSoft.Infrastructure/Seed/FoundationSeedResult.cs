namespace BillSoft.Infrastructure.Seed;

public sealed record FoundationSeedResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int PermissionsInserted,
    int RolesInserted,
    int RolePermissionsInserted);

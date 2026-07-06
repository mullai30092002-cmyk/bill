namespace BillSoft.Infrastructure.Seed;

public interface IDemoLoginSeedService
{
    Task<DemoLoginSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}

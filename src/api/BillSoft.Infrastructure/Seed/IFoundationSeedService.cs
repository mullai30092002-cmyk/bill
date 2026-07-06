namespace BillSoft.Infrastructure.Seed;

public interface IFoundationSeedService
{
    Task<FoundationSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}

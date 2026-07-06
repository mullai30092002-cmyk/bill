using Xunit;

namespace BillSoft.Tests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void BillSoft_Domain_Project_Should_Be_Loadable()
    {
        var assembly = typeof(BillSoft.Domain.Common.BaseEntity).Assembly;

        Assert.Equal("BillSoft.Domain", assembly.GetName().Name);
    }
}

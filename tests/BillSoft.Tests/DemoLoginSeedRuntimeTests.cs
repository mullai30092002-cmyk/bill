using BillSoft.Infrastructure.Setup;
using Xunit;

namespace BillSoft.Tests;

public sealed class DemoLoginSeedRuntimeTests
{
    [Fact]
    public void ParseOptions_Should_Default_To_Disabled()
    {
        var options = DemoLoginSeedRuntime.ParseOptions(Array.Empty<string>());

        Assert.False(options.TriggeredByCommandLine);
        Assert.False(options.ShouldRun);
        Assert.False(options.ShouldExitAfterSeed);
    }

    [Fact]
    public void ParseOptions_Should_Enable_Seed_When_Command_Line_Flag_Is_Used()
    {
        var options = DemoLoginSeedRuntime.ParseOptions(["--seed-demo-login"]);

        Assert.True(options.TriggeredByCommandLine);
        Assert.True(options.ShouldRun);
        Assert.True(options.ShouldExitAfterSeed);
    }
}

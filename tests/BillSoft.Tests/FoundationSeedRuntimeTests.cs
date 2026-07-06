using BillSoft.Infrastructure.Setup;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BillSoft.Tests;

public sealed class FoundationSeedRuntimeTests
{
    [Fact]
    public void ParseOptions_Should_Default_To_Disabled()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = FoundationSeedRuntime.ParseOptions(configuration, Array.Empty<string>());

        Assert.False(options.RunFoundationSeed);
        Assert.False(options.ExitAfterSeed);
        Assert.False(options.TriggeredByCommandLine);
        Assert.False(options.ShouldRun);
        Assert.False(options.ShouldExitAfterSeed);
    }

    [Fact]
    public void ParseOptions_Should_Enable_Seed_When_Config_Is_Enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FoundationSeedRuntime.RunFoundationSeedConfigKey] = "true",
                [FoundationSeedRuntime.ExitAfterSeedConfigKey] = "false"
            })
            .Build();

        var options = FoundationSeedRuntime.ParseOptions(configuration, Array.Empty<string>());

        Assert.True(options.RunFoundationSeed);
        Assert.False(options.ExitAfterSeed);
        Assert.False(options.TriggeredByCommandLine);
        Assert.True(options.ShouldRun);
        Assert.False(options.ShouldExitAfterSeed);
    }

    [Fact]
    public void ParseOptions_Should_Enable_Seed_And_Exit_When_Command_Line_Flag_Is_Used()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FoundationSeedRuntime.RunFoundationSeedConfigKey] = "false",
                [FoundationSeedRuntime.ExitAfterSeedConfigKey] = "false"
            })
            .Build();

        var options = FoundationSeedRuntime.ParseOptions(configuration, ["--seed-foundation"]);

        Assert.False(options.RunFoundationSeed);
        Assert.False(options.ExitAfterSeed);
        Assert.True(options.TriggeredByCommandLine);
        Assert.True(options.ShouldRun);
        Assert.True(options.ShouldExitAfterSeed);
    }
}

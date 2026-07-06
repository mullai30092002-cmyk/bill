using BillSoft.Domain.Localization;
using Xunit;

namespace BillSoft.Tests;

public sealed class MobileNumberNormalizerTests
{
    [Fact]
    public void Singapore_National_Number_Should_Normalize_To_E164()
    {
        var normalized = MobileNumberNormalizer.Normalize("SG", "90000001");

        Assert.Equal("SG", normalized.CountryCode);
        Assert.Equal("+65", normalized.DialCode);
        Assert.Equal("90000001", normalized.NationalNumber);
        Assert.Equal("+6590000001", normalized.E164);
    }

    [Fact]
    public void Singapore_E164_Number_Should_Remain_Canonical()
    {
        var normalized = MobileNumberNormalizer.Normalize("SG", "+6590000001");

        Assert.Equal("+6590000001", normalized.E164);
    }

    [Fact]
    public void Singapore_Number_With_Spaces_Should_Normalize()
    {
        var normalized = MobileNumberNormalizer.Normalize("SG", "65 9000 0001");

        Assert.Equal("+6590000001", normalized.E164);
    }

    [Fact]
    public void India_National_Number_Should_Normalize_To_E164()
    {
        var normalized = MobileNumberNormalizer.Normalize("IN", "9876543210");

        Assert.Equal("IN", normalized.CountryCode);
        Assert.Equal("+91", normalized.DialCode);
        Assert.Equal("9876543210", normalized.NationalNumber);
        Assert.Equal("+919876543210", normalized.E164);
    }

    [Fact]
    public void India_E164_Number_Should_Remain_Canonical()
    {
        var normalized = MobileNumberNormalizer.Normalize("IN", "+919876543210");

        Assert.Equal("+919876543210", normalized.E164);
    }

    [Fact]
    public void India_Number_With_Spaces_Should_Normalize()
    {
        var normalized = MobileNumberNormalizer.Normalize("IN", "91 98765 43210");

        Assert.Equal("+919876543210", normalized.E164);
    }

    [Fact]
    public void India_Number_With_Leading_Zero_Should_Normalize()
    {
        var normalized = MobileNumberNormalizer.Normalize("IN", "09876543210");

        Assert.Equal("+919876543210", normalized.E164);
    }

    [Fact]
    public void Unsupported_Country_Should_Be_Rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MobileNumberNormalizer.Normalize("US", "90000001"));

        Assert.Contains("Unsupported country code", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_Number_Should_Be_Rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MobileNumberNormalizer.Normalize("SG", "123"));

        Assert.Contains("cannot normalize", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

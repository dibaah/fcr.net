using FCR.Domain;
using Xunit;

namespace FCR.Tests;

public class FcrCodeTests
{
    [Theory]
    [InlineData("FCR-CT-PI-046", "FCR-CT-PI-046")]
    [InlineData(" fcr-ct-pi-46 ", "FCR-CT-PI-46")]
    [InlineData("FCR-CT-IN-031", "FCR-CT-IN-031")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Validate_NormalizesOrAcceptsValid(string? input, string expected) =>
        Assert.Equal(expected, FcrCode.Validate(input));

    [Theory]
    [InlineData("FCR-XX-PI-1")]   // bad area
    [InlineData("FCR-CT-ZZ-1")]   // bad dept
    [InlineData("CT-PI-046")]     // missing prefix
    [InlineData("FCR-CT-PI")]     // missing number
    public void Validate_RejectsMalformed(string input) =>
        Assert.Throws<FcrValidationException>(() => FcrCode.Validate(input));

    [Fact]
    public void FolderPath_MapsAreaAndDept() =>
        Assert.Equal(
            Path.Combine("/root", "CTEP", "PI", "FCR-CT-PI-046"),
            FcrCode.GenerateFolderPath("/root", "FCR-CT-PI-046"));

    [Fact]
    public void FolderPath_EmptyWhenUnmappable() =>
        Assert.Equal("", FcrCode.GenerateFolderPath("/root", "FCR-ZZ-XX-999"));
}

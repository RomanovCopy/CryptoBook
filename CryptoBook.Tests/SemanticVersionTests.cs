using CryptoBook.DTO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.7", "1.0.7")]
    [InlineData("v2.3.4", "2.3.4")]
    [InlineData("v1.1.0.1", "1.1.0.1")]
    [InlineData("1.2.3-beta.2+build.9", "1.2.3-beta.2")]
    public void TryParse_AcceptsReleaseTag(string value, string expected)
    {
        Assert.True(SemanticVersion.TryParse(value, out SemanticVersion? version));
        Assert.Equal(expected, version!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+build+second")]
    [InlineData("1.2.3-бета")]
    [InlineData("release-1.2.3")]
    public void TryParse_RejectsInvalidVersion(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0")]
    [InlineData("1.1.0.1", "1.1.0")]
    [InlineData("1.1.0.2", "1.1.0.1")]
    [InlineData("2.0.0", "1.99.99")]
    [InlineData("1.0.0", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.10", "1.0.0-rc.2")]
    [InlineData("1.0.0-beta.11", "1.0.0-beta.2")]
    public void CompareTo_UsesSemanticVersionPrecedence(
        string newer,
        string older)
    {
        SemanticVersion left = Parse(newer);
        SemanticVersion right = Parse(older);

        Assert.True(left.CompareTo(right) > 0);
        Assert.True(right.CompareTo(left) < 0);
    }

    private static SemanticVersion Parse(string value)
    {
        Assert.True(SemanticVersion.TryParse(value, out SemanticVersion? version));
        return version!;
    }
}

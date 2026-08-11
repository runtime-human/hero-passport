using HeroPassport.Domain.Primitives;
using System.Text;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class SafePrimitivesTests
{
    [Fact]
    public void SafeTextNormalizesNfcWhitespaceAndScalarBounds()
    {
        var value = SafeTextV1.Normalize("  Cafe\u0301\t hello\nworld  ", 1, 100);

        Assert.Equal("Café hello world", value);
        Assert.True(value.IsNormalized(NormalizationForm.FormC));
    }

    [Theory]
    [InlineData("bad\0text")]
    [InlineData("left\u202Eright")]
    public void SafeTextRejectsDangerousControls(string value)
    {
        Assert.Throws<ArgumentException>(() => SafeTextV1.Normalize(value, 1, 100));
    }

    [Fact]
    public void SafeTextRejectsUnpairedSurrogate()
    {
        var value = new string(['b', 'a', 'd', '\uD800', 't', 'e', 'x', 't']);
        Assert.Throws<ArgumentException>(() => SafeTextV1.Normalize(value, 1, 100));
    }

    [Fact]
    public void TypedIdsUseCanonicalLowercaseUuidV7()
    {
        var heroId = HeroId.New();
        var text = heroId.ToString();

        Assert.Equal(36, text.Length);
        Assert.Equal('7', text[14]);
        Assert.Equal(text.ToLowerInvariant(), text);
        Assert.Equal(heroId, HeroId.Parse(text));
        Assert.Throws<FormatException>(() => HeroId.Parse(text.ToUpperInvariant()));
    }

    [Fact]
    public void JsonSafeIntegerRejectsValuesOutsideJavascriptExactRange()
    {
        Assert.Equal(9_007_199_254_740_991L, JsonSafeInteger.Require(9_007_199_254_740_991L));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSafeInteger.Require(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSafeInteger.Require(9_007_199_254_740_992L));
    }
}

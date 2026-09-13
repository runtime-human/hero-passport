using HeroPassport.Web.Security;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class LocalWebSessionAuthorityTests
{
    private const string Bootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string Session = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public void CorrectBootstrapIsConsumedExactlyOnce()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);

        Assert.True(authority.TryConsumeBootstrap(Bootstrap));
        Assert.False(authority.TryConsumeBootstrap(Bootstrap));
    }

    [Fact]
    public void WrongBootstrapDoesNotConsumeLegitimateCapability()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);

        Assert.False(authority.TryConsumeBootstrap(Session));
        Assert.True(authority.TryConsumeBootstrap(Bootstrap));
    }

    [Fact]
    public async Task ConcurrentCorrectBootstrapHasExactlyOneWinner()
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => authority.TryConsumeBootstrap(Bootstrap))));

        Assert.Single(results, static result => result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64url!")]
    [InlineData("AA")]
    public void MalformedSessionFailsClosed(string? candidate)
    {
        var authority = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);

        Assert.False(authority.IsSessionValid(candidate));
    }

    [Fact]
    public void SessionValidationIsAuthoritySpecific()
    {
        var first = LocalWebSessionAuthority.CreateForTesting(Bootstrap, Session);
        var second = LocalWebSessionAuthority.CreateForTesting(
            "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8",
            "YGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn8");

        Assert.True(first.IsSessionValid(Session));
        Assert.False(second.IsSessionValid(Session));
    }
}

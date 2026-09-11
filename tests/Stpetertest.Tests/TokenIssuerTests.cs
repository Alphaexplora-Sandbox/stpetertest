using Stpetertest.Domain;
using Xunit;

namespace Stpetertest.Tests;

public class TokenIssuerTests
{
    [Fact]
    public void Issue_RefusesEverything_WhenNoCredentialIsConfigured()
    {
        // The safe direction to fail. A scaffold that fell back to a default
        // would publish that credential on every deployment made from it.
        var issuer = new TokenIssuer(null, null);

        Assert.False(issuer.IsConfigured);
        Assert.Null(issuer.Issue("anyone", "anything"));
    }

    [Fact]
    public void Issue_ReturnsAValidToken_ForTheConfiguredCredential()
    {
        var issuer = new TokenIssuer("ci", "secret");

        var token = issuer.Issue("ci", "secret");

        Assert.NotNull(token);
        Assert.True(issuer.IsValid(token));
    }

    [Theory]
    [InlineData("ci", "wrong")]
    [InlineData("wrong", "secret")]
    [InlineData("", "")]
    [InlineData("ci", "secre")]
    public void Issue_RefusesAnyOtherCredential(string username, string password)
    {
        var issuer = new TokenIssuer("ci", "secret");

        Assert.Null(issuer.Issue(username, password));
    }

    [Fact]
    public void IsValid_RejectsATokenItNeverIssued()
    {
        var issuer = new TokenIssuer("ci", "secret");

        Assert.False(issuer.IsValid("not-a-real-token"));
        Assert.False(issuer.IsValid(null));
        Assert.False(issuer.IsValid("   "));
    }

    [Fact]
    public void IsValid_RejectsATokenOnceItHasExpired()
    {
        // Expiry that is never enforced is not expiry. A zero lifetime makes
        // the window already closed by the time the check runs.
        var issuer = new TokenIssuer("ci", "secret", TimeSpan.Zero);

        var token = issuer.Issue("ci", "secret");

        Assert.NotNull(token);
        Assert.False(issuer.IsValid(token));
        // And it stays rejected: the entry is dropped on the failed check, so
        // a second call must not resurrect it.
        Assert.False(issuer.IsValid(token));
    }

    [Fact]
    public void Issue_ReturnsADifferentTokenEachTime()
    {
        var issuer = new TokenIssuer("ci", "secret");

        Assert.NotEqual(issuer.Issue("ci", "secret"), issuer.Issue("ci", "secret"));
    }

    [Fact]
    public void IsConfigured_IsFalse_WhenOnlyOneHalfIsSet()
    {
        // A half-configured deployment is not a working one, and must not be
        // treated as if the operator meant to leave it open.
        Assert.False(new TokenIssuer("ci", null).IsConfigured);
        Assert.False(new TokenIssuer(null, "secret").IsConfigured);
        Assert.False(new TokenIssuer("  ", "  ").IsConfigured);
    }
}

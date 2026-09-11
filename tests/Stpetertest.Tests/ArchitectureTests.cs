using NetArchTest.Rules;
using Stpetertest.Domain;
using Xunit;

namespace Stpetertest.Tests;

// Asserted against the compiled assembly, so the rule holds for what was
// actually built rather than for what the folder names suggest.
public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Target =
        typeof(IServiceStatus).Assembly;

    private const string DomainNamespace = "Stpetertest.Domain";

    [Fact]
    public void Domain_Namespace_Is_Not_Empty()
    {
        // NetArchTest reports success for a rule with nothing to check, so
        // a renamed or emptied namespace would silently disable the rule
        // below rather than fail. This is the guard for that.
        var domainTypes = Types.InAssembly(Target)
            .That()
            .ResideInNamespace(DomainNamespace)
            .GetTypes();

        Assert.NotEmpty(domainTypes);
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_AspNetCore()
    {
        // A domain type that references the web framework cannot be reused
        // outside a web host, and cannot be tested without one.
        var result = Types.InAssembly(Target)
            .That()
            .ResideInNamespace(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Domain types must not depend on ASP.NET Core: " +
                string.Join(", ", result.FailingTypeNames ?? []));
    }
}

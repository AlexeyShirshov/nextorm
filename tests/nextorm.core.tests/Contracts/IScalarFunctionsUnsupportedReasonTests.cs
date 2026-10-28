using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The <see cref="IScalarFunctions.GetUnsupportedReason"/> hook is a default interface method: a
/// provider (or an external implementation) written before it existed keeps compiling and keeps its
/// previous generic "not supported" behaviour instead of gaining a version-specific reason.
/// </summary>
public class IScalarFunctionsUnsupportedReasonTests
{
    [Fact]
    public void GetUnsupportedReason_ShouldDefaultToNullForAnImplementationWithoutTheMember()
    {
        IScalarFunctions functions = new LegacyScalarFunctions();

        functions.GetUnsupportedReason("array_except").Should().BeNull(
            "the default implementation leaves the caller's generic message in place");
    }

    /// <summary>An implementation predating the optional member: it declares no reason at all.</summary>
    private sealed class LegacyScalarFunctions : IScalarFunctions
    {
        public bool Supports(string name) => true;

        public string Render(string name, IReadOnlyList<string> args) => name;
    }
}

using System.Reflection;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the source-compatible default-interface shape of the D392 vector-search dialect members.
/// </summary>
public sealed class VectorSearchDialectInterfaceCompatibilityTests
{
    [Fact]
    public void VectorSearchMembers_ShouldRemainDefaultInterfaceImplementations()
    {
        var dialectInterface = typeof(ISqlDialect);

        AssertDefaultInterfaceImplementation(
            dialectInterface.GetProperty(nameof(ISqlDialect.SupportsVectorSearch))?.GetMethod,
            "get_SupportsVectorSearch");
        AssertDefaultInterfaceImplementation(
            dialectInterface.GetMethod(nameof(ISqlDialect.MakeApproximateTop)),
            nameof(ISqlDialect.MakeApproximateTop));
        AssertDefaultInterfaceImplementation(
            dialectInterface.GetProperty(nameof(ISqlDialect.ApproximateVectorSearchTopNArgumentName))?.GetMethod,
            "get_ApproximateVectorSearchTopNArgumentName");
    }

    private static void AssertDefaultInterfaceImplementation(MethodInfo? method, string memberName)
    {
        method.Should().NotBeNull($"{memberName} must remain part of ISqlDialect");
        method!.IsAbstract.Should().BeFalse(
            $"{memberName} must have a default body so existing direct ISqlDialect implementations remain source-compatible");
    }
}

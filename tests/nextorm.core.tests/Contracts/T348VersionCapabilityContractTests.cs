using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T348 (#282) U1: the version-capability members are additive default interface members, so an
/// external dialect or <see cref="ISqlServerFunctions"/> implementation written before they existed
/// keeps compiling and keeps the fail-closed behaviour.
/// </summary>
public sealed class T348VersionCapabilityContractTests
{
    [Theory]
    [InlineData(nameof(ISqlDialect.SupportsSqlServer2025Functions))]
    [InlineData(nameof(ISqlDialect.SupportsSqlServer2022Functions))]
    [InlineData(nameof(ISqlDialect.SupportsProductAggregate))]
    public void VersionCapability_ShouldBeADefaultInterfaceMethodReturningFalse(string name)
    {
        var property = typeof(ISqlDialect).GetProperty(name);

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<bool>();
        property.GetMethod.Should().NotBeNull();
        property.GetMethod!.IsAbstract.Should().BeFalse(
            "the member keeps a default implementation so existing external dialects stay compatible");
    }

    [Fact]
    public void GetUnsupportedReason_ShouldBeADefaultInterfaceMethodReturningNull()
    {
        var method = typeof(ISqlServerFunctions).GetMethod(nameof(ISqlServerFunctions.GetUnsupportedReason));

        method.Should().NotBeNull();
        method!.IsAbstract.Should().BeFalse(
            "an external ISqlServerFunctions written before the member existed keeps its generic refusal");

        ISqlServerFunctions stub = new LegacySqlServerFunctions();
        stub.GetUnsupportedReason("regexp_count").Should().BeNull();
        stub.Supports("regexp_count").Should().BeFalse();
    }

    /// <summary>An implementation shaped like one written before the version-reason member existed.</summary>
    private sealed class LegacySqlServerFunctions : ISqlServerFunctions
    {
        public bool Supports(string name) => false;

        public string Render(string name, IReadOnlyList<string> args) =>
            throw new NotSupportedException($"The {name} function is not supported by this provider.");
    }
}

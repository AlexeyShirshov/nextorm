using System.Reflection;
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
    [InlineData(nameof(ISqlDialect.SupportsApproxCountDistinctAggregate))]
    [InlineData(nameof(ISqlDialect.SupportsChecksumAggregate))]
    [InlineData(nameof(ISqlDialect.SupportsGroupingIdAggregate))]
    [InlineData(nameof(ISqlDialect.SupportsApproxPercentileCont))]
    [InlineData(nameof(ISqlDialect.SupportsApproxPercentileDisc))]
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

    /// <summary>
    /// T348 U4: the five aggregate members carry the CLR shape the engine reports (a count as
    /// <c>long</c>, a checksum as <c>int?</c>, a grouping mask as <c>int</c>, the continuous percentile
    /// as <c>double?</c> and the discrete one as the sort-input type). No member returns a
    /// <see cref="WindowFunction{T}"/>, which is why the translator's <c>OVER</c> refusal is
    /// unreachable from the public surface — the engine's own Msg 10754 is the only place that
    /// obligation is observable.
    /// </summary>
    [Theory]
    [InlineData("approx_count_distinct", "System.Int64")]
    [InlineData("checksum_agg", "System.Nullable`1[System.Int32]")]
    [InlineData("grouping_id", "System.Int32")]
    [InlineData("approx_percentile_cont", "System.Nullable`1[System.Double]")]
    [InlineData("approx_percentile_disc", null)]
    public void U4AggregateMember_ShouldHaveTheEngineClrShapeAndNoWindowOverload(string name, string? returnType)
    {
        var members = typeof(SqlServerFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == name)
            .ToArray();

        members.Should().ContainSingle("the U4 surface declares one aggregate member per SQL name");

        var member = members[0];
        if (returnType is not null)
            member.ReturnType.ToString().Should().Be(returnType);

        (member.ReturnType.IsGenericType
            && member.ReturnType.GetGenericTypeDefinition() == typeof(WindowFunction<>))
            .Should().BeFalse("a scalar return keeps the aggregate out of the window pipeline");
    }

    [Fact]
    public void U4Percentiles_ShouldExposeNoWindowFunctionOverload()
    {
        var windowOverloads = typeof(SqlServerFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name is "approx_percentile_cont" or "approx_percentile_disc"
                || m.Name is "approx_count_distinct" or "checksum_agg" or "grouping_id")
            .Where(m => m.ReturnType.IsGenericType
                && m.ReturnType.GetGenericTypeDefinition() == typeof(WindowFunction<>))
            .Select(m => m.Name)
            .ToArray();

        windowOverloads.Should().BeEmpty(
            "the approximate percentiles are WITHIN GROUP aggregates: no public member offers OVER");
    }

    /// <summary>An implementation shaped like one written before the version-reason member existed.</summary>
    private sealed class LegacySqlServerFunctions : ISqlServerFunctions
    {
        public bool Supports(string name) => false;

        public string Render(string name, IReadOnlyList<string> args) =>
            throw new NotSupportedException($"The {name} function is not supported by this provider.");
    }
}

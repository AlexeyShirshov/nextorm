using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding;

public sealed class ShardKeyPredicateExtractorTests
{
    private static readonly PropertyInfo TenantProperty = typeof(CompositeRow).GetProperty(nameof(CompositeRow.Tenant))!;
    private static readonly PropertyInfo IdProperty = typeof(CompositeRow).GetProperty(nameof(CompositeRow.Id))!;

    [Fact]
    public void Extractor_ShouldReadConstantEquality()
    {
        Expression<Func<CompositeRow, bool>> predicate = row => row.Id == 17;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [IdProperty], out var values).Should().BeTrue();
        values.Should().Equal(17);
    }

    [Fact]
    public void Extractor_ShouldReadClosureMemberEquality()
    {
        var expected = 23;
        Expression<Func<CompositeRow, bool>> predicate = row => row.Id == expected;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [IdProperty], out var values).Should().BeTrue();
        values.Should().Equal(expected);
    }

    [Fact]
    public void Extractor_ShouldSupportReversedEquality()
    {
        var expected = 29;
        Expression<Func<CompositeRow, bool>> predicate = row => expected == row.Id;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [IdProperty], out var values).Should().BeTrue();
        values.Should().Equal(expected);
    }

    [Fact]
    public void Extractor_ShouldCollectCompositeKeyEqualitiesJoinedByAnd()
    {
        var tenant = "blue";
        var id = 31;
        Expression<Func<CompositeRow, bool>> predicate = row => row.Tenant == tenant && row.Id == id;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [TenantProperty, IdProperty], out var values).Should().BeTrue();
        values.Should().Equal(tenant, id);
    }

    [Fact]
    public void Extractor_ShouldReturnFalseForMissingKeyEquality()
    {
        Expression<Func<CompositeRow, bool>> predicate = row => row.Tenant == "blue";

        ShardKeyPredicateExtractor.TryGetValues(predicate, [TenantProperty, IdProperty], out var values).Should().BeFalse();
        values.Should().BeEmpty();
    }

    [Fact]
    public void Extractor_ShouldReturnFalseForOr()
    {
        Expression<Func<CompositeRow, bool>> predicate = row => row.Id == 3 || row.Id == 4;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [IdProperty], out var values).Should().BeFalse();
        values.Should().BeEmpty();
    }

    [Fact]
    public void Extractor_ShouldReturnFalseForInequality()
    {
        Expression<Func<CompositeRow, bool>> predicate = row => row.Id != 5;

        ShardKeyPredicateExtractor.TryGetValues(predicate, [IdProperty], out var values).Should().BeFalse();
        values.Should().BeEmpty();
    }

    private sealed class CompositeRow
    {
        public string Tenant { get; set; } = string.Empty;
        public int Id { get; set; }
    }
}

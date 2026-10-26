using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T340 (#340) DB-free seam for the <see cref="TimeOnly"/> projection.
/// <para>
/// At r1 (red) the reader-factory arm does not exist yet, so composing a mapper for a
/// <see cref="TimeOnly"/>-typed column still lands in the generic
/// <c>SelectExpression.GetDataRecordMethod</c> rejection. This fact therefore fails by assertion on
/// the unchanged implementation — the visible defect — and is the entry point for the B3 domain-guard
/// matrix (negative tick, exactly <see cref="TimeSpan.TicksPerDay"/>, in-day <see cref="TimeSpan"/>,
/// <see cref="TimeOnly"/> passthrough, null, alien representation) driven through the same seam.
/// </para>
/// </summary>
public class TimeOnlyProjectionTests
{
    [Fact]
    public void TimeOnly_ColumnMapper_ShouldBeBuildable()
    {
        var column = new SelectExpression(typeof(TimeOnly))
        {
            Index = 0,
            PropertyName = "T",
        };
        var record = Expression.Parameter(typeof(IDataRecord), "record");

        FluentActions.Invoking(() => RowMapperFactory.MapColumn(column, record, supportsNativeDuration: true))
            .Should().NotThrow(
                "a TimeOnly-typed mapped column must be composable for a provider with a native time-of-day reader");
    }
}

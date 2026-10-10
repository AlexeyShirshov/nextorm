using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #162 boundary regression for the in-memory context: it does not implement
/// <see cref="IBatchExecutor"/>, so starting a read over a lazy temp-table source is rejected up front
/// with <see cref="NotSupportedException"/> rather than silently reading a mapped base table.
/// </summary>
[Trait("D162", "Boundary")]
public class TempTableNavigationBoundaryTests
{
    [Fact]
    public void InMemory_read_over_a_temp_table_source_should_throw_not_supported()
    {
        using var ctx = new InMemoryDataContext();

        var tempTable = ctx.From<ConventionalEntity>()
            .Where(x => x.Id > 0)
            .Select(x => new { x.Id })
            .AsTempTable();

        var act = () => ctx.From(tempTable);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot materialise a temporary table*");
    }
}

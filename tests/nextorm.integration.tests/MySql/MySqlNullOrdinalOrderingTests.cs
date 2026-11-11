using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// The T287 NULL-placement matrix covers the expression-key path; it never orders by an output column
/// ordinal with an explicit NULL placement. MySQL has no <c>NULLS FIRST</c>/<c>NULLS LAST</c> syntax
/// (<c>SqlDialectBase.SupportsNullsOrdering</c> is false), so the ordinal branch is the one that has to
/// emulate the placement with a CASE over the resolved key expression. These facts execute that branch
/// against a live server and assert the row order the client receives.
/// </summary>
[SqlTable("c13_null_ordinal")]
internal interface IC13NullOrdinalRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? Key { get; set; }
}

[Collection("MySql")]
public sealed class MySqlNullOrdinalOrderingTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    public MySqlNullOrdinalOrderingTests()
    {
        Execute("drop table if exists c13_null_ordinal");
        Execute("create table c13_null_ordinal (id int not null primary key, k int null)");
        Execute("insert into c13_null_ordinal (id, k) values (1, null), (2, 2), (3, null), (4, -1), (5, 1)");
    }

    /// <summary>Ordinal key 2 is <c>k</c>; NULLs belong at the end of an ascending order.</summary>
    [Fact]
    public void OrdinalOrderBy_WithNullsLast_ShouldPlaceNullsAtTheEnd()
    {
        var rows = Context.From<IC13NullOrdinalRow>()
            .OrderBy(2, NullsPosition.Last)
            .ThenBy(1)
            .Select(x => new { x.Id, x.Key })
            .ToList();

        rows.Select(r => r.Key).Should().Equal(new int?[] { -1, 1, 2, null, null });
        rows.Select(r => r.Id).Should().Equal(4, 5, 2, 1, 3);
    }

    /// <summary>Descending with NULLs first is the mirrored cell of the same emulation.</summary>
    [Fact]
    public void OrdinalOrderByDescending_WithNullsFirst_ShouldPlaceNullsAtTheStart()
    {
        var rows = Context.From<IC13NullOrdinalRow>()
            .OrderByDescending(2, NullsPosition.First)
            .ThenBy(1)
            .Select(x => new { x.Id, x.Key })
            .ToList();

        rows.Select(r => r.Key).Should().Equal(new int?[] { null, null, 2, 1, -1 });
        rows.Select(r => r.Id).Should().Equal(1, 3, 2, 5, 4);
    }

    /// <summary>
    /// A second ordinal key after the emulated placement: the CASE must be emitted for the first key
    /// and the plain ordinal for the second, and the result must still be ordered by both.
    /// </summary>
    [Fact]
    public void OrdinalOrderBy_WithNullsFirst_ShouldKeepTheSecondaryOrdinalKey()
    {
        var rows = Context.From<IC13NullOrdinalRow>()
            .OrderBy(2, NullsPosition.First)
            .ThenBy(1)
            .Select(x => new { x.Id, x.Key })
            .ToList();

        rows.Select(r => r.Key).Should().Equal(new int?[] { null, null, -1, 1, 2 });
        rows.Select(r => r.Id).Should().Equal(1, 3, 4, 5, 2);
    }

    /// <summary>
    /// The CASE emulation repeats the ordering key outside the select list, which DISTINCT does not
    /// permit, so the builder refuses instead of emitting SQL the server would reject. This is the
    /// fail-closed guard, not a silent drop of the requested placement.
    /// </summary>
    [Fact]
    public void Distinct_WithExplicitNullPlacement_ShouldBeRefused()
    {
        var act = () => Context.From<IC13NullOrdinalRow>()
            .Distinct()
            .OrderBy(2, NullsPosition.Last)
            .Select(x => new { x.Id, x.Key })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("DISTINCT");
    }

    /// <summary>An ordinal that does not resolve to a selected column cannot carry a CASE placement.</summary>
    [Fact]
    public void OrdinalOrderBy_OutsideTheSelectList_ShouldBeRefused()
    {
        var act = () => Context.From<IC13NullOrdinalRow>()
            .OrderBy(9, NullsPosition.Last)
            .Select(x => new { x.Id, x.Key })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("does not resolve to a selected column");
    }

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

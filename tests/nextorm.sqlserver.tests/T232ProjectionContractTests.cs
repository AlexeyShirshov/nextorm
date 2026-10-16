using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-02 (#232) shared projection contract: the frozen legacy-vs-typed-column oracle for the typed
/// SQL Server <c>PIVOT</c>/<c>UNPIVOT</c> result projection. It pins, before the typed overloads
/// exist, the exact legacy (untyped <see cref="TableAlias"/>) SQL, the by-name typed-column SQL the
/// typed projector is specified to emit (<see cref="SqlFunctions.Column{T}"/>), the alias spelling,
/// the pivot value order and the UNPIVOT role identifiers, so the typed implementation (D-04/D-05)
/// is measured against an already-committed oracle instead of its own output.
/// </summary>
/// <remarks>
/// The frozen contract lives in
/// <c>artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/test-contract.json</c>. This file is test-only
/// data: no production abstraction and no typed overload is compiled here. It never opens a
/// connection.
/// </remarks>
public class T232ProjectionContractTests
{
    // ------------------------------------------------------------------ frozen contract (rv=1)

    /// <summary>The first PIVOT value of the frozen fixture.</summary>
    internal const string PivotValueOne = "1";

    /// <summary>The second PIVOT value of the frozen fixture.</summary>
    internal const string PivotValueTwo = "2";

    /// <summary>The ordered <c>IN (...)</c> fragment (projection-order contract).</summary>
    internal const string PivotValueOrderFragment = "for quarter in ([1], [2])";

    /// <summary>The UNPIVOT value-column role identifier of the frozen fixture.</summary>
    internal const string UnpivotValueRole = "val";

    /// <summary>The UNPIVOT name-column role identifier of the frozen fixture.</summary>
    internal const string UnpivotNameRole = "qtr";

    /// <summary>The legacy (untyped) PIVOT SQL of the frozen fixture.</summary>
    internal const string LegacyPivotSql =
        "select category, [1], [2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]";

    /// <summary>The legacy (untyped) UNPIVOT SQL of the frozen fixture.</summary>
    internal const string LegacyUnpivotSql =
        "select category, qtr, val\n from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]";

    /// <summary>The by-name typed-column PIVOT SQL the typed projector must reproduce.</summary>
    internal const string TypedColumnPivotSql =
        "select [category], [1] as [Q1], [2] as [Q2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]";

    /// <summary>The by-name typed-column UNPIVOT SQL the typed projector must reproduce.</summary>
    internal const string TypedColumnUnpivotSql =
        "select [category], [qtr] as [Quarter], [val] as [Measure]\n from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]";

    // ------------------------------------------------------------------ frozen fixture rows

    /// <summary>One deterministic sales row of the frozen fixture.</summary>
    internal readonly record struct SalesRow(string Category, int Quarter, decimal? Margin);

    /// <summary>The deterministic sales fixture; the two quarter values define the PIVOT value order.</summary>
    internal static readonly SalesRow[] SalesRows =
    [
        new("North", 1, 10m),
        new("North", 2, 20m),
        new("South", 1, 5m)
    ];

    /// <summary>One deterministic quarterly row of the frozen fixture.</summary>
    internal readonly record struct QuarterlyRow(string Category, decimal? Q1, decimal? Q2);

    /// <summary>The deterministic quarterly fixture (UNPIVOT source).</summary>
    internal static readonly QuarterlyRow[] QuarterlyRows =
    [
        new("North", 10m, 20m),
        new("South", 5m, null)
    ];

    // ------------------------------------------------------------------ helpers

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ------------------------------------------------------------------ oracle

    [Fact]
    public void LegacyPivot_EmitsBareColumnsInProjectionOrder()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        // The legacy by-name route emits the raw identifiers, in projection order, with no alias.
        SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create(PivotValueOne), PivotValue.Create(PivotValueTwo))
            .Select(t => new
            {
                Category = t.GetString("category"),
                Q1 = t.GetNullableDecimal("[1]"),
                Q2 = t.GetNullableDecimal("[2]")
            }))
            .Should().Be(LegacyPivotSql);
    }

    [Fact]
    public void TypedColumnPivot_EmitsMemberAliasPerProjectedColumn()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        // The mechanism the typed projector is specified to use: SqlFunctions.Column<T>(source, name)
        // binds the TableAlias source and sets ColumnName, so each cell is aliased to its member.
        SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create(PivotValueOne), PivotValue.Create(PivotValueTwo))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Q1 = SqlFunctions.Column<decimal?>(t, "1"),
                Q2 = SqlFunctions.Column<decimal?>(t, "2")
            }))
            .Should().Be(TypedColumnPivotSql);
    }

    [Fact]
    public void LegacyPivot_And_TypedColumnPivot_AgreeOnSourceAndValueOrder()
    {
        using var ctx = SqlServerTestContext.Create();

        var legacy = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create(PivotValueOne), PivotValue.Create(PivotValueTwo))
            .Select(t => new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]"), Q2 = t.GetNullableDecimal("[2]") }));

        var typed = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create(PivotValueOne), PivotValue.Create(PivotValueTwo))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Q1 = SqlFunctions.Column<decimal?>(t, "1"),
                Q2 = SqlFunctions.Column<decimal?>(t, "2")
            }));

        // Same pivot source, same ordered IN list; only the member aliases differ.
        legacy.Should().Contain(PivotValueOrderFragment);
        typed.Should().Contain(PivotValueOrderFragment);
        typed.Should().Contain("pivot (sum(margin)");
        legacy.Should().Contain("select category, [1], [2]");
        typed.Should().Contain("select [category], [1] as [Q1], [2] as [Q2]");
    }

    [Fact]
    public void LegacyUnpivot_And_TypedColumnUnpivot_UseTheSameRoleIdentifiers()
    {
        using var ctx = SqlServerTestContext.Create();

        var legacy = SqlOf(ctx, ctx.From<IQuarterlyEntity>()
            .Unpivot(UnpivotValueRole, UnpivotNameRole, UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new { Category = t.GetString("category"), Qtr = t.GetString("qtr"), Val = t.GetNullableDecimal("val") }));

        var typed = SqlOf(ctx, ctx.From<IQuarterlyEntity>()
            .Unpivot(UnpivotValueRole, UnpivotNameRole, UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Quarter = SqlFunctions.Column<string>(t, UnpivotNameRole),
                Measure = SqlFunctions.Column<decimal?>(t, UnpivotValueRole)
            }));

        legacy.Should().Be(LegacyUnpivotSql);
        typed.Should().Be(TypedColumnUnpivotSql);

        // One identifier per role: the value column and the name column are distinct roles.
        UnpivotValueRole.Should().NotBe(UnpivotNameRole);
        typed.Should().Contain($"unpivot ([{UnpivotValueRole}] for [{UnpivotNameRole}] in ([q1], [q2]))");
    }

    [Fact]
    public void FixtureRows_DefineTheNullAndCountExpectations()
    {
        // SUM over a group missing a value yields NULL; a COUNT over the same group yields 0.
        var north = SalesRows.Where(r => r.Category == "North").ToArray();
        north.Select(r => r.Quarter).Should().Equal(1, 2);

        var northQ1 = north.Where(r => r.Quarter == 1).Sum(r => r.Margin);
        var southQ2 = SalesRows.Where(r => r.Category == "South" && r.Quarter == 2).Select(r => r.Margin).FirstOrDefault();

        northQ1.Should().Be(10m);
        southQ2.Should().BeNull("a missing PIVOT cell materializes NULL");
        SalesRows.Count(r => r.Category == "South" && r.Quarter == 2).Should().Be(0, "COUNT of a missing PIVOT cell is 0");

        // UNPIVOT keeps a NULL cell as a NULL value column row.
        QuarterlyRows.Single(r => r.Category == "South").Q2.Should().BeNull();
    }
}

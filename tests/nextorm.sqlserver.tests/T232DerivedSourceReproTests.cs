using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D232-PREREQ-typed-derived (#232, r=3, n=2): the regression probes for the typed derived-source defect
/// (<c>D232-D05-typed-derived-source-not-composable</c>). The typed <c>Pivot</c>/<c>Unpivot</c>
/// overloads return an <see cref="EntityBuilder{TShape}"/> over a derived pivot source and their XML doc
/// promises it "can be consumed as a derived source or CTE"; these probes pin the derived-source routes
/// and the exact SQL each must emit.
/// <para>
/// Every probe is <c>Explicit</c>, so it is never part of the default suite: it is executed in its own
/// isolated child process by <c>rv3/verify.py</c> (C20/C34/C35), so a process-killing termination stays
/// inside that child instead of aborting a whole sweep. The probes assert the whole statement, not just
/// the absence of an exception: the intermediate derived layer must reference the derived table's own
/// output identifiers (a bare <c>[category]</c> and the aliased <c>[Q1]</c>) instead of re-rendering the
/// inner projection and re-aliasing the underlying source column (<c>[1] as [Q1]</c>, which SQL Server
/// rejects as <c>Invalid column name '1'</c>).
/// </para>
/// </summary>
public class T232DerivedSourceReproTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<TShape> TypedPivot<TShape>(IDataContext ctx, System.Linq.Expressions.Expression<System.Func<ISalesEntity, NextORM.Core.PivotValues, TShape>> projection)
        => ctx.From<ISalesEntity>().Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, projection);

    // ------------------------------------------------------------------ V26 (a): typed EntityBuilder source

    /// <summary>(a) The typed builder is consumed as a derived source and the whole statement is valid.</summary>
    [Fact(Explicit = true)]
    public void Repro01_TypedBuilderSource_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var typed = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        // ctx.From(EntityBuilder<TShape>) copies the builder; the derived layer must reference the
        // derived table's own output identifiers, never re-alias the inner source column.
        var sql = SqlOf(ctx, ctx.From(typed).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Be(
            "select [category], [Q1]\n" +
            " from (select [category], [Q1]\n" +
            " from (select category, [1] as [Q1]\n" +
            " from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]) as [t3]");
        sql.Should().NotContain("[1] as [Q1]\n from (select category", "the intermediate layer must not re-alias the source column");
    }

    /// <summary>(a) The typed builder source composes through Where + Select with the full statement.</summary>
    [Fact(Explicit = true)]
    public void Repro02_TypedBuilderSource_WhereSelectIsComposable()
    {
        using var ctx = SqlServerTestContext.Create();
        var typed = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        var sql = SqlOf(ctx, ctx.From(typed).Where(x => x.Q1 > 0).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Be(
            "select [category], [Q1]\n" +
            " from (select [category], [Q1]\n" +
            " from (select category, [1] as [Q1]\n" +
            " from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]) as [t3]\n" +
            " where (t3.[Q1] > cast(0 as decimal(38, 10)))");
    }

    // ------------------------------------------------------------------ V27 (b): typed QueryCommand source

    /// <summary>(b) The typed command is consumed as a derived source with the full statement.</summary>
    [Fact(Explicit = true)]
    public void Repro03_TypedCommandSource_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var command = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") }).ToCommand();

        var sql = SqlOf(ctx, ctx.From(command).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Be(
            "select [category], [Q1]\n" +
            " from (select [category], [Q1]\n" +
            " from (select category, [1] as [Q1]\n" +
            " from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]) as [t3]");
    }

    /// <summary>(b) A self-join of the typed derived command resolves both sides with the full statement.</summary>
    [Fact(Explicit = true)]
    public void Repro04_TypedCommandSource_SelfJoinResolves()
    {
        using var ctx = SqlServerTestContext.Create();
        var command = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") }).ToCommand();

        var sql = SqlOf(ctx, ctx.From(command)
            .Join(ctx.From(command), (a, b) => a.Q1 < b.Q1)
            .Select(p => new { Left = p.Item1.Category, Right = p.Item2.Category }));

        sql.Should().Be(
            "select t3.[category], t6.[category]\n" +
            " from (select [category], [Q1]\n" +
            " from (select category, [1] as [Q1]\n" +
            " from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]) as [t3]\n" +
            " join (select [category], [Q1]\n" +
            " from (select category, [1] as [Q1]\n" +
            " from sales pivot (sum(margin) for quarter in ([1])) as [t4]) as [t5]) as [t6] on (t3.[Q1] < t6.[Q1])");
    }

    // ------------------------------------------------------------------ V28 (c): pivot-free control

    /// <summary>
    /// (c) The pivot-free control: the projection is embedded directly through <c>From(QueryCommand)</c>.
    /// The derived read collapses to the bare two-layer projection, proving the re-entry under test is
    /// not a general property of every derived read.
    /// </summary>
    [Fact(Explicit = true)]
    public void Repro05_PivotFreeDerivedControl_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();

        var inner = ctx.From("t232_sales")
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Margin = SqlFunctions.Column<decimal?>(t, "margin")
            });

        var sql = SqlOf(ctx, ctx.From(inner).Select(x => new { x.Category, x.Margin }));

        sql.Should().Be(
            "select category, margin\n" +
            " from (select category, margin\n" +
            " from t232_sales) as [t1]");
    }

    // ------------------------------------------------------------------ V28 (c2): pivot-free builder mirror

    /// <summary>
    /// (c2) The pivot-free mirror of R01's route: a metadata-less <see cref="EntityBuilder{TShape}"/> whose
    /// source is a derived projection is consumed through <c>From(EntityBuilder&lt;TShape&gt;)</c> + <c>Select</c>.
    /// Unlike R05 (which embeds the projection directly through <c>From(QueryCommand)</c>), this goes
    /// through the builder route the typed PIVOT result uses, so it pins the intermediate derived layer's
    /// identifiers without PIVOT in the picture.
    /// </summary>
    [Fact(Explicit = true)]
    public void Repro05b_PivotFreeBuilderDerivedControl_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();

        var inner = ctx.From("t232_sales")
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Margin = SqlFunctions.Column<decimal?>(t, "margin")
            });

        var builder = ctx.From(inner);

        var sql = SqlOf(ctx, ctx.From(builder).Select(x => new { x.Category, x.Margin }));

        sql.Should().Be(
            "select [category], [margin]\n" +
            " from (select [category], [margin]\n" +
            " from (select category, margin\n" +
            " from t232_sales) as [t1]) as [t2]");
    }

    // ------------------------------------------------------------------ R06: unaliased identifier

    /// <summary>
    /// R06 (V29 prerequisite): the grouping column is emitted by a source-column name that is not aliased
    /// (its target member differs from the source column only by case), so the derived layer must expose
    /// the source's own spelling <c>[category]</c> — an invented <c>[Category]</c> would not resolve on a
    /// case-sensitive collation.
    /// </summary>
    [Fact(Explicit = true)]
    public void Repro06_DerivedGroupingColumn_KeepsTheUnaliasedIdentifier()
    {
        using var ctx = SqlServerTestContext.Create();
        var typed = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        var sql = SqlOf(ctx, ctx.From(typed).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Contain("select [category], [Q1]\n from (select category, [1] as [Q1]");
        sql.Should().NotContain("[Category]", "the unaliased grouping column keeps the source's own spelling");
    }

    // ------------------------------------------------------------------ R07: plan/cache locality

    /// <summary>
    /// R07 (V24 prerequisite): two derived sources whose typed shape is the same CLR type but whose pivot
    /// values differ must not be confused in the plan or the shared select-list cache: each statement keeps
    /// its own pivot value and repeating one yields it identically.
    /// </summary>
    [Fact(Explicit = true)]
    public void Repro07_TwoDerivedSourcesWithDifferentValues_StayDistinct()
    {
        using var ctx = SqlServerTestContext.Create();

        var first = TypedPivot(ctx, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });
        var second = TypedPivot(ctx, (m, pv) => new { m.Category, V = pv.Value<decimal?>("2") });

        var firstSql = SqlOf(ctx, ctx.From(first).Select(x => new { x.Category, x.V }));
        var secondSql = SqlOf(ctx, ctx.From(second).Select(x => new { x.Category, x.V }));

        firstSql.Should().Contain("[1] as [V]").And.Contain("in ([1])");
        secondSql.Should().Contain("[2] as [V]").And.Contain("in ([2])");
        firstSql.Should().NotContain("[2]");
        secondSql.Should().NotContain("[1]");
        // Repeating the first derived source after the interleaved one yields the identical statement.
        SqlOf(ctx, ctx.From(first).Select(x => new { x.Category, x.V })).Should().Be(firstSql);
    }
}

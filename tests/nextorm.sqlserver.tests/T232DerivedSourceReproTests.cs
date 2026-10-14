using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D232-PREREQ-typed-derived (#232, r=3): the baseline regression probes for the typed derived-source
/// defect (<c>D232-D05-typed-derived-source-not-composable</c>). The typed <c>Pivot</c>/<c>Unpivot</c>
/// overloads return an <see cref="EntityBuilder{TShape}"/> over a derived pivot source and their XML doc
/// promises it "can be consumed as a derived source or CTE"; these probes pin the derived-source routes
/// that must keep working.
/// <para>
/// Every probe is <c>Explicit</c>, so it is never part of the default suite: the baseline behaviour is a
/// process-killing termination, and executing it unconditionally would abort every later sweep of this
/// project while the defect is open. <c>rv3/verify.py</c> (C20) runs each probe in its own isolated child
/// process and records the actual outcome verbatim. No database connection is opened: the failure occurs
/// while the command is prepared.
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

    /// <summary>(a) The typed builder is consumed as a derived source and the projection survives.</summary>
    [Fact(Explicit = true)]
    public void Repro01_TypedBuilderSource_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var typed = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        // ctx.From(EntityBuilder<TShape>) copies the builder; the returned source must keep the typed
        // projection instead of collapsing to the bare inner command.
        var sql = SqlOf(ctx, ctx.From(typed).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Contain("[Q1]");
    }

    /// <summary>(a) The typed builder source composes through Where + Select.</summary>
    [Fact(Explicit = true)]
    public void Repro02_TypedBuilderSource_WhereSelectIsComposable()
    {
        using var ctx = SqlServerTestContext.Create();
        var typed = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        var sql = SqlOf(ctx, ctx.From(typed).Where(x => x.Q1 > 0).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Contain("where");
        sql.Should().Contain("[Q1]");
    }

    // ------------------------------------------------------------------ V27 (b): typed QueryCommand source

    /// <summary>(b) The typed command is consumed as a derived source and the projection survives.</summary>
    [Fact(Explicit = true)]
    public void Repro03_TypedCommandSource_ProjectsAsDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var command = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") }).ToCommand();

        var sql = SqlOf(ctx, ctx.From(command).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Contain("[Q1]");
    }

    /// <summary>(b) A self-join of the typed derived command resolves both sides.</summary>
    [Fact(Explicit = true)]
    public void Repro04_TypedCommandSource_SelfJoinResolves()
    {
        using var ctx = SqlServerTestContext.Create();
        var command = TypedPivot(ctx, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") }).ToCommand();

        var sql = SqlOf(ctx, ctx.From(command)
            .Join(ctx.From(command), (a, b) => a.Q1 < b.Q1)
            .Select(p => new { Left = p.Item1.Category, Right = p.Item2.Category }));

        sql.Should().Contain("join");
    }

    // ------------------------------------------------------------------ V28 (c): pivot-free control

    /// <summary>
    /// (c) The pivot-free control: the same two-layer derived shape as the typed pivot route, but over a
    /// plain table source instead of a PIVOT source. It establishes whether the re-entry exists
    /// independently of PIVOT.
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

        sql.Should().Contain("select");
    }
}

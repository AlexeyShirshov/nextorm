using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

/// <summary>
/// Serialized with the other plan-cache hit assertions: a concurrent <c>PurgeQueryCache</c> on another
/// xunit thread evicts a raw plan this thread just stored (see <see cref="QueryPlanCacheCollection"/>).
/// </summary>
[Collection("Query plan cache")]
public class D362CacheLifecycleBTests
{
    private sealed class E
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    // 1. Clone and CloneForCache preserve effective state -> same SQL
    [Fact]
    public void Clone_PreservesEffectiveState()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var sql1 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);

        var clone = (QueryCommand<long>)cmd.Clone();
        var sql2 = Normalize(Prepare(ctx, clone).SqlStmt ?? string.Empty);
        sql1.Should().Be(sql2);
    }

    [Fact]
    public void CloneForCache_PreservesShapeAndRebinds()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        // The cache clone must carry the prepared effective state (grouping + HAVING) and the live
        // capture must rebind from the shared closure. Behaviour, not a not-null smoke check.
        var min = 6;
        var original = ctx.From<Grp>()
            .Where(x => x.Amount > min)
            .SelectGroupHaving(
                x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) },
                r => r.Total > 0);

        var preparedOriginal = Prepare(ctx, original);
        var sql = Normalize(preparedOriginal.SqlStmt!);
        preparedOriginal.NoParams.Should().BeFalse();
        preparedOriginal.NeedsParamRefresh.Should().BeTrue("the captured value refreshes per execution");
        ctx.ToList(preparedOriginal).Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)null, Total = 1 },
        }, "min = 6 keeps only the 10/20/7 rows");

        var clone = (QueryCommand<GrpRow>)original.CloneForCache();
        min = 0;

        var preparedClone = Prepare(ctx, clone);
        Normalize(preparedClone.SqlStmt!).Should().Be(sql, "the clone derives the identical statement");
        CountOf(sql, "group by").Should().Be(1, "grouping survives the clone once");
        CountOf(sql, "having").Should().Be(1, "HAVING survives the clone once");
        Regex.Matches(sql, @"\bdistinct\b", RegexOptions.IgnoreCase).Count.Should().Be(0);

        ctx.ToList(preparedClone).Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        }, "the clone rebinds the changed captured value (min = 0)");
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the clone reuses the prepared shape without re-inference");

        // DISTINCT is part of the effective state too: clone the constant projection.
        var constant = ctx.From<Grp>().Where(x => x.Id < 0)
            .SelectGroup(x => new GrpRow { Category = null, Total = 1 });
        var preparedConstant = Prepare(ctx, constant);
        var constantClone = (QueryCommand<GrpRow>)constant.CloneForCache();
        var preparedConstantClone = Prepare(ctx, constantClone);

        Normalize(preparedConstantClone.SqlStmt!).Should().Be(Normalize(preparedConstant.SqlStmt!));
        Regex.Matches(Normalize(preparedConstantClone.SqlStmt!), @"\bdistinct\b", RegexOptions.IgnoreCase).Count
            .Should().Be(1, "the constant projection keeps exactly one DISTINCT after cloning");
        ctx.ToList(preparedConstantClone).Should().BeEmpty("the empty source still returns zero rows");
    }

    // 2. shared AnyCommand / sticky Cache flag untouched
    [Fact]
    public void Operator_DoesNotTouchCacheFlag()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = e.Select(x => x.Id);
        var before = cmd.Cache;
        var _ = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        cmd.Cache.Should().Be(before);
    }

    // 3. raw-cache hit: prepare twice yields identical SQL (no re-inference needed)
    [Fact]
    public void RawCacheHit_IdenticalSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var sql1 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        cmd.ResetPreparation();
        var sql2 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql1.Should().Be(sql2);
    }

    private sealed class GrpRow
    {
        public string? Category { get; set; }
        public int Total { get; set; }
    }

    private sealed class Grp
    {
        public long Id { get; set; }
        public string? Category { get; set; }
        public int Amount { get; set; }
    }

    private const string GrpSeed =
        "CREATE TABLE Grp (Id INTEGER PRIMARY KEY, Category TEXT NULL, Amount INTEGER NOT NULL);" +
        "INSERT INTO Grp (Id, Category, Amount) VALUES (1,'a',10),(2,'a',20),(3,'b',5),(4,NULL,7);";

    private static (SqliteConnection Connection, SqliteDataContext Context) CreateGrpContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var seed = connection.CreateCommand();
        seed.CommandText = GrpSeed;
        seed.ExecuteNonQuery();
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        return (connection, ctx);
    }

    // 4. AC6 zero-inference proof: a confirmed warm raw-recipe hit runs the inference visitor once on the
    //    cold preparation and zero times on the second, execution-verified command. The fixture is
    //    closure-free, which is exactly what the raw-recipe path requires (a captured value makes the raw
    //    shape ineligible by design: QueryCommand.QueryPreparer.HasCapturedValue).
    [Fact]
    public void SelectGroup_WarmRawHit_RunsInferenceOnce()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var cold = ctx.From<Grp>()
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the cold preparation runs the inference once");
        cold.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        });

        var warm = ctx.From<Grp>()
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(
            1, "a confirmed raw-cache hit must run the inference visitor zero times");
        warm.Should().BeEquivalentTo(cold);
    }

    [Fact]
    public void SelectGroupHaving_WarmRawHit_RunsInferenceOnce()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var cold = ctx.From<Grp>()
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count() },
                r => r.Category != null)
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the cold preparation runs the inference once");
        cold.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
        });

        var warm = ctx.From<Grp>()
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count() },
                r => r.Category != null)
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(
            1, "a confirmed raw-cache hit must run the inference visitor zero times");
        warm.Should().BeEquivalentTo(cold);
    }

    // 6. Raw-shape matrix: the same shape and closure reuse one prepared command; every distinct shape
    //    (different literal, condition expression, closure member, operator) is a different key.
    [Fact]
    public void RawShape_SameShapeAndClosure_ReusesTheSamePreparedCommand()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var min = 6;
        QueryCommand<GrpRow> Build() => ctx.From<Grp>()
            .Where(x => x.Amount > min)
            .SelectGroup(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) });

        var first = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        second.Should().BeSameAs(first, "one raw shape means one prepared plan");
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the same shape and closure run inference once");
    }

    [Fact]
    public void RawShape_DifferentLiteral_IsADifferentKey()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        _ = ctx.From<Grp>().Where(x => x.Amount > 6)
            .SelectGroup(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) }).ToList();
        _ = ctx.From<Grp>().Where(x => x.Amount > 7)
            .SelectGroup(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) }).ToList();

        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(2, "a different literal is a different raw shape");
    }

    [Fact]
    public void RawShape_DifferentConditionExpression_IsADifferentKey()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        _ = ctx.From<Grp>()
            .SelectGroupHaving(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) }, r => r.Category == "a")
            .ToList();
        _ = ctx.From<Grp>()
            .SelectGroupHaving(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) }, r => r.Category == "b")
            .ToList();

        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(2, "a different condition literal is a different raw shape");
    }

    [Fact]
    public void RawShape_DifferentOperator_IsADifferentKey()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        _ = ctx.From<Grp>()
            .SelectGroup(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) })
            .ToList();
        _ = ctx.From<Grp>()
            .SelectGroupHaving(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) }, r => r.Category != null)
            .ToList();

        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(2, "SelectGroup and SelectGroupHaving carry different component state");
    }

    // 7. ResetPreparation re-derives the effective state: the HAVING predicate is not duplicated and
    //    DISTINCT is not doubled.
    [Fact]
    public void ResetPreparation_RecomputesPredicatesWithoutDuplication()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;

        var cmd = ctx.From<Grp>()
            .SelectGroupHaving(
                x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) },
                r => r.Category != "z");

        var first = Normalize(((DbPreparedQueryCommand<GrpRow>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).SqlStmt!);
        cmd.ResetPreparation();
        var second = Normalize(((DbPreparedQueryCommand<GrpRow>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).SqlStmt!);

        second.Should().Be(first, "ResetPreparation must rebuild the same statement");
        CountOf(second, "having").Should().Be(1, "the HAVING clause is rendered once");
        CountOf(second, "'z'").Should().Be(1, "the HAVING predicate is not duplicated across prepare cycles");
    }

    [Fact]
    public void ResetPreparation_ConstantProjection_KeepsOneDistinct()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;

        var cmd = ctx.From<Grp>().Where(x => x.Id < 0).SelectGroup(x => new GrpRow { Category = null, Total = 1 });

        var first = Normalize(((DbPreparedQueryCommand<GrpRow>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).SqlStmt!);
        cmd.ResetPreparation();
        var second = Normalize(((DbPreparedQueryCommand<GrpRow>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).SqlStmt!);

        second.Should().Be(first);
        Regex.Matches(second, @"\bdistinct\b", RegexOptions.IgnoreCase).Count.Should().Be(1, "DISTINCT is not doubled");
        CountOf(second, "where").Should().Be(1, "the pre-group predicate is rendered once");
    }

    private static int CountOf(string sql, string fragment)
        => Regex.Matches(sql, Regex.Escape(fragment)).Count;

    // 8. Positional parameters (SqlFunctions.Parameter) are part of the shape and bind per execution:
    //    a warm raw hit runs no inference and the second execution reads the new positional value.
    [Fact]
    public void SelectGroup_PositionalWarmHit_RebindsRows_OneInference()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        QueryCommand<GrpRow> Build() => ctx.From<Grp>()
            .Where(x => x.Amount > SqlFunctions.Parameter<int>(0))
            .SelectGroup(x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) });

        var first = Prepare(ctx, Build());
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "cold preparation infers once");
        ctx.ToList(first, new object[] { 6 }).Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)null, Total = 1 },
        }, "positional value 6 keeps only the rows with amount > 6");

        var second = Prepare(ctx, Build());
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the warm raw hit runs no inference");
        second.Should().BeSameAs(first, "the same shape serves the same prepared command");

        ctx.ToList(second, new object[] { 0 }).Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        }, "the second execution binds the new positional value; the stale 6-row set is not observed");
    }

    [Fact]
    public void SelectGroupHaving_PositionalWarmHit_RebindsRows_OneInference()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        QueryCommand<GrpRow> Build() => ctx.From<Grp>()
            .SelectGroupHaving(
                x => new GrpRow { Category = x.Category, Total = SqlFunctions.Sql.count(x.Id) },
                r => r.Total > SqlFunctions.Parameter<int>(0));

        var first = Prepare(ctx, Build());
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "cold preparation infers once");
        ctx.ToList(first, new object[] { 2 }).Should().BeEmpty("count > 2 filters every group");

        var second = Prepare(ctx, Build());
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the warm raw hit runs no inference");
        second.Should().BeSameAs(first, "the same shape serves the same prepared command");

        ctx.ToList(second, new object[] { 0 }).Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        }, "the HAVING threshold rebinds positionally; the empty stale result is not observed");
    }

    // 5. A captured value in the same closure is a raw-cache hit: the raw shape is keyed by the closure
    //    identity, so the second command adopts the stored recipe (inference stays 1) and the refreshed
    //    parameter reads the closure's current value (the rows change).
    [Fact]
    public void SelectGroup_ChangedCapturedValue_SameClosure_IsARawHit()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var min = 6;
        var cold = ctx.From<Grp>()
            .Where(x => x.Amount > min)
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the cold preparation runs the inference once");
        cold.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)null, Total = 1 },
        });

        min = 0;
        var warm = ctx.From<Grp>()
            .Where(x => x.Amount > min)
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(
            1, "the same closure instance must hit the raw recipe with zero inference runs");
        warm.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        }, "the captured parameter must rebind to the new value on the raw hit");
    }

    [Fact]
    public void SelectGroupHaving_ChangedCapturedValue_SameClosure_IsARawHit()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var category = "a";
        var cold = ctx.From<Grp>()
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count() },
                r => r.Category == category)
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1, "the cold preparation runs the inference once");
        cold.Should().BeEquivalentTo(new[] { new { Category = (string?)"a", Total = 2 } });

        category = "b";
        var warm = ctx.From<Grp>()
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count() },
                r => r.Category == category)
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(
            1, "the same closure instance must hit the raw recipe with zero inference runs");
        warm.Should().BeEquivalentTo(
            new[] { new { Category = (string?)"b", Total = 1 } },
            "the captured HAVING parameter must rebind to the new value on the raw hit");
    }

    // 6. No false hit: the same closure instance but a different captured member is a different raw
    //    shape, so the second command prepares (and infers) again.
    [Fact]
    public void SelectGroup_DifferentCapturedMember_IsNotARawHit()
    {
        var (connection, ctx) = CreateGrpContext();
        using var connectionScope = connection;
        using var ctxScope = ctx;
        SelectGroupDiagnostics.Reset();

        var low = 6;
        var high = 0;
        var first = ctx.From<Grp>()
            .Where(x => x.Amount > low)
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(1);

        var second = ctx.From<Grp>()
            .Where(x => x.Amount > high)
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count() })
            .ToList();
        SelectGroupDiagnostics.AnalyzeCalls.Should().Be(
            2, "a different captured member is a different raw shape and must not reuse the first recipe");
        second.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)"b", Total = 1 },
            new { Category = (string?)null, Total = 1 },
        });
        first.Should().BeEquivalentTo(new[]
        {
            new { Category = (string?)"a", Total = 2 },
            new { Category = (string?)null, Total = 1 },
        });
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T217 (#217) executed regression for the cached-path parameter-refresh fallback that reaches the
/// previously uncovered true arm at <c>src/nextorm.core/Visitors/MemberTranslator.cs:135</c> — a
/// <see cref="DateTimeOffset"/> date part rendered while <c>BaseExpressionVisitor.IsParamMode</c> is
/// true. A query that carries a sort has no <c>ParamRefreshRecipe</c>, so a plan-cache hit re-renders
/// the WHERE through <c>QueryPlanner.ExtractParams</c> (the only <c>ParamMode=true</c> render root);
/// the arm is load-bearing because the SQL builder is null in param mode.
/// </summary>
[CollectionDefinition("T217 param refresh", DisableParallelization = true)]
public sealed class T217ParamRefreshCollectionDefinition;

/// <summary>
/// Runs the parameter-refresh regression on SQLite (no container required). The collection is
/// serialized against every other integration collection so the process-wide plan-cache diagnostic
/// counters are read without a concurrent mutation.
/// </summary>
[Collection("T217 param refresh")]
public sealed class SqliteT217DateTimeOffsetParamRefreshTests : ProviderTestSuite
{
    private static readonly object SeedGate = new();
    private static bool _seeded;

    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static void Exec(IDataContext ctx, string sql)
    {
        using (ctx.ExecuteRaw(sql))
        {
        }
    }

    private void EnsureFixture()
    {
        lock (SeedGate)
        {
            if (_seeded)
                return;

            var ctx = _sut.DataProvider;
            Exec(ctx, "drop table if exists t217_param");
            Exec(ctx, "create table t217_param (id integer primary key, value text not null)");
            Exec(
                ctx,
                "insert into t217_param (id, value) values "
                + "(1, '2023-05-17 10:00:00.123+00:00'), "
                + "(2, '2024-01-01 00:00:00.001+00:00'), "
                + "(3, '2023-12-31 23:59:59.500+00:00')");

            _seeded = true;
        }
    }

    [Fact]
    public void CachedParamRefresh_ShouldReWalkTheDtoPartInParamModeAndRebindTheChangedYear()
    {
        // E217-18 / R217-08-MT135-EXEC: the reachable MemberTranslator.cs:135 arm is exercised by a real
        // cached execution, not asserted. The sort keeps ParamRefreshRecipe.TryCreate at null
        // (HasSupportedShape rejects Sorting), so the second (plan-cache hit) execution refreshes the
        // captured year through ExtractParams -> ParamMode render of the WHERE -> TryTranslate with
        // IsParamMode == true. Because the builder is null in param mode, a missing arm would throw
        // instead of silently returning, so a green second run is load-bearing evidence.
        EnsureFixture();
        var ctx = _sut.DataProvider;
        ((DataContext)ctx).PurgeQueryCache();

        QueryPlanner.ResetCounters();
        ParamRefreshRecipe.ResetCounters();

        var capturedYear = 2023;
        var first = ctx.From<IT217ParamEntity>()
            .Where(x => x.Value.Year == capturedYear)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        var afterFirst = QueryPlanner.FallbackRefreshes;

        capturedYear = 2024;
        var second = ctx.From<IT217ParamEntity>()
            .Where(x => x.Value.Year == capturedYear)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        var afterSecond = QueryPlanner.FallbackRefreshes;

        first.Should().Equal(new[] { 1, 3 }, "the first execution matches the 2023 rows");
        second.Should().Equal(new[] { 2 }, "the plan-cache hit must rebind the changed captured year");
        ParamRefreshRecipe.FastBindHits.Should().Be(0, "a sorted shape has no ParamRefreshRecipe");
        afterFirst.Should().Be(0, "the cold render (ParamMode=false) enters no refresh path");
        afterSecond.Should().Be(
            afterFirst + 1,
            "the cache hit refreshes through ExtractParams, the only ParamMode=true render root that walks the WHERE (MemberTranslator.cs:135)");
    }
}

/// <summary>T217 fixture entity for the cached parameter-refresh regression (table <c>t217_param</c>).</summary>
[SqlTable("t217_param")]
public interface IT217ParamEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("value")]
    DateTimeOffset Value { get; set; }
}

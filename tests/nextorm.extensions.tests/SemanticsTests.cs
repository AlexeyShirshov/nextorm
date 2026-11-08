using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

/// <summary>
/// Behaviour of <c>SelectWhereMin</c>/<c>SelectWhereMax</c> asserted against the fixed fixture below, not
/// against another implementation. Every expected row set is written out explicitly: two implementations
/// agreeing is not evidence that either is right, and the compatibility operator is scheduled for removal.
/// </summary>
/// <remarks>
/// Fixture (scores 10,10,20,NULL,20,30,5; group keys 1,1,2,NULL,2,3,NULL):
/// <code>
/// id score name  createdAt   groupKey groupKey2
///  1    10    a  2024-01-01         1        x
///  2    10    b  2024-02-01         1        x
///  3    20    c  2024-03-01         2        y
///  4  NULL    d  2024-04-01      NULL        x
///  5    20    e  2024-05-01         2        y
///  6    30    f  2024-06-01         3        z
///  7     5    g  2024-07-01      NULL        y
/// </code>
/// A NULL value key never wins an extreme, so id 4 is excluded from every value comparison.
/// </remarks>
public class SemanticsTests
{
    private sealed class SemEntity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? GroupKey { get; set; }
        public string? GroupKey2 { get; set; }
    }

    private sealed class Dto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class FloatEntity
    {
        public long Id { get; set; }
        public double? Value { get; set; }
    }

    private static (SqliteConnection Connection, SqliteDataContext Context) CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE SemEntity (
  Id INTEGER PRIMARY KEY,
  Score INTEGER NULL,
  Name TEXT NULL,
  CreatedAt TEXT NOT NULL,
  GroupKey INTEGER NULL,
  GroupKey2 TEXT NULL
);
INSERT INTO SemEntity VALUES
  (1, 10, 'a', '2024-01-01T00:00:00', 1, 'x'),
  (2, 10, 'b', '2024-02-01T00:00:00', 1, 'x'),
  (3, 20, 'c', '2024-03-01T00:00:00', 2, 'y'),
  (4, NULL, 'd', '2024-04-01T00:00:00', NULL, 'x'),
  (5, 20, 'e', '2024-05-01T00:00:00', 2, 'y'),
  (6, 30, 'f', '2024-06-01T00:00:00', 3, 'z'),
  (7, 5, 'g', '2024-07-01T00:00:00', NULL, 'y');
CREATE TABLE FloatEntity (
  Id INTEGER PRIMARY KEY,
  Value REAL NULL
);
INSERT INTO FloatEntity VALUES
  (1, 1.5),
  (2, 2.5),
  (3, NULL);";
        cmd.ExecuteNonQuery();
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        return (connection, ctx);
    }

    private static long[] Ids<T>(IEnumerable<T> rows, Func<T, long> id)
        => rows.Select(id).OrderBy(x => x).ToArray();

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None))
            .SqlStmt!.Replace("\r\n", "\n");

    private static int CountMatches(string sql, string pattern)
        => Regex.Matches(sql, pattern, RegexOptions.IgnoreCase).Count;

    // 1. Direction x ties
    [Fact]
    public void Min_One_KeepsTheSmallestValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(7);
        rows[0].Score.Should().Be(5);
    }

    [Fact]
    public void Min_All_KeepsEveryTiedRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        // 10 is tied between ids 1 and 2; the global minimum 5 is not tied.
        var global = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(global, x => x.Id).Should().Equal(7);

        var tied = ctx.From<SemEntity>().Where(x => x.Score == 10)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(tied, x => x.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void Max_One_KeepsTheGreatestValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(6);
        rows[0].Score.Should().Be(30);
    }

    [Fact]
    public void Max_All_KeepsEveryTiedRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var global = ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(global, x => x.Id).Should().Equal(6);

        var tied = ctx.From<SemEntity>().Where(x => x.Score == 20)
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(tied, x => x.Id).Should().Equal(3, 5);
    }

    // 2. Value kinds
    [Fact]
    public void StringValue_BothDirections()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        ctx.From<SemEntity>().SelectWhereMin(x => x.Name, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Name.Should().Be("a");
        ctx.From<SemEntity>().SelectWhereMax(x => x.Name, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Name.Should().Be("g");
    }

    [Fact]
    public void DateTimeValue_BothDirections()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().SelectWhereMin(x => x.CreatedAt, ExtremeRowTies.One).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(1);

        var max = ctx.From<SemEntity>().SelectWhereMax(x => x.CreatedAt, ExtremeRowTies.One).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(7);
    }

    [Fact]
    public void NullableScalar_ExcludesNullValues()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        min.Should().NotContain(x => x.Score == null);
        Ids(min, x => x.Id).Should().Equal(7);

        var max = ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        max.Should().NotContain(x => x.Score == null);
        Ids(max, x => x.Id).Should().Equal(6);
    }

    [Fact]
    public void AnonymousComposite_ComparesLexicographically()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList();
        min.Should().ContainSingle();
        min[0].Should().Match<SemEntity>(x => x.Id == 7 && x.Score == 5 && x.Name == "g");

        var max = ctx.From<SemEntity>().SelectWhereMax(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList();
        max.Should().ContainSingle();
        max[0].Should().Match<SemEntity>(x => x.Id == 6 && x.Score == 30 && x.Name == "f");
    }

    [Fact]
    public void ValueTupleComposite_ComparesLexicographically()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().SelectWhereMin(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(7);

        var max = ctx.From<SemEntity>().SelectWhereMax(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    // 3. Grouping
    [Fact]
    public void GroupedBySingleKey_OnePerGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        // Groups 1, 2, 3 and NULL. Group 3 (score 30) and the NULL group (score 5) are unique winners;
        // groups 1 (10,10) and 2 (20,20) are tied, so the surviving row is one of the tied ids.
        var min = ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One, x => x.GroupKey).ToList();

        min.Should().HaveCount(4);
        Ids(min.Where(x => x.GroupKey == 3 || x.GroupKey == null), x => x.Id).Should().Equal(6, 7);
        min.Single(x => x.GroupKey == 1).Id.Should().BeOneOf(1, 2);
        min.Single(x => x.GroupKey == 2).Id.Should().BeOneOf(3, 5);
    }

    [Fact]
    public void GroupedByCompositeKey_AllKeepsTiedWinnersPerGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        // Groups (1,x) → 10,10; (2,y) → 20,20; (3,z) → 30; (NULL,x) → only the NULL score row, excluded;
        // (NULL,y) → 5. `All` keeps both tied rows of the first two groups.
        var all = ctx.From<SemEntity>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => new { x.GroupKey, x.GroupKey2 }).ToList();

        Ids(all, x => x.Id).Should().Equal(1, 2, 3, 5, 6, 7);
    }

    // 4. NULL semantics
    [Fact]
    public void NullGroupKey_IsItsOwnGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList();

        // Group NULL holds only id 7: id 4 has a NULL score and never wins an extreme.
        Ids(min, x => x.Id).Should().Equal(1, 2, 3, 5, 6, 7);
        min.Should().Contain(x => x.GroupKey == null && x.Id == 7);
    }

    // 5. Input filters
    [Fact]
    public void CapturedFilter_IsAppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        int threshold = 15;
        var rows = ctx.From<SemEntity>().Where(x => x.Score > threshold)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle();
        rows[0].Score.Should().Be(20);
        rows[0].Id.Should().BeOneOf(3, 5);
    }

    [Fact]
    public void ConstantFilter_IsAppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().Where(x => x.Score > 15)
            .SelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    [Fact]
    public void AllWithCapturedFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        Ids(rows, x => x.Id).Should().Equal(3, 5);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1,
            "the input condition belongs to the aggregated source only");
    }

    [Fact]
    public void AllWithConstantFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > 15)
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        Ids(rows, x => x.Id).Should().Equal(6);
        CountMatches(sql, @"Score\s*>\s*15").Should().Be(1,
            "the input condition belongs to the aggregated source only");
    }

    [Fact]
    public void AllGroupedWithFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        // Rows above 15: (3, 20, y), (5, 20, y), (6, 30, z); group y ties at 20, group z wins at 30.
        Ids(rows, x => x.Id).Should().Equal(3, 5, 6);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1);
    }

    [Fact]
    public void CompositeAllWithFilter_RendersThePredicatePerStageAndNotInTheOuterProjection()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        // Above 15 only (20,c), (20,e), (30,f) remain; the composite minimum is (20,'c').
        rows.Should().ContainSingle().Which.Id.Should().Be(3);

        // One aggregate stage per value component, each reading the filtered source; the outer projection
        // must not repeat the predicate.
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(2);
        var outerSelect = sql[..sql.IndexOf("from (", StringComparison.Ordinal)];
        CountMatches(outerSelect, @"Score\s*>\s*\$threshold").Should().Be(0);
    }

    [Fact]
    public void CompositeGroupedAllWithFilter_UsesAggregateJoinAndTwoPredicates()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.GroupKey);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        // Group 2 keeps (20,'c') and drops (20,'e') on the second key; group 3 keeps (30,'f').
        Ids(rows, x => x.Id).Should().Equal(3, 6);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(2,
            "the input condition is applied to both aggregate stages");
        sql.Should().Contain(" join ").And.NotContain("rank()");
    }

    // 6. Projections
    [Fact]
    public void ScalarProjection_ReturnsTheWinningValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var names = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, x => x.Name, ExtremeRowTies.One).ToList();

        names.Should().ContainSingle().Which.Should().Be("g");
    }

    [Fact]
    public void DtoProjection_ReturnsTheWinningRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, x => new Dto { Id = x.Id, Name = x.Name }, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(7);
        rows[0].Name.Should().Be("g");
    }

    [Fact]
    public void ChainedSelect_KeepsTheWinningRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One)
            .Select(x => new { x.Id, x.Name }).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(7);
        rows[0].Name.Should().Be("g");
    }

    // 7. Outer ordering
    [Fact]
    public void OuterOrderThenBy_OrdersTheSurvivingRows()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2)
            .OrderBy(x => x.Id).ThenBy(x => x.Name).ToList();

        // Group x: 10 tied between 1 and 2; group y: 5 at 7; group z: 30 at 6.
        Ids(rows, x => x.Id).Should().Equal(1, 2, 6, 7);
    }

    // 8. Rejection boundaries
    [Fact]
    public void Paging_IsRejected()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        Action act = () => _ = ctx.From<SemEntity>().SelectWhereMin(x => x.Score).Limit(1).ToList();

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void GroupByAndHaving_AreRejected()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        Action grouped = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).SelectWhereMin(x => x.Score).ToList();
        Action having = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).Having(x => x.Score > 1)
            .SelectWhereMin(x => x.Score).ToList();

        grouped.Should().Throw<BuildSqlCommandException>();
        having.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void InMemoryProvider_IsRejected()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => _ = ctx.From<SemEntity>().SelectWhereMin(x => x.Score).ToList();

        act.Should().Throw<NotSupportedException>();
    }

    // 9. Generated SQL structure
    [Fact]
    public void SqlStructure_PerShape()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var oneMin = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One));
        oneMin.Should().Contain("row_number()").And.NotContain(" partition by ").And.Contain("= 1");

        var oneMax = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.One));
        oneMax.Should().Contain(" desc");

        var all = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All));
        all.Should().Contain(" join ");

        var grouped = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One, x => x.GroupKey));
        grouped.Should().Contain("partition by");

        // SQLite advertises null-safe not-distinct comparison and no risky join capabilities, so the
        // nullable group-key `All` shape uses the aggregate+join lowering; a non-nullable group key
        // keeps the same aggregate + join-back shape.
        var nullableGroupAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey));
        nullableGroupAll.Should().Contain(" join ").And.NotContain("rank()");

        var nonNullableGroupAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.Id));
        nonNullableGroupAll.Should().Contain(" join ").And.NotContain("rank(");
    }

    [Fact]
    public void SqlStructure_CompositeAllAggregatesOncePerValueComponent()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var sql = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.Id));

        sql.Should().Contain(" join ").And.NotContain("rank(");
        CountMatches(sql, @"\bmin\(").Should().Be(2, "one aggregate stage per value component");
    }

    /// <summary>
    /// A floating value key under <c>All</c> keeps the rank window: ClickHouse's aggregate and its ordering
    /// disagree about <c>NaN</c>, and the winner set must follow the ordering. The component decides from
    /// the expression alone, because it is built before any dialect is known.
    /// </summary>
    [Fact]
    public void SqlStructure_FloatingValueKeyUnderAllUsesTheRankWindow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var all = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>().SelectWhereMax(x => x.Value, ExtremeRowTies.All));
        all.Should().Contain("rank()").And.NotContain(" join ");

        var groupedAll = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>()
            .SelectWhereMin(x => x.Value, ExtremeRowTies.All, x => x.Id));
        groupedAll.Should().Contain("rank()").And.Contain("partition by").And.NotContain(" join ");

        var one = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>().SelectWhereMax(x => x.Value, ExtremeRowTies.One));
        one.Should().Contain("row_number()");
    }
}

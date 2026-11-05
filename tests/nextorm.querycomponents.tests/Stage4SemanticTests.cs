using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

/// <summary>
/// Behaviour of the proof package's operators, asserted against the fixed fixture below rather than against
/// the compatibility operator: two implementations agreeing is not evidence that either is right, and the
/// compatibility operator is scheduled for removal.
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
public class Stage4SemanticTests
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
    public void S4_1_MinOne_KeepsTheSmallestValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(7);
        rows[0].Score.Should().Be(5);
    }

    [Fact]
    public void S4_1_MinAll_KeepsEveryTiedRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var global = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(global, x => x.Id).Should().Equal(7);

        var tied = ctx.From<SemEntity>().Where(x => x.Score == 10)
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(tied, x => x.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void S4_1_MaxOne_KeepsTheGreatestValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(6);
        rows[0].Score.Should().Be(30);
    }

    [Fact]
    public void S4_1_MaxAll_KeepsEveryTiedRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var global = ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(global, x => x.Id).Should().Equal(6);

        var tied = ctx.From<SemEntity>().Where(x => x.Score == 20)
            .ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(tied, x => x.Id).Should().Equal(3, 5);
    }

    // 2. Value kinds
    [Fact]
    public void S4_2_StringValue_BothDirections()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Name, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Name.Should().Be("a");
        ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.Name, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Name.Should().Be("g");
    }

    [Fact]
    public void S4_2_DateTimeValue_BothDirections()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.CreatedAt, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Id.Should().Be(1);
        ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.CreatedAt, ExtremeRowTies.One)
            .ToList().Should().ContainSingle().Which.Id.Should().Be(7);
    }

    [Fact]
    public void S4_2_NullableScalar_ExcludesNullValues()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        min.Should().NotContain(x => x.Score == null);
        Ids(min, x => x.Id).Should().Equal(7);

        var max = ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        max.Should().NotContain(x => x.Score == null);
        Ids(max, x => x.Id).Should().Equal(6);
    }

    [Fact]
    public void S4_2_AnonymousComposite_ComparesLexicographically()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>().ExternalSelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(7);

        var max = ctx.From<SemEntity>().ExternalSelectWhereMax(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    [Fact]
    public void S4_2_ValueTupleComposite_ComparesLexicographically()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(7);

        var max = ctx.From<SemEntity>()
            .ExternalSelectWhereMax(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    // 3. Grouping
    [Fact]
    public void S4_3_GroupedSingleKey_OnePerGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        // Groups 1 (10,10), 2 (20,20), 3 (30) and NULL (5 at id 7). The tied groups keep one arbitrary row.
        var min = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One, x => x.GroupKey).ToList();

        min.Should().HaveCount(4);
        Ids(min.Where(x => x.GroupKey == 3 || x.GroupKey == null), x => x.Id).Should().Equal(6, 7);
        min.Single(x => x.GroupKey == 1).Id.Should().BeOneOf(1, 2);
        min.Single(x => x.GroupKey == 2).Id.Should().BeOneOf(3, 5);
    }

    [Fact]
    public void S4_3_GroupedCompositeKey_AllKeepsTiedWinnersPerGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        // (1,x) → 10,10; (2,y) → 20,20; (3,z) → 30; (NULL,x) → only the NULL score row, excluded;
        // (NULL,y) → 5. `All` keeps both tied rows of the first two groups.
        var all = ctx.From<SemEntity>()
            .ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.All, x => new { x.GroupKey, x.GroupKey2 }).ToList();

        Ids(all, x => x.Id).Should().Equal(1, 2, 3, 5, 6, 7);
    }

    // 4. NULL semantics
    [Fact]
    public void S4_4_NullGroupKey_IsItsOwnGroup()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var min = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList();

        Ids(min, x => x.Id).Should().Equal(1, 2, 3, 5, 6, 7);
        min.Should().Contain(x => x.GroupKey == null && x.Id == 7);
    }

    // 5. Input filters
    [Fact]
    public void S4_5_CapturedFilter_IsAppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        int threshold = 15;
        var rows = ctx.From<SemEntity>().Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle();
        rows[0].Score.Should().Be(20);
        rows[0].Id.Should().BeOneOf(3, 5);
    }

    [Fact]
    public void S4_5_ConstantFilter_IsAppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().Where(x => x.Score > 15)
            .ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    [Fact]
    public void S4_5_AllCapturedFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        Ids(rows, x => x.Id).Should().Equal(3, 5);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1,
            "the input condition belongs to the aggregated source only");
    }

    [Fact]
    public void S4_5_AllConstantFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > 15)
            .ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        Ids(rows, x => x.Id).Should().Equal(6);
        CountMatches(sql, @"Score\s*>\s*15").Should().Be(1);
    }

    [Fact]
    public void S4_5_AllGroupedFilter_RendersThePredicateOnce()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        // Above 15: (3, 20, y), (5, 20, y), (6, 30, z); group y ties at 20, group z wins at 30.
        Ids(rows, x => x.Id).Should().Equal(3, 5, 6);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1);
    }

    [Fact]
    public void S4_5_AllCompositeFilter_RendersThePredicatePerStageOnly()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        rows.Should().ContainSingle().Which.Id.Should().Be(3);
        // The rank-window shape reads the filtered source once; the outer projection never repeats it.
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1);
        var outerSelect = sql[..sql.IndexOf("from (", StringComparison.Ordinal)];
        CountMatches(outerSelect, @"Score\s*>\s*\$threshold").Should().Be(0);
    }

    [Fact]
    public void S4_5_AllCompositeGroupedFilter_UsesTheRankWindow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var cmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.GroupKey);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var rows = ctx.ToList(prepared);

        Ids(rows, x => x.Id).Should().Equal(3, 6);
        CountMatches(sql, @"Score\s*>\s*\$threshold").Should().Be(1);
        sql.Should().Contain("rank()").And.NotContain(" join ");
    }

    // 6. Projections
    [Fact]
    public void S4_6_ScalarProjection_ReturnsTheWinningValue()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var names = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, x => x.Name, ExtremeRowTies.One).ToList();

        names.Should().ContainSingle().Which.Should().Be("g");
    }

    [Fact]
    public void S4_6_DtoProjection_ReturnsTheWinningRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, x => new Dto { Id = x.Id, Name = x.Name }, ExtremeRowTies.One).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(7);
        rows[0].Name.Should().Be("g");
    }

    [Fact]
    public void S4_6_ChainedSelect_KeepsTheWinningRow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One)
            .Select(x => new { x.Id, x.Name }).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(7);
        rows[0].Name.Should().Be("g");
    }

    // 7. Outer ordering
    [Fact]
    public void S4_7_OuterOrderThenBy_OrdersTheSurvivingRows()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        var rows = ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2)
            .OrderBy(x => x.Id).ThenBy(x => x.Name).ToList();

        Ids(rows, x => x.Id).Should().Equal(1, 2, 6, 7);
    }

    // 8. Rejection boundaries
    [Fact]
    public void S4_8_Paging_IsRejected()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        Action act = () => _ = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score).Limit(1).ToList();

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void S4_8_GroupByAndHaving_AreRejected()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        Action grouped = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).ExternalSelectWhereMin(x => x.Score).ToList();
        Action having = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).Having(x => x.Score > 1)
            .ExternalSelectWhereMin(x => x.Score).ToList();

        grouped.Should().Throw<BuildSqlCommandException>();
        having.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void S4_8_InMemoryProvider_IsRejected()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => _ = ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score).ToList();

        act.Should().Throw<NotSupportedException>();
    }

    // 9. Generated SQL structure
    [Fact]
    public void S4_9_SqlStructure_PerShape()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var oneMin = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One));
        oneMin.Should().Contain("row_number()").And.NotContain(" partition by ").And.Contain("= 1");

        var oneMax = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().ExternalSelectWhereMax(x => x.Score, ExtremeRowTies.One));
        oneMax.Should().Contain(" desc");

        // This proof package is rank-window only: every `All` shape keeps the window and filters on it.
        var all = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All));
        all.Should().Contain("rank()").And.NotContain(" join ");

        var grouped = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One, x => x.GroupKey));
        grouped.Should().Contain("partition by");

        var nullableGroupAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey));
        nullableGroupAll.Should().Contain("rank()").And.NotContain(" join ");

        var nonNullableGroupAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.Id));
        nonNullableGroupAll.Should().Contain("rank()").And.Contain("partition by").And.NotContain(" join ");
    }

    [Fact]
    public void S4_9_CompositeAllKeepsTheRankWindow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var sql = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .ExternalSelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.Id));

        // The proof package never joins an aggregate back: it ranks on both value keys and partitions on
        // the group key, so a composite key costs no extra stage.
        sql.Should().Contain("rank()").And.Contain("partition by").And.NotContain(" join ");
        CountMatches(sql, @"\bmin\(").Should().Be(0);
    }

    /// <summary>
    /// A floating value key under <c>All</c> keeps the rank window: ClickHouse's aggregate and its ordering
    /// disagree about <c>NaN</c>, and the winner set must follow the ordering.
    /// </summary>
    [Fact]
    public void S4_9_FloatingValueKeyUnderAllUsesTheRankWindow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var all = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>()
            .ExternalSelectWhereMax(x => x.Value, ExtremeRowTies.All));
        all.Should().Contain("rank()").And.NotContain(" join ");

        var groupedAll = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>()
            .ExternalSelectWhereMin(x => x.Value, ExtremeRowTies.All, x => x.Id));
        groupedAll.Should().Contain("rank()").And.Contain("partition by").And.NotContain(" join ");

        var one = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>()
            .ExternalSelectWhereMax(x => x.Value, ExtremeRowTies.One));
        one.Should().Contain("row_number()");
    }
}

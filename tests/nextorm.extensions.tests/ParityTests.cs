using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class ParityTests
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

    private static void AssertSameIds<T>(IEnumerable<T> builtIn, IEnumerable<T> external, Func<T, long> id)
        => Ids(external, id).Should().Equal(Ids(builtIn, id));

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None))
            .SqlStmt!.Replace("\r\n", "\n");

    // 1. Direction x Ties
    [Fact]
    public void S4_1_MinOne_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One).ToList();
        AssertSameIds(built, ext, x => x.Id);
    }

    [Fact]
    public void S4_1_MinAll_Parity_Multiset()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(ext, x => x.Id).Should().Equal(Ids(built, x => x.Id));
    }

    [Fact]
    public void S4_1_MaxOne_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.One).ToList();
        AssertSameIds(built, ext, x => x.Id);
    }

    [Fact]
    public void S4_1_MaxAll_Parity_Multiset()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All).ToList();
        Ids(ext, x => x.Id).Should().Equal(Ids(built, x => x.Id));
    }

    // 2. Value kinds
    [Fact]
    public void S4_2_StringValue_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Name, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Name, ExtremeRowTies.One).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.Name, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Name, ExtremeRowTies.One).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_2_DateTimeValue_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.CreatedAt, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.CreatedAt, ExtremeRowTies.One).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.CreatedAt, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.CreatedAt, ExtremeRowTies.One).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_2_NullableScalar_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.One).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_2_AnonymousComposite_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => new { x.Score, x.Name }, ExtremeRowTies.One).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_2_ValueTupleComposite_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => new ValueTuple<int?, string?>(x.Score, x.Name), ExtremeRowTies.One).ToList(), x => x.Id);
    }

    // 3. Grouping
    [Fact]
    public void S4_3_GroupedSingleKey_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One, x => x.GroupKey).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One, x => x.GroupKey).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_3_GroupedCompositeKey_BothDirections_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One, x => new { x.GroupKey, x.GroupKey2 }).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One, x => new { x.GroupKey, x.GroupKey2 }).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => new { x.GroupKey, x.GroupKey2 }).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All, x => new { x.GroupKey, x.GroupKey2 }).ToList(), x => x.Id);
    }

    // 4. NULL semantics
    [Fact]
    public void S4_4_NullValueKey_MinMax_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All).ToList(), x => x.Id);
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList(),
            ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All).ToList(), x => x.Id);
    }

    [Fact]
    public void S4_4_NullGroupKey_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        AssertSameIds(ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList(),
            ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All, x => x.GroupKey).ToList(), x => x.Id);
    }

    // 5. Input filter
    [Fact]
    public void S4_5_CapturedFilter_AppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        int threshold = 15;
        var built = ctx.From<SemEntity>().Where(x => x.Score > threshold).SelectWhereMin(x => x.Score, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().Where(x => x.Score > threshold).SelectWhereMinEx(x => x.Score, ExtremeRowTies.One).ToList();
        AssertSameIds(built, ext, x => x.Id);
        ext.Should().ContainSingle();
        ext[0].Score.Should().Be(20);
    }

    [Fact]
    public void S4_5_ConstantFilter_AppliedBeforeRanking()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().Where(x => x.Score > 15).SelectWhereMax(x => x.Score, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().Where(x => x.Score > 15).SelectWhereMaxEx(x => x.Score, ExtremeRowTies.One).ToList();
        AssertSameIds(built, ext, x => x.Id);
        ext.Should().ContainSingle();
        ext[0].Score.Should().Be(30);
    }

    // 5b. `All` + an input filter: the predicate belongs to the aggregated source only and must not
    // render a second time around the reshape (the extra parameters were the audit's C4 finding).
    [Fact]
    public void S4_5_AllCapturedFilter_PredicateOnce_AndParity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var built = ctx.From<SemEntity>().Where(x => x.Score > threshold).SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToList();
        var extCmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(extCmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var ext = ctx.ToList(prepared);

        AssertSameIds(built, ext, x => x.Id);
        ext.Should().NotBeEmpty();
        CountPredicate(sql, @"Score\s*>\s*\$threshold").Should().Be(1, "the input condition is applied inside the aggregated source exactly once");
    }

    [Fact]
    public void S4_5_AllConstantFilter_PredicateOnce_AndParity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var built = ctx.From<SemEntity>().Where(x => x.Score > 15).SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToList();
        var extCmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > 15)
            .SelectWhereMaxEx(x => x.Score, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(extCmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var ext = ctx.ToList(prepared);

        AssertSameIds(built, ext, x => x.Id);
        ext.Should().NotBeEmpty();
        CountPredicate(sql, @"Score\s*>\s*15").Should().Be(1, "the input condition is applied inside the aggregated source exactly once");
    }

    [Fact]
    public void S4_5_AllGroupedFilter_PredicateOnce_AndParity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var built = ctx.From<SemEntity>().Where(x => x.Score > threshold).SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2).ToList();
        var extCmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => x.Score, ExtremeRowTies.All, x => x.GroupKey2);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(extCmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var ext = ctx.ToList(prepared);

        AssertSameIds(built, ext, x => x.Id);
        ext.Should().NotBeEmpty();
        CountPredicate(sql, @"Score\s*>\s*\$threshold").Should().Be(1, "the input condition is applied inside the aggregated source exactly once");
    }

    [Fact]
    public void S4_5_AllCompositeFilter_Parity_OuterProjectionFreeOfPredicate()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        var built = ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All)
            .ToList();
        var extCmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => new { x.Score, x.Name }, ExtremeRowTies.All);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(extCmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var ext = ctx.ToList(prepared);

        AssertSameIds(built, ext, x => x.Id);
        ext.Should().NotBeEmpty();

        // The composite `All` lowering aggregates one value component per stage; every stage reads the
        // raw source, so the filter is required in each stage (the built-in lowering documents the same
        // n+1 shape). The outer projection must not repeat it.
        CountPredicate(sql, @"Score\s*>\s*\$threshold").Should().Be(2);
        var outerSelect = sql[..sql.IndexOf("from (", StringComparison.Ordinal)];
        CountPredicate(outerSelect, @"Score\s*>\s*\$threshold").Should().Be(0);
    }

    [Fact]
    public void S4_5_AllCompositeGroupedFilter_PredicateOnce_AndParity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        int threshold = 15;
        // SQLite advertises null-safe not-distinct comparison and no risky join capabilities, so the
        // nullable group-key `All` shape uses aggregate+join; each composite stage applies the filter.
        var built = ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.GroupKey)
            .ToList();
        var extCmd = (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.GroupKey);
        var prepared = (DbPreparedQueryCommand<SemEntity>)ctx.GetPreparedQueryCommand(extCmd, false, true, CancellationToken.None);
        var sql = prepared.SqlStmt!.Replace("\r\n", "\n");
        var ext = ctx.ToList(prepared);

        AssertSameIds(built, ext, x => x.Id);
        ext.Should().NotBeEmpty();
        CountPredicate(sql, @"Score\s*>\s*\$threshold").Should().Be(2, "the input condition is applied to both aggregate stages");
        sql.Should().Contain(" join ").And.NotContain("rank()");
    }

    private static int CountPredicate(string sql, string pattern)
        => Regex.Matches(sql, pattern, RegexOptions.IgnoreCase).Count;

    // 6. Projection
    [Fact]
    public void S4_6_ScalarProjection_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, x => x.Name, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, x => x.Name, ExtremeRowTies.One).ToList();
        ext.Should().Equal(built);
    }

    [Fact]
    public void S4_6_DtoProjection_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, x => new Dto { Id = x.Id, Name = x.Name }, ExtremeRowTies.One).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, x => new Dto { Id = x.Id, Name = x.Name }, ExtremeRowTies.One).ToList();
        ext.Select(x => (x.Id, x.Name)).Should().Equal(built.Select(x => (x.Id, x.Name)));
    }

    [Fact]
    public void S4_6_ChainedSelect_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One).Select(x => new { x.Id, x.Name }).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One).Select(x => new { x.Id, x.Name }).ToList();
        ext.Select(x => (x.Id, x.Name)).Should().Equal(built.Select(x => (x.Id, x.Name)));
    }

    // 7. Outer order and paging
    [Fact]
    public void S4_7_OuterOrderThenBy_Parity()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        var built = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.All).OrderBy(x => x.Score).ThenBy(x => x.Name).ToList();
        var ext = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All).OrderBy(x => x.Score).ThenBy(x => x.Name).ToList();
        ext.Select(x => x.Id).Should().Equal(built.Select(x => x.Id));
    }

    [Fact]
    public void S4_7_Paging_ParityWithBuiltInRejection()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        Action built = () => _ = ctx.From<SemEntity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One).Limit(1).ToList();
        Action ext = () => _ = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One).Limit(1).ToList();
        var b = Record.Exception(built);
        var e = Record.Exception(ext);
        b.Should().NotBeNull();
        e.Should().NotBeNull();
        e!.GetType().Should().Be(b!.GetType());
    }

    // 8. Rejection boundaries
    [Fact]
    public void S4_8_UnsupportedShapes_MatchBuiltInRejections()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;

        Action builtGroup = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).SelectWhereMin(x => x.Score).ToList();
        Action extGroup = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).SelectWhereMinEx(x => x.Score).ToList();
        Record.Exception(builtGroup)!.GetType().Should().Be(Record.Exception(extGroup)!.GetType());

        Action builtHaving = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).Having(x => x.Score > 1).SelectWhereMin(x => x.Score).ToList();
        Action extHaving = () => _ = ctx.From<SemEntity>().GroupBy(x => x.GroupKey).Having(x => x.Score > 1).SelectWhereMinEx(x => x.Score).ToList();
        Record.Exception(builtHaving)!.GetType().Should().Be(Record.Exception(extHaving)!.GetType());
    }

    [Fact]
    public void S4_8_InMemory_Rejected()
    {
        using var ctx = new InMemoryDataContext();
        Action act = () => _ = ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score).ToList();
        act.Should().Throw<NotSupportedException>();
    }

    // 9. Generated SQL structure
    [Fact]
    public void S4_9_SqlStructure_Axes()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var oneMin = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One));
        oneMin.Should().Contain("row_number()");
        oneMin.Should().NotContain(" partition by ");
        oneMin.Should().Contain("= 1");

        var oneMax = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMaxEx(x => x.Score, ExtremeRowTies.One));
        oneMax.Should().Contain(" desc");

        var all = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All));
        all.Should().Contain("join");

        var grouped = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One, x => x.GroupKey));
        grouped.Should().Contain("partition by");

        // `All` picks shape using public capability flags: SQLite's null-safe comparison and join
        // capabilities permit the null-safe aggregate+join; floating values still use rank-window.
        var groupedNullableAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All, x => x.GroupKey));
        groupedNullableAll.Should().Contain(" join ").And.NotContain("rank()");

        var groupedNonNullableAll = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.All, x => x.Id));
        groupedNonNullableAll.Should().Contain(" join ").And.NotContain("rank(");
    }

    [Fact]
    public void S4_9_CompositeAllSqlTopology_MatchesLegacyStagedShape()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn;
        using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var legacy = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMin(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.Id));
        ctx.PurgeQueryCache();
        var external = SqlOf(ctx, (QueryCommand<SemEntity>)ctx.From<SemEntity>()
            .SelectWhereMinEx(x => new { x.Score, x.Name }, ExtremeRowTies.All, x => x.Id));

        legacy.Should().Contain(" join ");
        external.Should().Contain(" join ");
        Regex.Matches(external, "\\bmin\\(", RegexOptions.IgnoreCase).Count
            .Should().Be(Regex.Matches(legacy, "\\bmin\\(", RegexOptions.IgnoreCase).Count);
        Regex.Matches(external, "\\bjoin\\b", RegexOptions.IgnoreCase).Count
            .Should().Be(Regex.Matches(legacy, "\\bjoin\\b", RegexOptions.IgnoreCase).Count);
    }

    /// <summary>
    /// A floating value key under <c>All</c> keeps the rank window instead of the aggregate + join-back
    /// shape: ClickHouse's aggregate and its ordering disagree about <c>NaN</c> (the server returns NaN
    /// as the maximum while ORDER BY ranks it last), and the winner set must follow the ordering. The
    /// component decides by the expression alone, because it is built before any dialect is known.
    /// </summary>
    [Fact]
    public void S4_9_FloatingValueKey_All_UsesTheRankWindow()
    {
        var (conn, ctx) = CreateContext();
        using var connScope = conn; using var ctxScope = ctx;
        ctx.PurgeQueryCache();

        var all = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>().SelectWhereMaxEx(x => x.Value, ExtremeRowTies.All));
        all.Should().Contain("rank()").And.NotContain(" join ");

        var groupedAll = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>().SelectWhereMinEx(x => x.Value, ExtremeRowTies.All, x => x.Id));
        groupedAll.Should().Contain("rank()").And.Contain("partition by").And.NotContain(" join ");

        // One keeps its single-survivor window, unchanged.
        var one = SqlOf(ctx, (QueryCommand<FloatEntity>)ctx.From<FloatEntity>().SelectWhereMaxEx(x => x.Value, ExtremeRowTies.One));
        one.Should().Contain("row_number()");
    }
}

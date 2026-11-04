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
  (7, 5, 'g', '2024-07-01T00:00:00', NULL, 'y');";
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
    }
}

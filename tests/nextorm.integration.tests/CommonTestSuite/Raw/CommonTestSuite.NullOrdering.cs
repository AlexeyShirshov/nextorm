using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// The T287 live fixture row: <c>id</c>, the nullable integer sort key <c>k</c> and the nullable string
/// <c>txt</c> used for reference-type parity. The physical table is created and reseeded per scenario by
/// <see cref="T287NullOrderingFixture"/>; the entity is only read through LINQ.
/// </summary>
[SqlTable("t287_null_order")]
internal interface IT287NullOrderRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? Key { get; set; }

    [Column("txt")]
    string? Text { get; set; }
}

/// <summary>
/// T287 (issue #287): the live acceptance matrix for explicit NULL placement — V01–V24 plus the
/// nullable-string, all-null, no-null, empty and duplicate-key variants. The assertions are made on the
/// returned rows, never on SQL text.
/// <para>
/// Every fact first reseeds the dedicated <c>t287_null_order</c> table with deliberately unsorted keys
/// (<c>NULL, 2, NULL, -1, 1</c>) and reads with the secondary <c>id</c> ascending key, so NULL ties are
/// deterministic. The <c>Default</c> placement is asserted against the pre-fix recon-observed order and
/// every explicit cell is proven to either change that order or reproduce it exactly when it coincides
/// with the provider default.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void T287NullOrdering_LiveMatrix_ShouldMatchThePortableOracle()
        => T287NullOrderingFixture.AssertLiveMatrix(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveNullableStringParity_ShouldMatchTheIntegerShape()
        => T287NullOrderingFixture.AssertNullableStringParity(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveAllNullKeys_ShouldFallBackToTheSecondaryOrder()
        => T287NullOrderingFixture.AssertAllNullKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveNoNullKeys_ShouldOrderValuesByDirection()
        => T287NullOrderingFixture.AssertNoNullKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveEmptyTable_ShouldReturnNoRows()
        => T287NullOrderingFixture.AssertEmptyTable(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveDuplicateKeys_ShouldBreakTiesByTheSecondaryKey()
        => T287NullOrderingFixture.AssertDuplicateKeys(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveParameterizedKey_ShouldBindTheRepeatedCaseOccurrence()
        => T287NullOrderingFixture.AssertParameterizedKey(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LivePerParentRank_ShouldPlaceEachPartitionsNullsAsRequested()
        => T287NullOrderingFixture.AssertPerParentRankPlacement(_sut.DataProvider, Provider.Name);

    [Fact]
    public void T287NullOrdering_LiveExtremeRow_ShouldPlaceTheSurvivorsNullsAsRequested()
        => T287NullOrderingFixture.AssertExtremeRowPlacement(_sut.DataProvider, Provider.Name);
}

/// <summary>
/// The cross-provider fixture and assertion bodies of the T287 live matrix. Each provider owns its own
/// database and its own <c>t287_null_order</c> table; the fixture only creates and reseeds it, and every
/// assertion is the frozen portable oracle.
/// </summary>
internal static class T287NullOrderingFixture
{
    /// <summary>The fixture table (one per provider database).</summary>
    internal const string Table = "t287_null_order";

    /// <summary>
    /// The recon rows, inserted unsorted: <c>NULL, 2, NULL, -1, 1</c> with texts <c>NULL, "b", NULL,
    /// "a", "c"</c> so the string parity mirrors the integer shape.
    /// </summary>
    private const string MainRows = "(1, null, null), (2, 2, 'b'), (3, null, null), (4, -1, 'a'), (5, 1, 'c')";

    private static readonly int?[] AscFirstKeys = [null, null, -1, 1, 2];
    private static readonly int?[] AscLastKeys = [-1, 1, 2, null, null];
    private static readonly int?[] DescFirstKeys = [null, null, 2, 1, -1];
    private static readonly int?[] DescLastKeys = [2, 1, -1, null, null];

    private static readonly string?[] AscFirstTexts = [null, null, "a", "b", "c"];
    private static readonly string?[] AscLastTexts = ["a", "b", "c", null, null];
    private static readonly string?[] DescFirstTexts = [null, null, "c", "b", "a"];
    private static readonly string?[] DescLastTexts = ["c", "b", "a", null, null];

    private static readonly object Gate = new();
    private static readonly HashSet<string> Created = new();

    /// <summary>The whole live matrix: four direction/placement cells, the Default control and the
    /// per-cell proof that the placement actually moved the rows relative to that provider's default.</summary>
    internal static void AssertLiveMatrix(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, MainRows);

        var defaultAsc = ReadKeys(ctx, OrderDirection.Asc, NullsPosition.Default);
        var defaultDesc = ReadKeys(ctx, OrderDirection.Desc, NullsPosition.Default);

        // Control: the pre-fix behaviour must not have moved (AC2 / the recon baseline).
        defaultAsc.Should().Equal(DefaultKeys(provider, OrderDirection.Asc),
            $"{provider}: the Default ASC placement must keep the recon-observed provider default");
        defaultDesc.Should().Equal(DefaultKeys(provider, OrderDirection.Desc),
            $"{provider}: the Default DESC placement must keep the recon-observed provider default");

        var ascFirst = ReadKeys(ctx, OrderDirection.Asc, NullsPosition.First);
        var ascLast = ReadKeys(ctx, OrderDirection.Asc, NullsPosition.Last);
        var descFirst = ReadKeys(ctx, OrderDirection.Desc, NullsPosition.First);
        var descLast = ReadKeys(ctx, OrderDirection.Desc, NullsPosition.Last);

        ascFirst.Should().Equal(AscFirstKeys, $"{provider}: ASC/FIRST expected NULL,NULL,-1,1,2");
        ascLast.Should().Equal(AscLastKeys, $"{provider}: ASC/LAST expected -1,1,2,NULL,NULL");
        descFirst.Should().Equal(DescFirstKeys, $"{provider}: DESC/FIRST expected NULL,NULL,2,1,-1");
        descLast.Should().Equal(DescLastKeys, $"{provider}: DESC/LAST expected 2,1,-1,NULL,NULL");

        AssertMoved(provider, OrderDirection.Asc, NullsPosition.First, ascFirst, defaultAsc);
        AssertMoved(provider, OrderDirection.Asc, NullsPosition.Last, ascLast, defaultAsc);
        AssertMoved(provider, OrderDirection.Desc, NullsPosition.First, descFirst, defaultDesc);
        AssertMoved(provider, OrderDirection.Desc, NullsPosition.Last, descLast, defaultDesc);
    }

    /// <summary>Nullable-reference parity: the string keys produce the same four shapes as the integers.</summary>
    internal static void AssertNullableStringParity(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, MainRows);

        ReadTexts(ctx, OrderDirection.Asc, NullsPosition.First).Should().Equal(AscFirstTexts,
            $"{provider}: ASC/FIRST text parity");
        ReadTexts(ctx, OrderDirection.Asc, NullsPosition.Last).Should().Equal(AscLastTexts,
            $"{provider}: ASC/LAST text parity");
        ReadTexts(ctx, OrderDirection.Desc, NullsPosition.First).Should().Equal(DescFirstTexts,
            $"{provider}: DESC/FIRST text parity");
        ReadTexts(ctx, OrderDirection.Desc, NullsPosition.Last).Should().Equal(DescLastTexts,
            $"{provider}: DESC/LAST text parity");
    }

    /// <summary>All-null keys collapse into one NULL group, so the secondary <c>id</c> key decides.</summary>
    internal static void AssertAllNullKeys(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, "(3, null, null), (1, null, null), (2, null, null)");

        ReadIds(ctx, OrderDirection.Desc, NullsPosition.First).Should().Equal([1, 2, 3],
            $"{provider}: every key is NULL, so the secondary id key must order the group");
        ReadIds(ctx, OrderDirection.Asc, NullsPosition.Last).Should().Equal([1, 2, 3],
            $"{provider}: the placement must not disturb an all-NULL group");
    }

    /// <summary>With no NULLs both placements must degrade to the plain direction ordering.</summary>
    internal static void AssertNoNullKeys(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, "(1, 2, null), (2, -1, null), (3, 1, null)");

        ReadKeys(ctx, OrderDirection.Asc, NullsPosition.Last).Should().Equal([-1, 1, 2],
            $"{provider}: ASC/LAST over values only");
        ReadKeys(ctx, OrderDirection.Desc, NullsPosition.First).Should().Equal([2, 1, -1],
            $"{provider}: DESC/FIRST over values only");
    }

    /// <summary>An empty fixture returns no rows for every explicit cell.</summary>
    internal static void AssertEmptyTable(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, string.Empty);

        ReadKeys(ctx, OrderDirection.Asc, NullsPosition.First).Should().BeEmpty($"{provider}: empty ASC/FIRST");
        ReadKeys(ctx, OrderDirection.Asc, NullsPosition.Last).Should().BeEmpty($"{provider}: empty ASC/LAST");
        ReadKeys(ctx, OrderDirection.Desc, NullsPosition.First).Should().BeEmpty($"{provider}: empty DESC/FIRST");
        ReadKeys(ctx, OrderDirection.Desc, NullsPosition.Last).Should().BeEmpty($"{provider}: empty DESC/LAST");
    }

    /// <summary>Duplicate keys keep the secondary <c>id</c> order inside each tie group.</summary>
    internal static void AssertDuplicateKeys(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, "(1, 1, null), (2, null, null), (3, 1, null), (4, null, null)");

        ReadIds(ctx, OrderDirection.Asc, NullsPosition.Last).Should().Equal([1, 3, 2, 4],
            $"{provider}: the value ties keep id order and the NULL tie follows them");
        ReadIds(ctx, OrderDirection.Desc, NullsPosition.First).Should().Equal([2, 4, 1, 3],
            $"{provider}: DESC/FIRST puts the NULL tie first, then the value tie");
    }

    /// <summary>
    /// A captured local inside the ordering key is bound as a parameter instead of being inlined. On the
    /// CASE dialects the emulation emits the key twice, so a missed occurrence would leave the statement
    /// unbound or apply a wrong value; the proof is therefore made on the returned rows, never on SQL
    /// text. A negative factor reverses the value order, so a dropped or mis-bound parameter cannot
    /// accidentally produce the expected sequence.
    /// </summary>
    internal static void AssertParameterizedKey(IDataContext ctx, string provider)
    {
        EnsureTable(ctx, provider);
        Reset(ctx, provider, MainRows);

        // Keys become NULL, -2, NULL, 1, -1: ASC/FIRST is NULL,NULL,-2(id 2),-1(id 5),1(id 4).
        var factor = -1;
        ReadParameterizedIds(ctx, factor, OrderDirection.Asc, NullsPosition.First)
            .Should().Equal([1, 3, 2, 5, 4],
                $"{provider}: the bound key must reverse the values under the NULL block; a plain ASC order would give [1,3,4,5,2]");
        ReadParameterizedIds(ctx, factor, OrderDirection.Asc, NullsPosition.Last)
            .Should().Equal([2, 5, 4, 1, 3],
                $"{provider}: the bound key must reverse the values before the NULL block");
    }

    /// <summary>
    /// The expected Default order per provider, taken from the recon capture: SQLite, SQL Server, MySQL
    /// and MariaDB default to NULLS FIRST ascending / LAST descending; PostgreSQL and ClickHouse default
    /// to NULLS LAST ascending (PostgreSQL flips to FIRST descending, ClickHouse stays LAST).
    /// </summary>
    internal static int?[] DefaultKeys(string provider, OrderDirection direction) => (provider, direction) switch
    {
        ("postgres", OrderDirection.Asc) => AscLastKeys,
        ("postgres", OrderDirection.Desc) => DescFirstKeys,
        ("clickhouse", OrderDirection.Asc) => AscLastKeys,
        ("clickhouse", OrderDirection.Desc) => DescLastKeys,
        (_, OrderDirection.Asc) => AscFirstKeys,
        _ => DescLastKeys
    };

    /// <summary>The recon-observed default NULL side of a provider and direction.</summary>
    internal static string DefaultSide(string provider, OrderDirection direction) => (provider, direction) switch
    {
        ("postgres", OrderDirection.Asc) => "LAST",
        ("postgres", OrderDirection.Desc) => "FIRST",
        ("clickhouse", OrderDirection.Asc) => "LAST",
        ("clickhouse", OrderDirection.Desc) => "LAST",
        (_, OrderDirection.Asc) => "FIRST",
        _ => "LAST"
    };

    /// <summary>
    /// The red→green proof of one cell: an explicit placement that differs from the provider default
    /// must change the returned order; one that coincides with it must reproduce the Default sequence.
    /// </summary>
    private static void AssertMoved(
        string provider,
        OrderDirection direction,
        NullsPosition requested,
        IReadOnlyList<int?> explicitSequence,
        IReadOnlyList<int?> defaultSequence)
    {
        var defaultSide = DefaultSide(provider, direction);
        var requestedSide = requested == NullsPosition.First ? "FIRST" : "LAST";

        if (requestedSide == defaultSide)
        {
            explicitSequence.Should().Equal(defaultSequence,
                $"{provider}: {direction}/{requestedSide} coincides with the provider default, so the rows must match");
        }
        else
        {
            explicitSequence.SequenceEqual(defaultSequence).Should().BeFalse(
                $"{provider}: {direction}/{requestedSide} must change the order relative to the provider default ({defaultSide})");
        }
    }

    private static List<int?> ReadKeys(IDataContext ctx, OrderDirection direction, NullsPosition nullsPosition)
        => ctx.From<IT287NullOrderRow>()
            .OrderBy(x => x.Key, direction, nullsPosition)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Key })
            .ToList()
            .Select(x => x.Key)
            .ToList();

    private static List<string?> ReadTexts(IDataContext ctx, OrderDirection direction, NullsPosition nullsPosition)
        => ctx.From<IT287NullOrderRow>()
            .OrderBy(x => x.Text, direction, nullsPosition)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Text })
            .ToList()
            .Select(x => x.Text)
            .ToList();

    private static List<int> ReadIds(IDataContext ctx, OrderDirection direction, NullsPosition nullsPosition)
        => ctx.From<IT287NullOrderRow>()
            .OrderBy(x => x.Key, direction, nullsPosition)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Id })
            .ToList()
            .Select(x => x.Id)
            .ToList();

    /// <summary>
    /// The same read as <see cref="ReadIds"/>, but with a captured <paramref name="factor"/> inside the
    /// ordering key so the key is a bound parameter (and, on the CASE dialects, a repeated one).
    /// </summary>
    private static List<int> ReadParameterizedIds(
        IDataContext ctx,
        int factor,
        OrderDirection direction,
        NullsPosition nullsPosition)
        => ctx.From<IT287NullOrderRow>()
            .OrderBy(x => x.Key * factor, direction, nullsPosition)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Id })
            .ToList()
            .Select(x => x.Id)
            .ToList();

    // --- V30: per-parent rank ordering through the eager per-parent top-N path -----------------------

    /// <summary>
    /// The four (direction, placement) cells of the per-parent ranking fixture with the per-parent id
    /// sequences a page of three must return. Parent 1 owns five children keyed <c>NULL, 2, NULL, -1, 1</c>
    /// (inserted out of order) and the page cuts into that sequence, so a placement lost between the rank
    /// window and the result changes the surviving ids; parent 2 owns <c>NULL, 1</c> and is ranked on its
    /// own partition.
    /// </summary>
    private static readonly
        (OrderDirection Direction, NullsPosition Placement, int[] Parent1, int[] Parent2)[] RankCells =
    [
        (OrderDirection.Asc, NullsPosition.First, [11, 13, 14], [21, 22]),
        (OrderDirection.Asc, NullsPosition.Last, [14, 15, 12], [22, 21]),
        (OrderDirection.Desc, NullsPosition.First, [11, 13, 12], [21, 22]),
        (OrderDirection.Desc, NullsPosition.Last, [12, 15, 14], [22, 21]),
    ];

    /// <summary>
    /// V30: the per-parent rank lowering (<c>row_number() over (partition by &lt;child key&gt; order by
    /// ...)</c>) must honour an explicit NULL placement. Asserted on the rows - the surviving child ids of
    /// each partition's page - and proven against the provider's own Default order, so a placement that
    /// coincides with the default must reproduce it and one that differs must move the NULLs.
    /// </summary>
    internal static void AssertPerParentRankPlacement(IDataContext ctx, string provider)
    {
        EnsureRankTables(ctx, provider);
        ResetRankRows(ctx, provider);

        var defaultPage = LoadRankPage(ctx, OrderDirection.Asc, NullsPosition.Default);

        foreach (var (direction, placement, parent1, parent2) in RankCells)
        {
            var page = LoadRankPage(ctx, direction, placement);

            page.Should().HaveCount(2, $"{provider}: both parents must stay in the result");
            page[0].Children.Select(c => c.Id).Should().Equal(parent1,
                $"{provider}: {direction}/{placement} must place the NULL keys of partition 1 on the requested "
                + "side of its ranked page");
            page[1].Children.Select(c => c.Id).Should().Equal(parent2,
                $"{provider}: {direction}/{placement} must rank partition 2 independently");

            var requestedSide = placement == NullsPosition.First ? "FIRST" : "LAST";
            if (direction == OrderDirection.Asc && requestedSide == DefaultSide(provider, direction))
                page[0].Children.Select(c => c.Id).Should().Equal(defaultPage[0].Children.Select(c => c.Id),
                    $"{provider}: ASC/{requestedSide} coincides with the per-parent default, so the page must match");
            else if (direction == OrderDirection.Asc)
                page[0].Children.Select(c => c.Id).SequenceEqual(defaultPage[0].Children.Select(c => c.Id))
                    .Should().BeFalse($"{provider}: ASC/{requestedSide} must move the NULLs relative to the "
                        + $"provider default ({DefaultSide(provider, direction)})");
        }
    }

    /// <summary>Loads the two-parent fixture with each parent's children ranked by (k placement, id), page 3.</summary>
    private static List<T287RankParentRow> LoadRankPage(IDataContext ctx, OrderDirection direction, NullsPosition placement)
        => ctx.From<T287RankParentRow>()
            .LoadWith(
                p => p.Children,
                c => c.From<T287RankChildRow>()
                    .OrderBy(x => x.Key, direction, placement)
                    .ThenBy(x => x.Id)
                    .Limit(3),
                p => p.Id,
                c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

    // --- V31: extreme-row outer ordering ------------------------------------------------------------

    private static readonly int?[] ExtremeAscFirstKeys = [null, null, -1, 1, 2];
    private static readonly int?[] ExtremeAscLastKeys = [-1, 1, 2, null, null];
    private static readonly int?[] ExtremeDescFirstKeys = [null, null, 2, 1, -1];
    private static readonly int?[] ExtremeDescLastKeys = [2, 1, -1, null, null];

    /// <summary>
    /// V31: the extreme-row lowering (<c>SelectWhereMax</c> with <c>ExtremeRowTies.All</c>, the portable
    /// rank-derived path every provider takes) orders its survivors on the outer statement. An explicit
    /// placement there must place the NULL keys of the outer key on the requested side; the assertion is
    /// made on the returned rows, and the Default cell is the provider's own order.
    /// </summary>
    internal static void AssertExtremeRowPlacement(IDataContext ctx, string provider)
    {
        EnsureRankTables(ctx, provider);
        ResetRankRows(ctx, provider);

        var asc = ReadExtremeKeys(ctx, OrderDirection.Asc, NullsPosition.First);
        var ascLast = ReadExtremeKeys(ctx, OrderDirection.Asc, NullsPosition.Last);
        var desc = ReadExtremeKeys(ctx, OrderDirection.Desc, NullsPosition.First);
        var descLast = ReadExtremeKeys(ctx, OrderDirection.Desc, NullsPosition.Last);

        asc.Should().Equal(ExtremeAscFirstKeys, $"{provider}: extreme-row ASC/FIRST expected NULL,NULL,-1,1,2");
        ascLast.Should().Equal(ExtremeAscLastKeys, $"{provider}: extreme-row ASC/LAST expected -1,1,2,NULL,NULL");
        desc.Should().Equal(ExtremeDescFirstKeys, $"{provider}: extreme-row DESC/FIRST expected NULL,NULL,2,1,-1");
        descLast.Should().Equal(ExtremeDescLastKeys, $"{provider}: extreme-row DESC/LAST expected 2,1,-1,NULL,NULL");

        var defaultAsc = ReadExtremeKeys(ctx, OrderDirection.Asc, NullsPosition.Default);
        var defaultDesc = ReadExtremeKeys(ctx, OrderDirection.Desc, NullsPosition.Default);
        defaultAsc.Should().Equal(DefaultKeys(provider, OrderDirection.Asc),
            $"{provider}: an unplaced extreme-row outer order must keep the provider default");
        defaultDesc.Should().Equal(DefaultKeys(provider, OrderDirection.Desc),
            $"{provider}: an unplaced extreme-row outer order must keep the provider default");

        AssertMoved(provider, OrderDirection.Asc, NullsPosition.First, asc, defaultAsc);
        AssertMoved(provider, OrderDirection.Asc, NullsPosition.Last, ascLast, defaultAsc);
        AssertMoved(provider, OrderDirection.Desc, NullsPosition.First, desc, defaultDesc);
        AssertMoved(provider, OrderDirection.Desc, NullsPosition.Last, descLast, defaultDesc);
    }

    /// <summary>Runs the extreme-row query (every tied survivor) and returns the outer ordering key.</summary>
    private static List<int?> ReadExtremeKeys(IDataContext ctx, OrderDirection direction, NullsPosition placement)
        => ctx.From<T287ExtremeRow>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .OrderBy(x => x.Key, direction, placement)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Key })
            .ToList()
            .Select(x => x.Key)
            .ToList();

    // --- V30/V31 fixture tables ---------------------------------------------------------------------

    /// <summary>The live per-parent ranking and extreme-row fixture tables, one per provider database.</summary>
    private static void EnsureRankTables(IDataContext ctx, string provider)
    {
        lock (Gate)
        {
            if (!Created.Add($"rank:{provider}"))
                return;

            Exec(ctx, "drop table if exists t287_null_rank_child");
            Exec(ctx, "drop table if exists t287_null_rank_parent");
            Exec(ctx, "drop table if exists t287_null_extreme");

            if (provider == "clickhouse")
            {
                Exec(ctx, "create table t287_null_rank_parent (id Int32, name Nullable(String)) engine = Memory");
                Exec(ctx, "create table t287_null_rank_child (id Int32, parent_id Int32, k Nullable(Int32)) engine = Memory");
                Exec(ctx, "create table t287_null_extreme (id Int32, k Nullable(Int32), score Nullable(Int32)) engine = Memory");
                return;
            }

            Exec(ctx, "create table t287_null_rank_parent (id int not null, name varchar(64) null)");
            Exec(ctx, "create table t287_null_rank_child (id int not null, parent_id int not null, k int null)");
            Exec(ctx, "create table t287_null_extreme (id int not null, k int null, score int null)");
        }
    }

    /// <summary>Reseeds the V30/V31 fixture rows.</summary>
    private static void ResetRankRows(IDataContext ctx, string provider)
    {
        var truncate = provider == "clickhouse";
        foreach (var table in new[] { "t287_null_rank_child", "t287_null_rank_parent", "t287_null_extreme" })
            Exec(ctx, truncate ? $"truncate table {table}" : $"delete from {table}");

        Exec(ctx, "insert into t287_null_rank_parent (id, name) values (1, 'a'), (2, 'b')");
        Exec(ctx,
            "insert into t287_null_rank_child (id, parent_id, k) values" +
            " (13, 1, null), (12, 1, 2), (11, 1, null), (15, 1, 1), (14, 1, -1), (22, 2, 1), (21, 2, null)");
        Exec(ctx,
            "insert into t287_null_extreme (id, k, score) values" +
            " (1, null, 9), (2, 2, 9), (3, null, 9), (4, -1, 9), (5, 1, 9)");
    }

    /// <summary>Creates the fixture table once per provider (the whole test class runs sequentially).</summary>
    private static void EnsureTable(IDataContext ctx, string provider)
    {
        lock (Gate)
        {
            if (!Created.Add(provider))
                return;

            Exec(ctx, $"drop table if exists {Table}");

            var ddl = provider == "clickhouse"
                ? $"create table {Table} (id Int32, k Nullable(Int32), txt Nullable(String)) engine = Memory"
                : $"create table {Table} (id int not null, k int null, txt varchar(64) null)";
            Exec(ctx, ddl);
        }
    }

    /// <summary>
    /// Reseeds the fixture table with the scenario rows (a non-empty list of VALUES tuples). ClickHouse has
    /// no plain <c>DELETE FROM</c> (its lightweight delete requires a condition), so the Memory table is
    /// reset with <c>TRUNCATE</c>; every other provider uses the portable <c>DELETE FROM</c>.
    /// </summary>
    private static void Reset(IDataContext ctx, string provider, string rows)
    {
        Exec(ctx, provider == "clickhouse" ? $"truncate table {Table}" : $"delete from {Table}");

        if (rows.Length > 0)
            Exec(ctx, $"insert into {Table} (id, k, txt) values {rows}");
    }

    private static void Exec(IDataContext ctx, string sql)
    {
        using var _ = ctx.ExecuteRaw(sql);
    }
}

/// <summary>Parent of the V30 per-parent ranking fixture (the LINQ surface is <c>LoadWith</c>).</summary>
[SqlTable("t287_null_rank_parent")]
public sealed class T287RankParentRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T287RankChildRow> Children { get; } = new List<T287RankChildRow>();
}

/// <summary>Paged child of <see cref="T287RankParentRow"/>; <c>k</c> is the nullable rank key.</summary>
[SqlTable("t287_null_rank_child")]
public sealed class T287RankChildRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("k")]
    public int? Key { get; set; }
}

/// <summary>
/// The V31 extreme-row fixture: <c>score</c> selects the survivor set, <c>k</c> carries the NULLs the
/// outer ordering has to place explicitly.
/// </summary>
[SqlTable("t287_null_extreme")]
public sealed class T287ExtremeRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("k")]
    public int? Key { get; set; }

    [Column("score")]
    public int? Score { get; set; }
}

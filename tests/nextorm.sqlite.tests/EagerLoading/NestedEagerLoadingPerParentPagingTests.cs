using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T212-D7 corrective: per-level, independent per-parent top-N on a nested (level 2+) eager graph
/// (variant matrix row B/R4), the nested tie-behavior contract inherited from #211, and the deepest-node
/// (no descendants) per-parent paging probe. All three run on SQLite, the provider whose per-parent
/// ranking is exercised by the #211 suite.
/// </summary>
[Trait("Feature", "T212-D7")]
public sealed class NestedEagerLoadingPerParentPagingTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText);
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-nested-perparent-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table npp_root (id integer primary key);" +
                "create table npp_mid (id integer primary key, root_id int not null, sort_key int not null);" +
                "create table npp_leaf (id integer primary key, mid_id int not null, sort_key int not null, tie_key int not null);" +
                "insert into npp_root (id) values (1), (2), (3);" +
                // Root 1: mids 11 (sort 2), 12 (sort 1). Root 2: mid 21. Root 3: none.
                "insert into npp_mid (id, root_id, sort_key) values (11, 1, 2), (12, 1, 1), (21, 2, 1);" +
                // Every mid has two leaves with distinct sort keys; mid 12 additionally ties its two leaves.
                "insert into npp_leaf (id, mid_id, sort_key, tie_key) values" +
                " (111, 11, 2, 5), (112, 11, 1, 5)," +
                " (121, 12, 1, 5), (122, 12, 2, 5)," +
                " (211, 21, 1, 5), (212, 21, 2, 5);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void Split_TwoSuccessiveLevels_PageIndependentPerParent()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Both the level-1 child (Mid) and the level-2 grandchild (Leaf) page per parent: the two
            // bounds must apply independently, per level, per parent -- not once across the whole graph.
            var roots = ctx.From<NppRoot>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<NppMid>().OrderBy(m => m.SortKey).Limit(1)
                        .LoadWith(m => m.Leaves, l => l.From<NppLeaf>().OrderBy(x => x.SortKey).Limit(1), m => m.Id, l => l.MidId),
                    r => r.Id,
                    m => m.RootId)
                .OrderBy(r => r.Id)
                .ToList();

            roots.Select(r => r.Id).Should().Equal(1, 2, 3);
            roots.Single(r => r.Id == 1).Mids.Select(m => m.Id).Should().Equal(new[] { 12 }, "root 1 keeps only its lowest-sorted mid");
            roots.Single(r => r.Id == 2).Mids.Select(m => m.Id).Should().Equal(21);
            roots.Single(r => r.Id == 3).Mids.Should().BeEmpty();
            roots.Single(r => r.Id == 1).Mids.Single().Leaves.Select(l => l.Id).Should().Equal(new[] { 121 }, "mid 12 keeps only its lowest-sorted leaf");
            roots.Single(r => r.Id == 2).Mids.Single().Leaves.Select(l => l.Id).Should().Equal(new[] { 211 });
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Split_NestedTiedOrder_PerParentPageKeepsCountAndMembershipOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The #211 tie contract: ordering by the all-equal tie_key leaves the surviving row unbounded.
            // Only the per-parent count and the membership set are promised, never a specific identity.
            var roots = ctx.From<NppRoot>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<NppMid>()
                        .LoadWith(m => m.Leaves, l => l.From<NppLeaf>().OrderBy(x => x.TieKey).Limit(1), m => m.Id, l => l.MidId),
                    r => r.Id,
                    m => m.RootId)
                .OrderBy(r => r.Id)
                .ToList();

            var mid12 = roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 12);
            mid12.Leaves.Should().HaveCount(1, "each parent still gets exactly its top-1 leaf under a tie");
            mid12.Leaves.Select(l => l.Id).Should().BeSubsetOf([121, 122], "the surviving tied row is not promised");
            roots.Single(r => r.Id == 2).Mids.Single().Leaves.Should().HaveCount(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_DeepestLeafPaged_IsExpressedPerParentAcrossMultipleParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // The deepest node has no descendants, so its per-parent page is lowered by the flat #211
            // source. It is expressible in single-query mode: every mid (across every root) keeps its own
            // lowest-sorted leaf, and the unpaged level-1 collection is unaffected.
            var roots = ctx.From<NppRoot>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<NppMid>()
                        .LoadWith(m => m.Leaves, l => l.From<NppLeaf>().OrderBy(x => x.SortKey).Limit(1), m => m.Id, l => l.MidId),
                    r => r.Id,
                    m => m.RootId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(r => r.Id)
                .ToList();

            roots.Single(r => r.Id == 1).Mids.Select(m => m.Id).Should().BeEquivalentTo([11, 12]);
            roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 11).Leaves.Select(l => l.Id).Should().Equal(112);
            roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 12).Leaves.Select(l => l.Id).Should().Equal(121);
            roots.Single(r => r.Id == 2).Mids.Single().Leaves.Select(l => l.Id).Should().Equal(211);
            roots.Single(r => r.Id == 3).Mids.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("npp_root")]
public sealed class NppRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<NppMid> Mids { get; } = new List<NppMid>();
}

[SqlTable("npp_mid")]
public sealed class NppMid
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("root_id")]
    public int RootId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    public ICollection<NppLeaf> Leaves { get; } = new List<NppLeaf>();
}

[SqlTable("npp_leaf")]
public sealed class NppLeaf
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mid_id")]
    public int MidId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    [Column("tie_key")]
    public int TieKey { get; set; }
}

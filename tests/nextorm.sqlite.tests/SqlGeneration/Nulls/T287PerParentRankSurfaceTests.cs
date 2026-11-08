using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>Parent of the V30 per-parent ranking fixture (the LINQ surface is <c>LoadWith</c>).</summary>
[SqlTable("t287_rank_parent")]
public sealed class T287RankParentEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T287RankChildEntity> Children { get; } = new List<T287RankChildEntity>();
}

/// <summary>Paged child of <see cref="T287RankParentEntity"/>; <c>k</c> is the nullable rank key.</summary>
[SqlTable("t287_rank_child")]
public sealed class T287RankChildEntity
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
/// V30 through the public eager-load surface: a per-parent top-N child (<c>LoadWith</c> + the child's own
/// <c>Limit</c>) ordered by a nullable key with an explicit NULL placement. The test proves both halves the
/// plan asked for: the ranked derived table the loader builds carries the placement into its
/// <c>row_number()</c> window and its outer order, and the per-partition result ranks the NULLs on the
/// requested side. The row assertions are the oracle - the SQL text is the corroborating structure.
/// </summary>
public sealed class T287PerParentRankSurfaceTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Parent 1 owns five children with keys <c>NULL, 2, NULL, -1, 1</c> (inserted out of order); parent 2
    /// owns <c>NULL, 1</c>. A page of three cuts into parent 1's sequence, so a placement dropped anywhere
    /// between the rank window and the result changes the surviving ids.
    /// </summary>
    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t287-rank-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table t287_rank_parent (id integer primary key, name text);" +
                "create table t287_rank_child (id integer primary key, parent_id int not null, k int null);" +
                "insert into t287_rank_parent (id, name) values (1, 'a'), (2, 'b');" +
                "insert into t287_rank_child (id, parent_id, k) values" +
                " (13, 1, null), (12, 1, 2), (11, 1, null), (15, 1, 1), (14, 1, -1)," +
                " (22, 2, 1), (21, 2, null);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    private static readonly
        (OrderDirection Direction, NullsPosition Placement, string Window, string[] Parent1, string[] Parent2)[] Cells =
    [
        (OrderDirection.Asc, NullsPosition.First, "k NULLS FIRST", ["11", "13", "14"], ["21", "22"]),
        (OrderDirection.Asc, NullsPosition.Last, "k NULLS LAST", ["14", "15", "12"], ["22", "21"]),
        (OrderDirection.Desc, NullsPosition.First, "k desc NULLS FIRST", ["11", "13", "12"], ["21", "22"]),
        (OrderDirection.Desc, NullsPosition.Last, "k desc NULLS LAST", ["12", "15", "14"], ["22", "21"]),
    ];

    [Fact]
    public void T287NullOrdering_PerParentRank_ShouldRankEachPartitionWithTheRequestedPlacement()
    {
        var (ctx, path) = CreateDb();
        var interceptor = new SqlRecordingInterceptor();
        ctx.AddInterceptor(interceptor);
        try
        {
            foreach (var (direction, placement, window, parent1, parent2) in Cells)
            {
                ctx.PurgeQueryCache();
                interceptor.Statements.Clear();

                var parents = ctx.From<T287RankParentEntity>()
                    .LoadWith(
                        p => p.Children,
                        c => c.From<T287RankChildEntity>()
                            .OrderBy(x => x.Key, direction, placement)
                            .ThenBy(x => x.Id)
                            .Limit(3),
                        p => p.Id,
                        c => c.ParentId)
                    .OrderBy(p => p.Id)
                    .ToList();

                parents.Select(p => p.Id).Should().Equal(1, 2);
                parents[0].Children.Select(c => c.Id.ToString()).Should().Equal(parent1,
                    $"the {direction}/{placement} oracle: the ranked page must place the NULLs where the request says");
                parents[1].Children.Select(c => c.Id.ToString()).Should().Equal(parent2,
                    $"the second partition must be ranked independently for {direction}/{placement}");

                // Structure: the child statement is the ranked derived table and both its window and its
                // outer order carry the requested placement (the loader never drops it).
                interceptor.Statements.Should().HaveCount(2, "one parent statement plus one batched child statement");
                var childSql = interceptor.Statements[1];
                childSql.Should().Contain($"over (partition by parent_id order by {window}, id)",
                    $"the rank window must carry {direction}/{placement} for the key and keep the secondary id key");
                childSql.Should().Contain($"order by t1.{window}, t1.id",
                    $"the outer order of the ranked page must carry {direction}/{placement}");
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void T287NullOrdering_PerParentRank_DefaultPlacement_ShouldKeepTheUnchangedWindow()
    {
        var (ctx, path) = CreateDb();
        var interceptor = new SqlRecordingInterceptor();
        ctx.AddInterceptor(interceptor);
        try
        {
            _ = ctx.From<T287RankParentEntity>()
                .LoadWith(
                    p => p.Children,
                    c => c.From<T287RankChildEntity>().OrderBy(x => x.Key).Limit(3),
                    p => p.Id,
                    c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();

            var childSql = interceptor.Statements[1];
            childSql.Should().Contain("over (partition by parent_id order by k)");
            childSql.Should().EndWith("order by t1.k");
            childSql.Should().NotContain("CASE").And.NotContain("NULLS");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

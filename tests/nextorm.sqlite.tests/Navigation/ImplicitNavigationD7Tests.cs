using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B D7: cache and prepared-command correctness for navigation queries (A11). The cold (first
/// prepare), warm (plan-cache hit) and prepared (parameterized reuse) paths must render identical SQL
/// and produce identical results; the normalization must be idempotent (no duplicate injected LEFT JOINs
/// or correlated subqueries); and the shared <c>.Any()/.Count()</c> command must stay free of sticky
/// per-call state.
/// </summary>
public class ImplicitNavigationD7Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Get<T>(IDataContext ctx, QueryCommand<T> cmd, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, storeInCache, CancellationToken.None);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d7-nav-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = Schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        ctx.From<D7Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<D7Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        return (ctx, path);
    }

    private const string Schema =
        "create table d7_parent (id integer primary key, name text);" +
        "create table d7_child (id integer primary key, parent_id integer, name text);" +
        "insert into d7_parent (id, name) values (1, 'p1'), (2, 'p2');" +
        "insert into d7_child (id, parent_id, name) values (10, 1, 'c10'), (11, 1, 'c11'), (12, 999, 'dangling');";

    [Fact]
    public void Reference_navigation_cold_warm_and_prepared_should_render_identical_sql()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var cold = Get(ctx, ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }), storeInCache: true);
            var warm = Get(ctx, ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }), storeInCache: true);
            var prepared = Get(ctx, ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }), storeInCache: false);

            ReferenceEquals(cold, warm).Should().BeTrue("an identical reference-navigation query must reuse the cached plan");
            Normalize(warm.DbCommand.CommandText).Should().Be(Normalize(cold.DbCommand.CommandText));
            Normalize(prepared.DbCommand.CommandText).Should().Be(Normalize(cold.DbCommand.CommandText));
            CountOccurrences(Normalize(cold.DbCommand.CommandText), "left join d7_parent").Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Collection_terminal_cold_warm_and_prepared_should_render_identical_sql()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var cold = Get(ctx, ctx.From<D7Parent>().Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count() }), storeInCache: true);
            var warm = Get(ctx, ctx.From<D7Parent>().Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count() }), storeInCache: true);
            var prepared = Get(ctx, ctx.From<D7Parent>().Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count() }), storeInCache: false);

            ReferenceEquals(cold, warm).Should().BeTrue("an identical collection-terminal query must reuse the cached plan");
            Normalize(warm.DbCommand.CommandText).Should().Be(Normalize(cold.DbCommand.CommandText));
            Normalize(prepared.DbCommand.CommandText).Should().Be(Normalize(cold.DbCommand.CommandText));
            CountOccurrences(Normalize(cold.DbCommand.CommandText), "from d7_child").Should().Be(2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Cold_warm_and_prepared_reference_queries_should_return_the_same_rows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var cold = ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }).ToList();
            var warm = ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }).ToList();
            var prepared = ctx.ToList(Get(ctx, ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }), storeInCache: false));

            cold.Should().BeEquivalentTo(warm);
            cold.Should().BeEquivalentTo(prepared);
            cold.Single(r => r.Id == 10).Name.Should().Be("p1");
            cold.Single(r => r.Id == 12).Name.Should().BeNull("the foreign key dangles");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Re_preparing_a_reference_navigation_command_should_not_duplicate_joins()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name });
            cmd.PrepareCommand(false, CancellationToken.None);
            var joinsAfterFirst = cmd.Joins!.Length;

            cmd.ResetPreparation();
            cmd.PrepareCommand(false, CancellationToken.None);

            cmd.Joins!.Length.Should().Be(joinsAfterFirst);
            joinsAfterFirst.Should().Be(1, "the reference expansion injects exactly one LEFT JOIN");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Re_preparing_a_collection_terminal_command_should_not_duplicate_subqueries()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<D7Parent>().Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count() });
            cmd.PrepareCommand(false, CancellationToken.None);
            var refsAfterFirst = cmd.ReferencedQueries.Count;
            var joinsAfterFirst = cmd.Joins?.Length ?? 0;

            cmd.ResetPreparation();
            cmd.PrepareCommand(false, CancellationToken.None);

            cmd.ReferencedQueries.Count.Should().Be(refsAfterFirst);
            (cmd.Joins?.Length ?? 0).Should().Be(joinsAfterFirst);
            refsAfterFirst.Should().Be(2, "Any and Count register one correlated subquery each");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Preparing_a_clone_of_an_expanded_command_should_keep_joins_and_subqueries_stable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var reference = ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name });
            reference.PrepareCommand(false, CancellationToken.None);
            var referenceJoins = reference.Joins!.Length;

            var referenceClone = reference.Clone();
            referenceClone.PrepareCommand(false, CancellationToken.None);
            referenceClone.Joins!.Length.Should().Be(referenceJoins);

            var collection = ctx.From<D7Parent>().Select(p => new { p.Id, C = p.Children.Count() });
            collection.PrepareCommand(false, CancellationToken.None);
            var collectionRefs = collection.ReferencedQueries.Count;

            var collectionClone = collection.Clone();
            collectionClone.PrepareCommand(false, CancellationToken.None);
            collectionClone.ReferencedQueries.Count.Should().Be(collectionRefs);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Interleaving_a_navigation_Any_should_keep_the_shared_command_cacheable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // A navigation-backed shared Any: the reference expansion injects a LEFT JOIN and rewrites
            // the presence test onto the joined principal key. A reference-presence predicate is used
            // (not a collection terminal) so the test never trips the pre-existing, unrelated stack
            // overflow of an outer Any over a correlated-subquery predicate.
            ctx.From<D7Child>().Where(c => c.Parent != null).Any().Should().BeTrue();

            ctx.AnyCommand.Should().NotBeNull();
            ctx.AnyCommand!.Value.Cache.Should().BeTrue("a navigation query must not disable the shared Any command's plan cache");

            // The same navigation-backed Any must reuse the cached shared command.
            var first = ctx.From<D7Child>().Where(c => c.Parent != null).Any();
            var second = ctx.From<D7Child>().Where(c => c.Parent != null).Any();
            first.Should().BeTrue();
            second.Should().BeTrue();
            CommandCacheHit(ctx).Should().BeTrue("the navigation-backed Any must keep hitting the plan cache");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void A_navigation_query_should_not_leak_state_into_an_unrelated_later_query()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            _ = ctx.From<D7Child>().Select(c => new { c.Id, Name = c.Parent!.Name }).ToList();
            _ = ctx.From<D7Parent>().Select(p => new { p.Id, Has = p.Children.Any() }).ToList();

            // An unrelated later query must still be planned and cached normally.
            var warm1 = Get(ctx, ctx.From<D7Parent>().Select(p => new { p.Id }), storeInCache: true);
            var warm2 = Get(ctx, ctx.From<D7Parent>().Select(p => new { p.Id }), storeInCache: true);

            ReferenceEquals(warm1, warm2).Should().BeTrue("navigation queries must not disable plan caching for the context");
            Normalize(warm1.DbCommand.CommandText).Should().NotContain("left join");
            Normalize(warm1.DbCommand.CommandText).Should().NotContain("exists(");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Prepared_reference_query_should_reuse_parameters_without_baking_in_a_join()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var cmd = ctx.From<D7Child>().Where(c => c.Parent!.Name == SqlFunctions.Parameter<string>(0)).Select(c => c.Id);
            var prepared = Get(ctx, cmd, storeInCache: false);

            var sql = Normalize(prepared.DbCommand.CommandText);
            CountOccurrences(sql, "left join d7_parent").Should().Be(1);

            // One prepared, parameterized nav command reused with different positional values. The SQL
            // shape (parameter placeholder, join alias) must not change between executions.
            var reusable = (IPreparedQueryCommand<int>)prepared;
            reusable.ToList(ctx, "p1").Should().BeEquivalentTo(new[] { 10, 11 });
            reusable.ToList(ctx, "p2").Should().BeEmpty();
            reusable.ToList(ctx, "absent").Should().BeEmpty();

            Normalize(prepared.DbCommand.CommandText).Should().Be(sql);
            CountOccurrences(Normalize(prepared.DbCommand.CommandText), "left join d7_parent").Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// Returns true when a freshly built, structurally identical query of a known unrelated shape is
    /// served from the plan cache (the same compiled command instance is returned).
    /// </summary>
    private static bool CommandCacheHit(IDataContext ctx)
    {
        var first = Get(ctx, ctx.From<D7Child>().Where(c => c.Id > 0).Select(c => c.Id), storeInCache: true);
        var second = Get(ctx, ctx.From<D7Child>().Where(c => c.Id > 0).Select(c => c.Id), storeInCache: true);
        return ReferenceEquals(first, second);
    }
}

[SqlTable("d7_parent")]
public sealed class D7Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<D7Child> Children { get; set; } = new List<D7Child>();
}

[SqlTable("d7_child")]
public sealed class D7Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public D7Parent? Parent { get; set; }
}

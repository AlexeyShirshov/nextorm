using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Executes real SQLite queries that project whole entities and materializes the entities back from the
/// flattened scalar columns. Unlike a SQL-text test, these run the query so a mapper that rejects or
/// mis-groups entity-typed columns fails here.
/// </summary>
public class EntityItemProjectionTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-entity-items-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table entity_parent (id integer primary key, name text);" +
                              "create table entity_child (id integer primary key, parent_id integer, name text, state text);" +
                              "insert into entity_parent (id, name) values (1, 'p1'), (2, 'p2');" +
                              "insert into entity_child (id, parent_id, name, state) values (10, 1, 'c1', 'Active'), (11, 1, 'c2', 'Closed');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void OuterJoin_EntityItems_ShouldMaterializeBothEntitiesAndNullChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { Left = p.Item1, Right = p.Item2 })
                .ToList();

            rows.Should().HaveCount(3);

            var withChildren = rows.Where(r => r.Right is not null).OrderBy(r => r.Right!.Id).ToList();
            withChildren.Should().HaveCount(2);
            withChildren.Should().OnlyContain(r => r.Left.Id == 1 && r.Left.Name == "p1");

            // Field order and the value converter on the nested entity survive the expansion.
            withChildren.Select(r => r.Right!.Id).Should().Equal(10, 11);
            withChildren.Select(r => r.Right!.Name).Should().Equal("c1", "c2");
            withChildren.Select(r => r.Right!.State).Should().Equal(EntityItemState.Active, EntityItemState.Closed);

            var childless = rows.Single(r => r.Left.Id == 2);
            childless.Left.Name.Should().Be("p2");
            childless.Right.Should().BeNull("a LEFT JOIN row with no child materializes the entity item as null");

            // A second execution reuses the cached mapper for the same SQL/shape and produces the same rows.
            var again = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { Left = p.Item1, Right = p.Item2 })
                .ToList();
            again.Should().HaveCount(3);
            again.Count(r => r.Right is null).Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void InnerJoin_MixedProjection_ShouldMaterializeScalarAndEntity()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<EntityItemParent>()
                .Join(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { ParentId = p.Item1.Id, Child = p.Item2 })
                .ToList();

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.ParentId == 1);
            rows.Select(r => r.Child!.Id).OrderBy(x => x).Should().Equal(10, 11);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void BareProjection_PreparedWithoutCache_ShouldStillMaterialize()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand();

            // Preparing with storeInCache: false skips the plan hash and the per-column ordinal must
            // still be reassigned after expansion.
            var prepared = ctx.GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: false, CancellationToken.None);
            var rows = prepared.ToList(ctx);

            rows.Should().HaveCount(3);
            rows.Single(r => r.Item1.Id == 2).Item2.Should().BeNull();
            rows.Single(r => r.Item2 is { Id: 10 }).Item2!.Name.Should().Be("c1");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void BareProjection_ShouldMaterializeProjectionOfEntitiesAndNullChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand());

            // Item columns are expanded in a deterministic order: the parent item first, then the child.
            sql.Should().Be("select t1.id, t1.name, t2.id, t2.parent_id as 'ParentId', t2.name, t2.state " +
                            "from entity_parent as 't1' left join entity_child as 't2' on t1.id = t2.parent_id");

            var rows = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToList();

            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(r => r.Item1.Name == "p1" || r.Item1.Name == "p2");

            var childless = rows.Single(r => r.Item1.Id == 2);
            childless.Item2.Should().BeNull();

            var child = rows.Single(r => r.Item2 is { Id: 10 });
            child.Item1.Id.Should().Be(1);
            child.Item2!.State.Should().Be(EntityItemState.Active);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

public enum EntityItemState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("entity_parent")]
public sealed class EntityItemParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("entity_child")]
public sealed class EntityItemChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<EntityItemState>))]
    public EntityItemState State { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B D5: whole-reference materialization and the unlifted non-nullable navigation scalar guard.
/// A whole reference projected from the missing side of the injected <c>LEFT JOIN</c> materializes as
/// <see langword="null"/> (A6); an unlifted non-nullable scalar read through an absent principal fails
/// with a diagnostic naming the navigation path and result type, while a nullable lift and a coalesce
/// materialize correctly (A7). InMemory lowering is D6.
/// </summary>
public class ImplicitNavigationD5Tests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText;

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d5-nav-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = Schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        ctx.From<D5Parent>();
        ctx.From<D5Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        return (ctx, path);
    }

    private const string Schema =
        "create table d5_parent (id integer primary key, age integer not null, name text);" +
        "create table d5_child (id integer primary key, parent_id integer, name text);" +
        "insert into d5_parent (id, age, name) values (1, 42, 'p1');" +
        "insert into d5_child (id, parent_id, name) values (10, 1, 'c1'), (11, 999, 'c2');";

    [Fact]
    public void Absent_whole_reference_should_materialize_null_and_present_reference_should_round_trip()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<D5Child>()
                .Select(c => new { c.Id, Parent = c.Parent })
                .ToList();

            var present = rows.Single(r => r.Id == 10);
            present.Parent.Should().NotBeNull();
            present.Parent!.Id.Should().Be(1);
            present.Parent.Age.Should().Be(42);
            present.Parent.Name.Should().Be("p1");

            rows.Single(r => r.Id == 11).Parent.Should().BeNull("the foreign key dangles and the LEFT JOIN produced all-NULL");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Whole_reference_projection_should_left_join_the_principal()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<D5Child>().Select(c => new { c.Id, Parent = c.Parent }));

            sql.Should().Contain("left join d5_parent");
            sql.Should().Contain("on t1.parent_id = t2.id");
            sql.Should().Contain("t2.age");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Unlifted_non_nullable_scalar_on_absent_principal_should_throw_with_path_and_result_type()
    {
        var (ctx, path) = CreateDb();
        try
        {
            Action act = () => SqlOf(ctx, ctx.From<D5Child>().Select(c => new { c.Id, Age = c.Parent!.Age }));

            act.Should().Throw<QueryPreparationException>()
                .WithMessage("*D5Child.Parent.Age*System.Int32*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Lifted_nullable_and_coalesced_scalars_should_materialize()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<D5Child>()
                .Select(c => new { c.Id, Lifted = (int?)c.Parent!.Age, Coalesced = (int?)c.Parent!.Age ?? -1 })
                .ToList();

            var present = rows.Single(r => r.Id == 10);
            present.Lifted.Should().Be(42);
            present.Coalesced.Should().Be(42);

            var dangling = rows.Single(r => r.Id == 11);
            dangling.Lifted.Should().BeNull();
            dangling.Coalesced.Should().Be(-1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Cloned_expanded_command_should_keep_the_unlifted_scalar_guard()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // The first preparation expands the navigation (installs the LEFT JOIN and the path map) and
            // then rejects the projection. The expanded-but-unprepared command is cloned; the clone must
            // keep the navigation paths so its own re-preparation rejects too instead of materializing a
            // silent default.
            var cmd = ctx.From<D5Child>().Select(c => new { c.Id, Age = c.Parent!.Age });

            Action first = () => cmd.PrepareCommand(false, CancellationToken.None);
            first.Should().Throw<QueryPreparationException>().WithMessage("*D5Child.Parent.Age*");

            var clone = cmd.Clone();
            Action second = () => clone.PrepareCommand(false, CancellationToken.None);
            second.Should().Throw<QueryPreparationException>().WithMessage("*D5Child.Parent.Age*System.Int32*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("d5_parent")]
public sealed class D5Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int Age { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("d5_child")]
public sealed class D5Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public D5Parent? Parent { get; set; }
}

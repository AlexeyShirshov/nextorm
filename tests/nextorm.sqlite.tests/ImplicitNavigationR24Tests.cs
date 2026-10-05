using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B R2.4: SQL consumer composition and the overflow surface. Before the fix a navigation
/// <c>Count()</c>/property <c>Count</c> used in a predicate, boolean projection or arithmetic failed at
/// prepare with <c>InvalidOperationException: The binary operator ... is not defined for
/// 'Func&lt;IQueryRegistry,QueryCommand&gt;' and 'Int32'</c>; the typed scalar placeholder now lets all
/// four consumers compose. The Int32 narrowing is emitted as a narrow <c>cast(count(...) as ...)</c>;
/// an out-of-range value read into an <see cref="int"/> surfaces as a top-level
/// <see cref="OverflowException"/> (the control below), while predicate/boolean/arithmetic comparisons
/// stay in the database on providers whose cast does not enforce the range.
/// </summary>
public class ImplicitNavigationR24Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static (SqliteDataContext Ctx, string Path) CreateDb(string schema)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-r24-nav-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static (SqliteDataContext Ctx, string Path) CreateOneToMany()
    {
        var (ctx, path) = CreateDb(Schema);
        ctx.From<R24SqlChild>();
        ctx.From<R24SqlParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        return (ctx, path);
    }

    private const string Schema =
        "create table r24s_parent (id integer primary key);" +
        "create table r24s_child (id integer primary key, parent_id integer);" +
        "insert into r24s_parent (id) values (1), (2);" +
        "insert into r24s_child (id, parent_id) values (10, 1), (11, 1);";

    // #148-B R2.4 red->green (composition): the four consumers x both Count forms prepare and run.
    [Fact]
    public void Count_dot_and_property_should_compose_in_scalar_predicate_bool_and_arithmetic()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => p.Children.Count()).ToList().Should().Equal(2, 0);
            ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => p.Children.Count).ToList().Should().Equal(2, 0);

            ctx.From<R24SqlParent>().Where(p => p.Children.Count() > 1).Select(p => p.Id).ToList().Should().Equal(1);
            ctx.From<R24SqlParent>().Where(p => p.Children.Count > 1).Select(p => p.Id).ToList().Should().Equal(1);

            var boolean = ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => new { B = p.Children.Count() == 2 }).ToList();
            boolean.Select(r => r.B).Should().Equal(true, false);
            var booleanProp = ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => new { B = p.Children.Count == 2 }).ToList();
            booleanProp.Select(r => r.B).Should().Equal(true, false);

            var arithmetic = ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => new { V = p.Children.Count() + 1 }).ToList();
            arithmetic.Select(r => r.V).Should().Equal(3, 1);
            var arithmeticProp = ctx.From<R24SqlParent>().OrderBy(p => p.Id).Select(p => new { V = p.Children.Count + 1 }).ToList();
            arithmeticProp.Select(r => r.V).Should().Equal(3, 1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // #148-B r3 A3′: every consumer's SQL keeps the correlated count wide (bigint, no in-database int
    // cast), so a predicate/boolean/arithmetic evaluates exactly at 64-bit; the outer checked
    // narrowing is a CLR-only materialization boundary. LongCount stays 64-bit end to end.
    [Fact]
    public void Count_consumers_should_emit_the_wide_count_and_never_an_in_database_int_cast()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var scalar = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => p.Children.Count())).ToLowerInvariant();
            var predicate = SqlOf(ctx, ctx.From<R24SqlParent>().Where(p => p.Children.Count() > 1).ToCommand()).ToLowerInvariant();
            var boolean = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => new { B = p.Children.Count == 2 })).ToLowerInvariant();
            var arithmetic = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => new { V = p.Children.Count() + 1 })).ToLowerInvariant();
            var longCount = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => p.Children.LongCount())).ToLowerInvariant();

            foreach (var sql in new[] { scalar, predicate, boolean, arithmetic, longCount })
            {
                sql.Should().Contain("count(", "the count must stay a 64-bit aggregate (A3′)");
                sql.Should().NotContain("cast(count(", "no in-database Int32 cast may be emitted (A3′)");
                sql.Should().NotContain(" as integer)", "no Int32 narrowing cast may wrap the wide count (A3′)");
                sql.Should().Contain("r24s_child");
            }

            arithmetic.Should().Contain(" + 1", "the arithmetic consumer must compose the typed scalar subquery");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // #148-B r3 A3′ witness (c): predicate / boolean / arithmetic around a navigation count compose a
    // wide constant and are evaluated exactly in the database at 64-bit. The count stays a wide
    // aggregate (no `cast(count(...) as integer)`), so `count + 3_000_000_000` is not narrowed/truncated
    // to 32 bits; the result exceeds Int32.MaxValue and is correct per row.
    [Fact]
    public void Wide_arithmetic_predicate_and_boolean_around_the_count_should_be_exact_in_database()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            const long offset = 3_000_000_000L;

            var predicate = ctx.From<R24SqlParent>()
                .Where(p => p.Children.Count() + offset > offset + 1)
                .Select(p => p.Id)
                .ToList();
            // Only the parent with 2 children exceeds offset + 1 when the comparison is exact at 64-bit.
            predicate.Should().Equal(1);

            var arithmetic = ctx.From<R24SqlParent>()
                .OrderBy(p => p.Id)
                .Select(p => new { V = p.Children.Count() + offset })
                .ToList();
            arithmetic.Select(r => r.V).Should().Equal(offset + 2, offset);

            var boolean = ctx.From<R24SqlParent>()
                .OrderBy(p => p.Id)
                .Select(p => new { B = p.Children.Count() + offset > offset + 1 })
                .ToList();
            boolean.Select(r => r.B).Should().Equal(true, false);

            var sql = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => new { V = p.Children.Count() + offset })).ToLowerInvariant();
            sql.Should().Contain("count(").And.NotContain("cast(count(").And.NotContain(" as integer)");
            sql.Should().Contain("3000000000");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // #148-B r3 A3′ witness (d): the cast suppression is scoped to the navigation-count provenance.
    // An ordinary checked scalar conversion over a plain int column still emits its SQL cast.
    [Fact]
    public void Ordinary_checked_scalar_conversion_should_keep_its_sql_cast()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var sql = SqlOf(ctx, ctx.From<R24SqlParent>().Select(p => (long)p.Id)).ToLowerInvariant();

            sql.Should().Contain("cast(", "a non-navigation conversion must still be rendered");
            sql.Should().NotContain("cast(count(", "no navigation count is involved");
            sql.Should().NotContain("count(");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // #148-B R2.4: the SQL execution surface for an out-of-range value is the Int32 read of the
    // narrow count column. A wide bigint read into int throws a top-level OverflowException; this is
    // the materializer seam that the navigation count's `cast(count(...) as integer)` feeds.
    [Fact]
    public void Out_of_range_int_scalar_read_should_throw_overflow()
    {
        var (ctx, path) = CreateDb(
            "create table r24s_wide (id integer primary key, big bigint);" +
            "insert into r24s_wide (id, big) values (1, 2147483648);");
        try
        {
            ctx.From<R24Wide>();
            Action act = () => ctx.From<R24Wide>().Select(w => w.Big).ToList();
            act.Should().Throw<OverflowException>("an out-of-range Int32 scalar read must not wrap or truncate");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("r24s_parent")]
public sealed class R24SqlParent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    public ICollection<R24SqlChild> Children { get; set; } = new List<R24SqlChild>();
}

[SqlTable("r24s_child")]
public sealed class R24SqlChild
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("r24s_wide")]
public sealed class R24Wide
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("big")]
    public int Big { get; set; }
}

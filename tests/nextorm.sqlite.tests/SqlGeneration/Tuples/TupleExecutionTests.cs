using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Executes the flat row-constructor surface against a real in-process SQLite database (no container).
/// SQLite 3.15+ supports row values, so a row comparison and inline <c>.ItemN</c> folding must return
/// the expected rows, not merely render SQL.
/// </summary>
public class TupleExecutionTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-tuple-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table complex_entity (id integer, nullableint integer, somestring text, b integer, dt text);" +
                "insert into complex_entity (id, nullableint, somestring, b, dt) values " +
                "(1, 10, 'a', 1, '2020-01-01'), (2, 20, 'b', 0, '2020-01-02'), (3, 30, 'c', 1, '2020-01-03');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    [Fact]
    public void Tuple_RowComparisonInWhere_ShouldExecuteAndFilter()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ids = ctx.From<IComplexEntity>()
                .Where(x => Tuple.Create(x.Id, x.String) == Tuple.Create(2L, "b"))
                .Select(x => x.Id)
                .ToList();

            ids.Should().Equal(2L);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Tuple_RowComparisonAndOr_ShouldExecuteAndFilter()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ids = ctx.From<IComplexEntity>()
                .Where(x => (Tuple.Create(x.Id, x.String) == Tuple.Create(1L, "a"))
                            || (Tuple.Create(x.Id, x.String) == Tuple.Create(3L, "c")))
                .Select(x => x.Id)
                .ToList();

            ids.Should().BeEquivalentTo(new[] { 1L, 3L });
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Tuple_InlineElementAccess_ShouldExecuteInFilterAndProjection()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ids = ctx.From<IComplexEntity>()
                .Where(x => Tuple.Create(x.Id, x.String).Item1 > 1L)
                .Select(x => x.Id)
                .ToList();

            ids.Should().BeEquivalentTo(new[] { 2L, 3L });

            var strings = ctx.From<IComplexEntity>()
                .OrderBy(x => Tuple.Create(x.Id, x.String).Item1)
                .Select(x => Tuple.Create(x.Id, x.String).Item2)
                .ToList();

            strings.Should().Equal("a", "b", "c");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Tuple_CapturedOperandComparison_ShouldThrowAtPreparationWithoutExecutingACommand()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var local = Tuple.Create(1L, "a");
            var act = () => ctx.From<IComplexEntity>()
                .Where(x => Tuple.Create(x.Id, x.String) == local)
                .Select(x => x.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().Contain("SqliteDialect");

            // The connection is still usable and the table is untouched: preparation failed before any
            // DB command reached the driver.
            ctx.From<IComplexEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

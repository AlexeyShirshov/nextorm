using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362SelectGroupMatrixTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    // --- Null args ---
    [Fact]
    public void NullProjection_ThrowsArgumentNullException()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var act = () => SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullCondition_ThrowsArgumentNullException()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var act = () => SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<Entity, long>>)(x => x.Id), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // --- Three outcomes: keyed ---
    [Fact]
    public void Keyed_GeneratesGroupBy()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => x.Id));
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("group by");
    }

    // --- Global aggregation (zero keys) ---
    [Fact]
    public void GlobalAggregation_NoGroupBy_HasHaving()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(
            e,
            (Expression<Func<Entity, int>>)(x => SqlFunctions.Sql.count(x.Id)),
            x => SqlFunctions.Sql.count(x.Id) > 5);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);

        sql.Should().NotContain("group by", "a global aggregate over zero keys has no GROUP BY list");
        sql.Should().Contain("count(", "the aggregate projection is unchanged");
        sql.Should().Contain(" having ", "the aggregate condition is a real HAVING");
    }

    /// <summary>
    /// The constant outcome against real rows: one exact value over a non-empty source and zero rows
    /// over an empty one (the predicate is folded into the pre-group WHERE and DISTINCT keeps it at one
    /// row, never "one row regardless of the input").
    /// </summary>
    [Fact]
    public void ConstantProjection_OneRowOnData_ZeroRowsOnEmptyInput()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seed = connection.CreateCommand())
        {
            seed.CommandText =
                "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER NULL, Name TEXT NULL, Category TEXT NULL);" +
                "INSERT INTO Entity (Id, Score, Name, Category) VALUES (1,10,'a','x'),(2,20,'b','y');";
            seed.ExecuteNonQuery();
        }

        using var ctx = new SqliteDataContext(connection, new DataContextBuilder());

        var nonEmpty = ctx.From<Entity>().SelectGroup(x => new { Constant = 7 }).ToList();
        nonEmpty.Should().ContainSingle().Which.Constant.Should().Be(7);

        var empty = ctx.From<Entity>().Where(x => x.Id < 0).SelectGroup(x => new { Constant = 7 }).ToList();
        empty.Should().BeEmpty("the pre-group predicate leaves no row, so the constant projection returns none");
    }

    // --- Constant projection ---
    [Fact]
    public void ConstantProjection_HasDistinct()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => 42));
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("distinct");
    }

    // --- HAVING placement ---
    [Fact]
    public void Having_PlacedInSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<Entity, long>>)(x => x.Id), x => x.Id > 5);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("having");
    }

    // --- In-memory rejection ---
    [Fact]
    public void InMemory_RejectsSelectGroup()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => x.Id));
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void InMemory_RejectsSelectGroupHaving()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<Entity, long>>)(x => x.Id), x => x.Score > 0);
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    // --- Builder immutability ---
    [Fact]
    public void Builder_NotMutated()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var original = e.Select(x => x.Id);
        var _ = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => x.Id));
        // original builder should still produce a plain select.
        var sql = Normalize(((DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(original, false, true, CancellationToken.None)).SqlStmt ?? string.Empty);
        sql.Should().NotContain("group by");
    }

    // --- Raw-shape distinctness: different literals ---
    [Fact]
    public void DifferentLiterals_DistinctSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd1 = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => x.Score > 5 ? x.Id : x.Id));
        var cmd2 = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, long>>)(x => x.Score > 10 ? x.Id : x.Id));
        var s1 = Normalize(Prepare(ctx, cmd1).SqlStmt ?? string.Empty);
        var s2 = Normalize(Prepare(ctx, cmd2).SqlStmt ?? string.Empty);
        s1.Should().NotBe(s2);
    }
}

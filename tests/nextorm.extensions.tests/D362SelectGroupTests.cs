using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362SelectGroupTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void SelectGroup_NullProjection_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var act = () => e.SelectGroup<Entity, object>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectGroupHaving_NullCondition_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var act = () => e.SelectGroupHaving<Entity, object>(x => x.Id, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectGroup_GeneratesGroupBy()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        Expression<Func<Entity, object>> proj = x => x.Category!;
        var cmd = SelectGroupExtensions.SelectGroup(e, proj);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);

        sql.Should().Contain("group by");
    }

    [Fact]
    public void SelectGroup_InMemory_Throws()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<Entity, object>>)(x => new { x.Category, Total = x.Score }));
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void SelectGroupHaving_InMemory_Throws()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<Entity, object>>)(x => new { x.Category, Total = x.Score }), x => x.Score > 0);
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }
}

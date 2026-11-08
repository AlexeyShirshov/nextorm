using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class SmokeTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public int rn { get; set; }
        public int rank { get; set; }
        public int __nextorm_ext_rn { get; set; }
    }

    [Fact]
    public void Api_Compiles_And_Returns_Expected_Types()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var builder = ExtremeRowExtensions.SelectWhereMin(e, x => x.Score);
        builder.Should().BeOfType<EntityBuilder<Entity>>();

        var cmd = ExtremeRowExtensions.SelectWhereMin(e, x => x.Score, x => new { x.Id, x.Name });
        cmd.GetType().IsGenericType.Should().BeTrue();
        cmd.GetType().GetGenericTypeDefinition().Should().Be(typeof(QueryCommand<>));
    }

    [Fact]
    public void MinEx_GeneratesRankWindow_AndRestoresProjection()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = ExtremeRowExtensions.SelectWhereMin(e, x => x.Score).Select(x => new { x.Id, x.Name });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);

        sql.Should().Contain("row_number()");
        sql.Should().Contain("__nextorm_ext_rn");
        sql.Should().Contain("= 1");
        sql.Should().NotContain("rn = 1");
    }

    [Fact]
    public void WarmHit_ReusesThePreparedPlan_WithoutRebuilding()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = ExtremeRowExtensions.SelectWhereMin(e, x => x.Score).ToCommand();
        var first = Prepare(ctx, cmd);
        var second = Prepare(ctx, cmd);

        // A confirmed warm hit returns the cached plan's SQL instance; the component's BuildQuery ran
        // only while that plan was built. SQL identity is the observable that needs no product counter.
        second.SqlStmt.Should().BeSameAs(first.SqlStmt);
    }

    [Fact]
    public void Min_Then_Max_Replaces_InPlace()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var builder = ExtremeRowExtensions.SelectWhereMax(ExtremeRowExtensions.SelectWhereMin(e, x => x.Score), x => x.Score);
        var cmd = builder.ToCommand();

        cmd.Components.Should().NotBeNull();
        cmd.Components!.Count.Should().Be(1);
        cmd.Components[0].Slot.Should().Be(typeof(ExtremeRowComponent<Entity>));
    }

    [Fact]
    public void InMemory_Context_Is_Rejected()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<Entity>();
        var cmd = ExtremeRowExtensions.SelectWhereMin(e, x => x.Score).ToCommand();

        var act = () => Prepare(ctx, cmd);
        act.Should().Throw<NotSupportedException>();
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");
}

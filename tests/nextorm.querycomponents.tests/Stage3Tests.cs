using System.Reflection;
using FluentAssertions;
using System.Linq.Expressions;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

public class Stage3Tests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    private static SqliteDataContext Ctx() => new("Data Source=:memory:", new DataContextBuilder());

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).SqlStmt!.Replace("\r\n", "\n");

    [Fact]
    public void A1_MinToMax_ReplacesInPlace_AndSqlUsesDesc()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        var b2 = b.ExternalSelectWhereMax(x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);

        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("desc");
    }

    [Fact]
    public void A1_MaxToMin_ReplacesInPlace_AndSqlHasNoDesc()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>().ExternalSelectWhereMax(x => x.Score);
        var b2 = b.ExternalSelectWhereMin(x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);

        var sql = SqlOf(ctx, cmd);
        sql.Should().NotContain(" desc");
    }

    // Chunk A2
    [Fact]
    public void A2_TwoDistinctExclusiveReshapeComponents_ThrowPreparationExceptionNamingSlots()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new ExternalSelectWhereComponent<Entity>(ExternalExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null))
            .WithComponent(new ConflictingReshapeComponent<Entity>());

        QueryCommand<Entity> cmd = b;
        Action act = () => _ = SqlOf(ctx, cmd);
        act.Should().Throw<Exception>()
            .Which.Message.Should().Contain("Multiple distinct exclusive row-source reshape");
    }

    // Chunk A3
    [Fact]
    public void A3_AdditivePredicate_ComposesInsideInnerSource()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new AdditivePredicateComponent<Entity>((Expression<Func<Entity, bool>>)(x => x.Score > 5)))
            .WithComponent(new ExternalSelectWhereComponent<Entity>(ExternalExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));

        QueryCommand<Entity> cmd = b;
        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("Score > 5");
        // predicate appears in the inner derived table, before rank
        var fromIndex = sql.IndexOf("from Entity", StringComparison.Ordinal);
        var predicateIndex = sql.IndexOf("Score > 5", StringComparison.Ordinal);
        var outerIndex = sql.IndexOf(") as 't1'", StringComparison.Ordinal);
        predicateIndex.Should().BeGreaterThan(fromIndex);
        predicateIndex.Should().BeLessThan(outerIndex);
    }
}

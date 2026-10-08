using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// D176.4 SQL-lowering regression: a nested JSON projection is captured as a recursive shape and its
/// scalar descendants are lowered into the prepared select list. The lowered statement must read the
/// exact same explicit scalar columns as the ordinary flat projection over the same leaves, so the
/// recursive writer's reader ordinals match the emitted SQL. These tests never open a connection.
/// </summary>
public class JsonStreamingSqlLoweringTests
{
    internal static DbPreparedQueryCommand<T> PrepareJson<T>(IDataContext ctx, QueryCommand<T> command)
    {
        var clone = (QueryCommand<T>)command.Clone();
        clone.DocumentMode = true;
        clone.ResetPreparation();
        clone.JsonShapeMode = true;
        clone.JsonShape = null;
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(clone, createEnumerator: false, storeInCache: false, CancellationToken.None);
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None);

    [Fact]
    public void NestedShape_ShouldLowerToTheSameExplicitColumnsAsTheFlatProjection()
    {
        using var ctx = PostgresTestContext.Create();

        var nested = PrepareJson(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.String, x.Int } }));
        var flat = Prepare(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.String, x.Int }));

        nested.DbCommand.CommandText.Should().Be(flat.DbCommand.CommandText);
        nested.DbCommand.CommandText.Should().Contain("nullableint").And.Contain("somestring");
        nested.DbCommand.CommandText.Should().NotContain("*");
    }

    [Fact]
    public void JsonShapeCapture_ShouldNotMutateTheCallerCommandOrItsCacheFlag()
    {
        using var ctx = PostgresTestContext.Create();
        var command = ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.String } });

        PrepareJson(ctx, command);

        command.Cache.Should().BeTrue("the JSON clone must not disable the caller's plan cache");
    }
}

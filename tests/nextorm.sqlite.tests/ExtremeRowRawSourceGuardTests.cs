using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #315: a projection-less <c>SelectWhereMax</c>/<c>SelectWhereMin</c> over a literal name-addressed
/// source (<c>From("table")</c>) declares an entity-returning whole-row result, but the source type has
/// no mapped metadata. Preparation must report the mapped-source contract
/// (<see cref="BuildSqlCommandException"/> with the frozen message) instead of falling through to the
/// generic "Select must return new anonymous type" projection error.
/// <para>
/// The negative controls are binding: a mapped entity with either projection-less operator keeps
/// preparing/rendering, and the explicitly projected overloads keep supporting a raw named source
/// (the projection supplies the result shape, so no mapped entity is required).
/// </para>
/// </summary>
public class ExtremeRowRawSourceGuardTests
{
    private const string MappedSourceMessage = "SelectWhereMax/SelectWhereMin requires a mapped entity source.";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    [Fact]
    public void RawSourceProjectionlessSelectWhereMax_ShouldThrowMappedSourceGuard()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => Prepare(ctx, ctx.From("simple_entity").SelectWhereMax(t => t["id"].AsInt).ToCommand());

        act.Should().Throw<BuildSqlCommandException>().WithMessage(MappedSourceMessage);
    }

    [Fact]
    public void RawSourceProjectionlessSelectWhereMin_ShouldThrowMappedSourceGuard()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => Prepare(ctx, ctx.From("simple_entity").SelectWhereMin(t => t["id"].AsInt).ToCommand());

        act.Should().Throw<BuildSqlCommandException>().WithMessage(MappedSourceMessage);
    }

    [Fact]
    public void RawSourceProjectedSelectWhereMax_ShouldRenderTheRequestedProjection()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = Normalize(Prepare(ctx, ctx.From("simple_entity")
            .SelectWhereMax(t => t["id"].AsInt, t => new { Id = t["id"].AsInt }))
            .DbCommand.CommandText);

        sql.Should().Contain("row_number() over (order by id desc)");
        sql.Should().Contain("from simple_entity");
        sql.Should().NotContain("requires a mapped entity source");
    }

    [Fact]
    public void RawSourceProjectedSelectWhereMin_ShouldRenderTheRequestedProjection()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = Normalize(Prepare(ctx, ctx.From("simple_entity")
            .SelectWhereMin(t => t["id"].AsInt, t => new { Id = t["id"].AsInt }))
            .DbCommand.CommandText);

        sql.Should().Contain("row_number() over (order by id)");
        sql.Should().Contain("from simple_entity");
        sql.Should().NotContain("requires a mapped entity source");
    }

    [Fact]
    public void MappedEntityProjectionlessSelectWhereMax_ShouldRenderTheWholeRow()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = Normalize(Prepare(ctx, ctx.From<ExplicitlyMappedEntity>().SelectWhereMax(x => x.Value).ToCommand())
            .DbCommand.CommandText);

        sql.Should().Contain("row_number() over (order by ExplicitColumn desc)");
        sql.Should().Contain("from ExplicitTable");
        sql.Should().NotContain("requires a mapped entity source");
    }

    [Fact]
    public void MappedEntityProjectionlessSelectWhereMin_ShouldRenderTheWholeRow()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = Normalize(Prepare(ctx, ctx.From<ExplicitlyMappedEntity>().SelectWhereMin(x => x.Value).ToCommand())
            .DbCommand.CommandText);

        sql.Should().Contain("row_number() over (order by ExplicitColumn)");
        sql.Should().Contain("from ExplicitTable");
        sql.Should().NotContain("requires a mapped entity source");
    }
}

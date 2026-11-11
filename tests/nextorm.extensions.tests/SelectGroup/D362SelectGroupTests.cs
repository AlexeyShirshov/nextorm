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
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, x => new { x.Category, Total = x.Score }, r => r.Total > 0);
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    private sealed class ProjectedRow
    {
        public long Id { get; set; }
        public int Rows { get; set; }
        public int Unbound { get; set; }
    }

    /// <summary>
    /// The condition is typed on the projection result: its member stands for the projection expression,
    /// so an aliased aggregate in <c>HAVING</c> renders the same statement as the underlying aggregate in
    /// a source <c>Having</c>.
    /// </summary>
    [Fact]
    public void SelectGroupHaving_ProjectionAlias_MatchesUnderlyingAggregateHaving()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var aliased = e.SelectGroupHaving(
            x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) }, r => r.Rows > 1);
        var chained = e.Having(x => SqlFunctions.Sql.count(x.Id) > 1)
            .SelectGroup(x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) });

        Normalize(Prepare(ctx, aliased).SqlStmt ?? string.Empty)
            .Should().Be(Normalize(Prepare(ctx, chained).SqlStmt ?? string.Empty));
    }

    /// <summary>A scalar projection makes the condition parameter the projected value itself.</summary>
    [Fact]
    public void SelectGroupHaving_ScalarProjection_ParameterIsTheValue()
    {
        var threshold = 5;
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = SelectGroupExtensions.SelectGroupHaving(
            e, (Expression<Func<Entity, long>>)(x => x.Id), f => f > threshold);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);

        sql.Should().Contain("having");
    }

    /// <summary>A member the projection does not bind fails closed, before any SQL.</summary>
    [Fact]
    public void SelectGroupHaving_UnboundMember_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(
            e, (Expression<Func<Entity, ProjectedRow>>)(x => new ProjectedRow { Id = x.Id }), r => r.Unbound > 0);

        FluentActions.Invoking(() => Prepare(ctx, cmd))
            .Should().Throw<QueryPreparationException>().WithMessage("*Unbound*");
    }

    /// <summary>Referencing the result as a whole is not a HAVING predicate and fails closed.</summary>
    [Fact]
    public void SelectGroupHaving_WholeResultReference_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(
            e, (Expression<Func<Entity, ProjectedRow>>)(x => new ProjectedRow { Id = x.Id }), r => r != null);

        FluentActions.Invoking(() => Prepare(ctx, cmd))
            .Should().Throw<QueryPreparationException>().WithMessage("*whole*");
    }
}

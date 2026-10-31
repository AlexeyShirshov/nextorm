using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

public class T360CompositeKey
{
    public long A { get; set; }
    public int? B { get; set; }
}

public class T360CompositeOrderByTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static IDataContext Ctx() => PostgresTestContext.Create();

    [Fact]
    public void Anonymous_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Anonymous_Desc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Desc).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id).ThenByDescending(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_NewExpression_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int)).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_ValueTupleCreate_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => ValueTuple.Create(x.Id, x.Int)).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_TupleCreate_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => Tuple.Create(x.Id, x.Int)).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void MemberInit_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Propagates_PerKey()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id }));
        composite.Should().Contain("NULLS FIRST");
    }

    [Fact]
    public void JoinedSource_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var joined = ctx.From<IComplexEntity>().Join<IComplexEntity>((a, b) => a.Id == b.Id);
        var composite = SqlOf(ctx, joined.OrderBy(x => new { x.Item1.Id, x.Item2.Int }).Select(x => new { x.Item1.Id }));
        var chained = SqlOf(ctx, joined.OrderBy(x => x.Item1.Id).ThenBy(x => x.Item2.Int).Select(x => new { x.Item1.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void CteSource_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var cte = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("recent");
        var composite = SqlOf(ctx, ctx.From(cte).OrderBy(r => new { r.Id, r.Int }).Select(r => new { r.Id }));
        var chained = SqlOf(ctx, ctx.From(cte).OrderBy(r => r.Id).ThenBy(r => r.Int).Select(r => new { r.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NonExpandable_Throws()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        FluentActions.Invoking(() => SqlOf(ctx, e.OrderBy(x => new[] { x.Id, x.Id }).Select(x => new { x.Id })))
            .Should().Throw<NotSupportedException>();
    }
}

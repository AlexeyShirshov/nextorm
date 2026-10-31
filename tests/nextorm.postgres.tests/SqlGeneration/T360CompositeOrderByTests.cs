using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

public class T360CompositeKey
{
    public long A { get; set; }
    public int? B { get; set; }
}

public struct T360CompositeStruct
{
    public long A { get; set; }
    public int? B { get; set; }
}

public class T360CompositeOrderByTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText;

    private static IDataContext Ctx() => PostgresTestContext.Create();

    // ---------- Anonymous ----------

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

    // ---------- Tuple ----------

    [Fact]
    public void Tuple_NewValueTuple_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int)).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_NewValueTuple_Desc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), OrderDirection.Desc).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id).ThenByDescending(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_NewTuple_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new Tuple<long, int?>(x.Id, x.Int)).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void Tuple_NewTuple_Desc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new Tuple<long, int?>(x.Id, x.Int), OrderDirection.Desc).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id).ThenByDescending(x => x.Int).Select(x => new { x.Id }));
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

    // ---------- MemberInit ----------

    [Fact]
    public void MemberInit_Class_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void MemberInit_Class_Desc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, OrderDirection.Desc).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id).ThenByDescending(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void MemberInit_Struct_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeStruct { A = x.Id, B = x.Int }).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void MemberInit_Struct_Desc_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeStruct { A = x.Id, B = x.Int }, OrderDirection.Desc).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id).ThenByDescending(x => x.Int).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    // ---------- NullsPosition ----------

    [Fact]
    public void NullsPosition_Anonymous_Asc_First_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id, OrderDirection.Asc, NullsPosition.First).ThenBy(x => x.Int, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Anonymous_Asc_Last_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id, OrderDirection.Asc, NullsPosition.Last).ThenBy(x => x.Int, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Anonymous_Desc_First_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Desc, NullsPosition.First).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id, NullsPosition.First).ThenByDescending(x => x.Int, NullsPosition.First).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Anonymous_Desc_Last_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new { x.Id, x.Int }, OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id, NullsPosition.Last).ThenByDescending(x => x.Int, NullsPosition.Last).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Tuple_Asc_First_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id, OrderDirection.Asc, NullsPosition.First).ThenBy(x => x.Int, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_Tuple_Desc_Last_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id, NullsPosition.Last).ThenByDescending(x => x.Int, NullsPosition.Last).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_MemberInit_Asc_Last_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderBy(x => x.Id, OrderDirection.Asc, NullsPosition.Last).ThenBy(x => x.Int, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void NullsPosition_MemberInit_Desc_First_EqualsChained()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        var composite = SqlOf(ctx, e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, OrderDirection.Desc, NullsPosition.First).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, e.OrderByDescending(x => x.Id, NullsPosition.First).ThenByDescending(x => x.Int, NullsPosition.First).Select(x => new { x.Id }));
        composite.Should().Be(chained);
    }

    // ---------- Query shapes ----------

    [Fact]
    public void JoinedSource_InnerJoin_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var joined = ctx.From<IComplexEntity>().Join<IComplexEntity>((a, b) => a.Id == b.Id);
        var composite = SqlOf(ctx, joined.OrderBy(x => new { x.Item1.Id, x.Item2.Int }).Select(x => new { x.Item1.Id }));
        var chained = SqlOf(ctx, joined.OrderBy(x => x.Item1.Id).ThenBy(x => x.Item2.Int).Select(x => new { x.Item1.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void JoinedSource_LeftJoin_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var joined = ctx.From<IComplexEntity>().LeftJoin(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);
        var composite = SqlOf(ctx, joined.OrderBy(x => new { x.Item1.Id, x.Item2.Int }).Select(x => new { x.Item1.Id }));
        var chained = SqlOf(ctx, joined.OrderBy(x => x.Item1.Id).ThenBy(x => x.Item2.Int).Select(x => new { x.Item1.Id }));
        composite.Should().Be(chained);
    }

    [Fact]
    public void AliasChain_DerivedSource_Asc_EqualsChained()
    {
        using var ctx = Ctx();
        var derived = ctx.From<IComplexEntity>().Select(x => new { x.Id, x.Int });
        var src = ctx.From(derived);
        var composite = SqlOf(ctx, src.OrderBy(x => new { x.Id, x.Int }).Select(x => new { x.Id }));
        var chained = SqlOf(ctx, src.OrderBy(x => x.Id).ThenBy(x => x.Int).Select(x => new { x.Id }));
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

    // ---------- Negative ----------

    [Fact]
    public void NonExpandable_Throws()
    {
        using var ctx = Ctx();
        var e = ctx.From<IComplexEntity>();
        FluentActions.Invoking(() => SqlOf(ctx, e.OrderBy(x => new[] { x.Id, x.Id }).Select(x => new { x.Id })))
            .Should().Throw<NotSupportedException>();
    }
}

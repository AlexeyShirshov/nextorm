using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D384 U3: compile-time and runtime arity assertions for the source-less join overloads.
/// Every assignment below is a compile-time assertion on the declared return type; the runtime
/// <see cref="FluentAssertions"/> check guards against a wrong runtime instance.
/// </summary>
public class JoinChainArityTests
{
    [Fact]
    public void StringJoin_OnEntityBuilder_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r =
            ctx.From("a").Join("b", (s, c) => s["id"] == c["id"]);
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void LeftJoin_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r =
            ctx.From("a").LeftJoin("b", (s, c) => s["id"] == c["id"]);
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void RightJoin_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r =
            ctx.From("a").RightJoin("b", (s, c) => s["id"] == c["id"]);
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void FullJoin_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r =
            ctx.From("a").FullJoin("b", (s, c) => s["id"] == c["id"]);
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void CrossJoin_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r = ctx.From("a").CrossJoin("b");
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void CrossApply_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r = ctx.From("a").CrossApply("b");
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void OuterApply_String_Returns_JoinedEntityBuilder_TableAlias_TableAlias()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias> r = ctx.From("a").OuterApply("b");
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, TableAlias>>();
    }

    [Fact]
    public void TypeOnly_CrossJoin_OnEntityBuilder_Returns_TableAlias_T()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, SimpleEntity> r = ctx.From("a").CrossJoin<SimpleEntity>();
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, SimpleEntity>>();
    }

    [Fact]
    public void TypeOnly_CrossApply_OnEntityBuilder_Returns_TableAlias_T()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, SimpleEntity> r = ctx.From("a").CrossApply<SimpleEntity>();
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, SimpleEntity>>();
    }

    [Fact]
    public void TypeOnly_OuterApply_OnEntityBuilder_Returns_TableAlias_T()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, SimpleEntity> r = ctx.From("a").OuterApply<SimpleEntity>();
        r.Should().BeOfType<JoinedEntityBuilder<TableAlias, SimpleEntity>>();
    }

    [Fact]
    public void Chain_Mixed_RawAndTyped_ReachesArity8()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity, TableAlias, SimpleEntity, TableAlias, SimpleEntity, TableAlias> r =
            ctx.From("a")
               .Join("b", (s, c) => true)
               .CrossJoin<SimpleEntity>()
               .LeftJoin("d", (p, c) => true)
               .CrossApply<SimpleEntity>()
               .RightJoin("f", (p, c) => true)
               .CrossJoin<SimpleEntity>()
               .FullJoin("h", (p, c) => true);
        // The compile-time assignment above is the assertion; keep a runtime guard too.
        r.Should().NotBeNull();
    }

    [Fact]
    public void Options_Accepted_OnAllOperators()
    {
        using var ctx = new InMemoryDataContext();
        Action<JoinOptions> o = j => j.SuppressCartesianWarning();
        JoinedEntityBuilder<TableAlias, TableAlias> a = ctx.From("a").Join("b", (s, c) => s["id"] == c["id"], o);
        JoinedEntityBuilder<TableAlias, TableAlias> b = ctx.From("a").LeftJoin("b", (s, c) => s["id"] == c["id"], o);
        JoinedEntityBuilder<TableAlias, TableAlias> c2 = ctx.From("a").RightJoin("b", (s, c) => s["id"] == c["id"], o);
        JoinedEntityBuilder<TableAlias, TableAlias> d = ctx.From("a").FullJoin("b", (s, c) => s["id"] == c["id"], o);
        JoinedEntityBuilder<TableAlias, TableAlias> e = ctx.From("a").CrossJoin("b", o);
        JoinedEntityBuilder<TableAlias, TableAlias> f = ctx.From("a").CrossApply("b", o);
        JoinedEntityBuilder<TableAlias, TableAlias> g = ctx.From("a").OuterApply("b", o);
        // type-only options
        JoinedEntityBuilder<TableAlias, SimpleEntity> h = ctx.From("a").CrossJoin<SimpleEntity>(o);
        JoinedEntityBuilder<TableAlias, SimpleEntity> i = ctx.From("a").CrossApply<SimpleEntity>(o);
        JoinedEntityBuilder<TableAlias, SimpleEntity> j = ctx.From("a").OuterApply<SimpleEntity>(o);
        // on a joined receiver
        var r = ctx.From("a").Join("b", (s, c) => s["id"] == c["id"]);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> k = r.CrossJoin("c", o);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> l = r.CrossApply("c", o);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> m = r.OuterApply("c", o);
        a.Should().NotBeNull();
        m.Should().NotBeNull();
    }

    [Fact]
    public void NonGeneric_EntityBuilder_StringJoin_Compiles()
    {
        using var ctx = new InMemoryDataContext();
        var b = new EntityBuilder(ctx, "a");
        JoinedEntityBuilder<TableAlias, TableAlias> r = b.Join("t", (s, c) => s["id"] == c["id"]);
        r.Should().NotBeNull();
    }
    // ------------------------------------------------------------------
    // U3 extension: string-form + type-only surface on every arity class 2..7
    // ------------------------------------------------------------------

    [Fact]
    public void StringAndTypeOnly_OnArity2()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> j = r.Join("c", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> l = r.LeftJoin("c", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> ri = r.RightJoin("c", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> f = r.FullJoin("c", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> c = r.CrossJoin("c");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> ca = r.CrossApply("c");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> oa = r.OuterApply("c");
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void StringAndTypeOnly_OnArity3()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true).Join("c", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> j = r.Join("d", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> l = r.LeftJoin("d", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> ri = r.RightJoin("d", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> f = r.FullJoin("d", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> c = r.CrossJoin("d");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> ca = r.CrossApply("d");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> oa = r.OuterApply("d");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void StringAndTypeOnly_OnArity4()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true).Join("c", (p, t) => true).Join("d", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> j = r.Join("e", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> l = r.LeftJoin("e", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ri = r.RightJoin("e", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> f = r.FullJoin("e", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> c = r.CrossJoin("e");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ca = r.CrossApply("e");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> oa = r.OuterApply("e");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void StringAndTypeOnly_OnArity5()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true).Join("c", (p, t) => true).Join("d", (p, t) => true).Join("e", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> j = r.Join("f", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> l = r.LeftJoin("f", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ri = r.RightJoin("f", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> f = r.FullJoin("f", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> c = r.CrossJoin("f");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ca = r.CrossApply("f");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> oa = r.OuterApply("f");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void StringAndTypeOnly_OnArity6()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true).Join("c", (p, t) => true).Join("d", (p, t) => true).Join("e", (p, t) => true).Join("f", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> j = r.Join("g", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> l = r.LeftJoin("g", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ri = r.RightJoin("g", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> f = r.FullJoin("g", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> c = r.CrossJoin("g");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ca = r.CrossApply("g");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> oa = r.OuterApply("g");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void StringAndTypeOnly_OnArity7()
    {
        using var ctx = new InMemoryDataContext();
        var r = ctx.From("a").Join("b", (s, c) => true).Join("c", (p, t) => true).Join("d", (p, t) => true).Join("e", (p, t) => true).Join("f", (p, t) => true).Join("g", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> j = r.Join("h", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> l = r.LeftJoin("h", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ri = r.RightJoin("h", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> f = r.FullJoin("h", (p, t) => true);
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> c = r.CrossJoin("h");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> ca = r.CrossApply("h");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> oa = r.OuterApply("h");
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> ct = r.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> cat = r.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, SimpleEntity> oat = r.OuterApply<SimpleEntity>();
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        ct.Should().NotBeNull(); cat.Should().NotBeNull(); oat.Should().NotBeNull();
    }

    [Fact]
    public void Mixed_RawThenTyped()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity> r =
            ctx.From("a").Join("b", (s, c) => true).CrossJoin<SimpleEntity>();
        r.Should().NotBeNull();
    }

    [Fact]
    public void Mixed_TypedThenRaw()
    {
        using var ctx = new InMemoryDataContext();
        JoinedEntityBuilder<SimpleEntity, TableAlias, SimpleEntity> r =
            ctx.From<SimpleEntity>().Join("b", (s, c) => true).LeftJoin<SimpleEntity>((p, c) => true);
        r.Should().NotBeNull();
    }

    [Fact]
    public void NonGeneric_EntityBuilder_AllStringOps()
    {
        using var ctx = new InMemoryDataContext();
        var b = new EntityBuilder(ctx, "a");
        JoinedEntityBuilder<TableAlias, TableAlias> j = b.Join("t", (s, c) => s["id"] == c["id"]);
        JoinedEntityBuilder<TableAlias, TableAlias> l = b.LeftJoin("t", (s, c) => s["id"] == c["id"]);
        JoinedEntityBuilder<TableAlias, TableAlias> ri = b.RightJoin("t", (s, c) => s["id"] == c["id"]);
        JoinedEntityBuilder<TableAlias, TableAlias> f = b.FullJoin("t", (s, c) => s["id"] == c["id"]);
        JoinedEntityBuilder<TableAlias, TableAlias> c = b.CrossJoin("t");
        JoinedEntityBuilder<TableAlias, TableAlias> ca = b.CrossApply("t");
        JoinedEntityBuilder<TableAlias, TableAlias> oa = b.OuterApply("t");
        j.Should().NotBeNull(); l.Should().NotBeNull(); ri.Should().NotBeNull(); f.Should().NotBeNull();
        c.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
    }

    [Fact]
    public void NonGeneric_EntityBuilder_TypeOnlyOps()
    {
        using var ctx = new InMemoryDataContext();
        var b = new EntityBuilder(ctx, "a");
        JoinedEntityBuilder<TableAlias, SimpleEntity> cj = b.CrossJoin<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, SimpleEntity> ca = b.CrossApply<SimpleEntity>();
        JoinedEntityBuilder<TableAlias, SimpleEntity> oa = b.OuterApply<SimpleEntity>();

        Action<JoinOptions> o = j => j.SuppressCartesianWarning();
        JoinedEntityBuilder<TableAlias, SimpleEntity> cjo = b.CrossJoin<SimpleEntity>(o);
        JoinedEntityBuilder<TableAlias, SimpleEntity> cao = b.CrossApply<SimpleEntity>(o);
        JoinedEntityBuilder<TableAlias, SimpleEntity> oao = b.OuterApply<SimpleEntity>(o);

        var r = b.CrossJoin("t");
        JoinedEntityBuilder<TableAlias, TableAlias, SimpleEntity> chained = r.CrossJoin<SimpleEntity>();

        cj.Should().NotBeNull(); ca.Should().NotBeNull(); oa.Should().NotBeNull();
        cjo.Should().NotBeNull(); cao.Should().NotBeNull(); oao.Should().NotBeNull();
        chained.Should().NotBeNull();
    }
}

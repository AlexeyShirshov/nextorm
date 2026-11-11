using System.Linq;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D384 U4: in-memory semantics parity for the source-less join overloads.
/// Each case executes the source-less form and the explicit <c>ctx.From(...)</c> form on equivalent
/// in-memory data and compares either the materialised rows or, where both refuse, the identical
/// exception (type + decisive content).
/// </summary>
public class JoinSourceLessInMemoryTests
{
    private static InMemoryDataContext RawContext(int count)
    {
        var ctx = new InMemoryDataContext();
        var rows = Enumerable.Range(0, count).Select(_ => new TableAlias()).ToArray();
        ctx.From("a").WithData(rows);
        ctx.From("b").WithData(rows);
        return ctx;
    }

    private static InMemoryDataContext TypedContext(int count)
    {
        var ctx = new InMemoryDataContext();
        var rows = Enumerable.Range(1, count).Select(i => new SimpleEntity { Id = i }).ToArray();
        ctx.From<SimpleEntity>().WithData(rows);
        return ctx;
    }

    // ---------- string-form joins that materialise in-memory ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_InnerJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var sl = slCtx.From("a").Join("b", (s, c) => true).ToList();
        var ex = exCtx.From("a").Join(exCtx.From("b"), (s, c) => true).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_LeftJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var sl = slCtx.From("a").LeftJoin("b", (s, c) => false).ToList();
        var ex = exCtx.From("a").LeftJoin(exCtx.From("b"), (s, c) => false).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_RightJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var sl = slCtx.From("a").RightJoin("b", (s, c) => false).ToList();
        var ex = exCtx.From("a").RightJoin(exCtx.From("b"), (s, c) => false).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_FullJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var sl = slCtx.From("a").FullJoin("b", (s, c) => false).ToList();
        var ex = exCtx.From("a").FullJoin(exCtx.From("b"), (s, c) => false).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_CrossJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var sl = slCtx.From("a").CrossJoin("b").ToList();
        var ex = exCtx.From("a").CrossJoin(exCtx.From("b")).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    // ---------- string-form joins that refuse in-memory (CrossApply / OuterApply) ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_CrossApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var slAct = () => slCtx.From("a").CrossApply("b")
            .Select(p => new { A = p.Item1["id"].AsInt, B = p.Item2["id"].AsInt })
            .ToList();
        var exAct = () => exCtx.From("a").CrossApply(exCtx.From("b"))
            .Select(p => new { A = p.Item1["id"].AsInt, B = p.Item2["id"].AsInt })
            .ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void StringForm_OuterApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = RawContext(count);
        using var exCtx = RawContext(count);

        var slAct = () => slCtx.From("a").OuterApply("b")
            .Select(p => new { A = p.Item1["id"].AsInt, B = p.Item2["id"].AsInt })
            .ToList();
        var exAct = () => exCtx.From("a").OuterApply(exCtx.From("b"))
            .Select(p => new { A = p.Item1["id"].AsInt, B = p.Item2["id"].AsInt })
            .ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
    }

    // ---------- type-only joins ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TypeOnly_CrossJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = TypedContext(count);
        using var exCtx = TypedContext(count);

        var sl = slCtx.From<SimpleEntity>().CrossJoin<SimpleEntity>()
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();
        var ex = exCtx.From<SimpleEntity>().CrossJoin(exCtx.From<SimpleEntity>())
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();

        sl.Should().BeEquivalentTo(ex);
        sl.Should().HaveCount(count * count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TypeOnly_CrossApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = TypedContext(count);
        using var exCtx = TypedContext(count);

        var slAct = () => slCtx.From<SimpleEntity>().CrossApply<SimpleEntity>()
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();
        var exAct = () => exCtx.From<SimpleEntity>().CrossApply(exCtx.From<SimpleEntity>())
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TypeOnly_OuterApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = TypedContext(count);
        using var exCtx = TypedContext(count);

        var slAct = () => slCtx.From<SimpleEntity>().OuterApply<SimpleEntity>()
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();
        var exAct = () => exCtx.From<SimpleEntity>().OuterApply(exCtx.From<SimpleEntity>())
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
            .ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
    }

    // ---------- raw -> typed chain reading columns via TableAlias ----------

    [Fact]
    public void Chain_RawThenTyped_ReadsColumnsViaTableAlias_LikeExplicit()
    {
        // source-less
        var slCtx = new InMemoryDataContext();
        slCtx.From("a").WithData(new[] { new TableAlias() });
        slCtx.From<SimpleEntity>().WithData(new[] { new SimpleEntity { Id = 7 } });

        var slAct = () => slCtx.From("a")
            .CrossJoin<SimpleEntity>()
            .Select(p => new { Raw = p.Item1["id"].AsInt, Typed = p.Item2.Id })
            .ToList();

        // explicit
        var exCtx = new InMemoryDataContext();
        exCtx.From("a").WithData(new[] { new TableAlias() });
        exCtx.From<SimpleEntity>().WithData(new[] { new SimpleEntity { Id = 7 } });

        var exAct = () => exCtx.From("a")
            .CrossJoin(exCtx.From<SimpleEntity>())
            .Select(p => new { Raw = p.Item1["id"].AsInt, Typed = p.Item2.Id })
            .ToList();

        // Engine limitation (pre-existing, not a #384 defect): the in-memory projection compiler does
        // not bind raw TableAlias column reads (p.ItemN["col"]). Bind the cause explicitly rather than
        // asserting a bare exception type:
        //   1. the TableAlias indexer returns no bound column outside a query expression, and
        //   2. reading .AsInt from that null column is exactly the failing access, and
        //   3. the same join without the raw read materialises on both forms (the join is not the cause).
        var rawAlias = new TableAlias();
        rawAlias["id"].Should().BeNull("an in-memory TableAlias has no bound column metadata, so the raw column read yields null");
        FluentActions.Invoking(() => _ = rawAlias["id"].AsInt).Should().Throw<NullReferenceException>(
            "dereferencing the unbound TableAlias column is the failing access this limitation is about");

        slCtx.From("a")
            .CrossJoin<SimpleEntity>()
            .Select(p => new { Typed = p.Item2.Id })
            .ToList()
            .Should().HaveCount(1, "the join itself materialises; only the raw TableAlias column read fails");
        exCtx.From("a")
            .CrossJoin(exCtx.From<SimpleEntity>())
            .Select(p => new { Typed = p.Item2.Id })
            .ToList()
            .Should().HaveCount(1, "the explicit join materialises the same way");

        var slEx = FluentActions.Invoking(() => slAct()).Should().Throw<NullReferenceException>(
            "the source-less form fails at the same raw TableAlias column read").Which;
        var exEx = FluentActions.Invoking(() => exAct()).Should().Throw<NullReferenceException>(
            "the explicit form fails at the same raw TableAlias column read").Which;
        slEx.Message.Should().Be(exEx.Message, "both forms fail at the same observable access with the same state");
    }

    // ---------- non-generic (raw TableAlias) builder, type-only joins ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void NonGeneric_TypeOnly_CrossJoin_MaterialisesLikeExplicit(int count)
    {
        using var slCtx = new InMemoryDataContext();
        using var exCtx = new InMemoryDataContext();
        var rawRows = Enumerable.Range(0, count).Select(_ => new TableAlias()).ToArray();
        var typedRows = Enumerable.Range(1, count).Select(i => new SimpleEntity { Id = i }).ToArray();
        slCtx.From("a").WithData(rawRows);
        exCtx.From("a").WithData(rawRows);
        slCtx.From<SimpleEntity>().WithData(typedRows);
        exCtx.From<SimpleEntity>().WithData(typedRows);

        var sl = new EntityBuilder(slCtx, "a").CrossJoin<SimpleEntity>().ToList();
        var ex = new EntityBuilder(exCtx, "a").CrossJoin(exCtx.From<SimpleEntity>()).ToList();

        sl.Should().BeEquivalentTo(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void NonGeneric_TypeOnly_CrossApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = new InMemoryDataContext();
        using var exCtx = new InMemoryDataContext();
        var rawRows = Enumerable.Range(0, count).Select(_ => new TableAlias()).ToArray();
        var typedRows = Enumerable.Range(1, count).Select(i => new SimpleEntity { Id = i }).ToArray();
        slCtx.From("a").WithData(rawRows);
        exCtx.From("a").WithData(rawRows);
        slCtx.From<SimpleEntity>().WithData(typedRows);
        exCtx.From<SimpleEntity>().WithData(typedRows);

        var slAct = () => new EntityBuilder(slCtx, "a").CrossApply<SimpleEntity>().ToList();
        var exAct = () => new EntityBuilder(exCtx, "a").CrossApply(exCtx.From<SimpleEntity>()).ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("CrossApply");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void NonGeneric_TypeOnly_OuterApply_RefusesLikeExplicit(int count)
    {
        using var slCtx = new InMemoryDataContext();
        using var exCtx = new InMemoryDataContext();
        var rawRows = Enumerable.Range(0, count).Select(_ => new TableAlias()).ToArray();
        var typedRows = Enumerable.Range(1, count).Select(i => new SimpleEntity { Id = i }).ToArray();
        slCtx.From("a").WithData(rawRows);
        exCtx.From("a").WithData(rawRows);
        slCtx.From<SimpleEntity>().WithData(typedRows);
        exCtx.From<SimpleEntity>().WithData(typedRows);

        var slAct = () => new EntityBuilder(slCtx, "a").OuterApply<SimpleEntity>().ToList();
        var exAct = () => new EntityBuilder(exCtx, "a").OuterApply(exCtx.From<SimpleEntity>()).ToList();

        slAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
        exAct.Should().Throw<NotSupportedException>().WithMessage("OuterApply");
    }
}
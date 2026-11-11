using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// M1 of the raw-handle migration: the shape identity that makes a native plan hit possible when the
/// caller hoists a fresh captured value per call. The hash indexes candidates; equality is proven by
/// comparing the fresh graph against the stored token stream, which never retains the closure.
/// </summary>
public class ShapeIdentityTests
{
    private sealed class ShapeEntity
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public string? Name { get; set; }
    }

    private sealed class Probe
    {
        public int Value { get; set; }
    }

    private sealed class ProbeHolder
    {
        public Probe First { get; set; } = new();
        public Probe Second { get; set; } = new();
    }

    // Same call site, so each invocation produces a fresh compiler-generated display class: the value
    // changes identity, the shape does not.
    private static Expression<Func<ShapeEntity, bool>> CapturedGreater(int value) => x => x.Id > value;

    private static Expression<Func<ShapeEntity, bool>> CapturedLess(int value) => x => x.Id < value;

    private static Expression<Func<ShapeEntity, bool>> CapturedTwo(Probe a, Probe b) => x => x.Id > a.Value && x.Id < b.Value;

    private static Expression<Func<ShapeEntity, bool>> CapturedMember(Probe probe) => x => x.Id > probe.Value;

    private static Expression<Func<ShapeEntity, bool>> FirstPath(ProbeHolder holder) => x => x.Id > holder.First.Value;

    private static Expression<Func<ShapeEntity, bool>> SecondPath(ProbeHolder holder) => x => x.Id > holder.Second.Value;

    private static readonly ShapeHeader Header = new(typeof(ShapeEntity), typeof(bool), typeof(object), 1);

    private static ShapeKey Create(Expression<Func<ShapeEntity, bool>> expression)
    {
        var key = ShapeIdentity.TryCreate(Header, expression);
        key.Should().NotBeNull("the captured-scalar shape must be keyable");
        return key!;
    }

    [Fact]
    public void FreshCapturedValue_ShouldYieldTheSameShape()
    {
        var first = Create(CapturedGreater(1));
        var second = Create(CapturedGreater(2));

        ReferenceEquals(first, second).Should().BeFalse("each call creates its own tokens");
        second.Hash.Should().Be(first.Hash, "the captured value is not part of the shape");
        ShapeIdentity.Matches(first, Header, CapturedGreater(3)).Should().BeTrue(
            "a fresh closure with a different value has the same shape, so the same plan is valid");
    }

    [Fact]
    public void DifferentOperator_ShouldYieldADifferentShape()
    {
        var greater = Create(CapturedGreater(1));
        var less = Create(CapturedLess(1));

        ShapeIdentity.Matches(greater, Header, CapturedLess(1)).Should().BeFalse(
            "the operator is part of the rendered SQL");
        less.Hash.Should().NotBe(greater.Hash);
    }

    [Fact]
    public void DifferentLiteralValue_ShouldYieldADifferentShape()
    {
        var five = Create(x => x.Id > 5);
        var six = Create(x => x.Id > 6);

        ShapeIdentity.Matches(five, Header, (Expression<Func<ShapeEntity, bool>>)(x => x.Id > 6)).Should().BeFalse(
            "a literal that changes the rendered SQL remains part of the identity");
        six.Hash.Should().NotBe(five.Hash);
    }

    [Fact]
    public void DifferentProjection_ShouldYieldADifferentShape()
    {
        var id = ShapeIdentity.TryCreate(new ShapeHeader(typeof(ShapeEntity), typeof(int), typeof(object), 1), (Expression<Func<ShapeEntity, int>>)(x => x.Id));
        var name = ShapeIdentity.TryCreate(new ShapeHeader(typeof(ShapeEntity), typeof(string), typeof(object), 1), (Expression<Func<ShapeEntity, string?>>)(x => x.Name));

        id.Should().NotBeNull();
        name.Should().NotBeNull();
        ShapeIdentity.Matches(id!, new ShapeHeader(typeof(ShapeEntity), typeof(int), typeof(object), 1), (Expression<Func<ShapeEntity, int>>)(x => x.Id + 1)).Should().BeFalse();
    }

    [Fact]
    public void CapturedCollection_ShouldFailClosed()
    {
        var ids = new[] { 1, 2, 3 };
        Expression<Func<ShapeEntity, bool>> indexed = x => x.Id == ids[0];
        Expression<Func<ShapeEntity, bool>> contains = x => ids.Contains(x.Id);

        ShapeIdentity.TryCreate(Header, indexed).Should().BeNull(
            "a captured array is the uncacheable shape the baseline keeps on the legacy route");
        ShapeIdentity.TryCreate(Header, contains).Should().BeNull();
    }

    [Fact]
    public void NestedCommand_ShouldFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        var nested = ctx.From<ShapeEntity>().ToCommand();
        Expression<Func<ShapeEntity, bool>> expression = x => x.Id > nested.GetHashCode();

        ShapeIdentity.TryCreate(Header, expression).Should().BeNull(
            "a nested command changes the rendered statement in ways this identity does not model");
    }

    [Fact]
    public void CapturedMembers_ShouldBeRecordedInCanonicalOrder()
    {
        var a = new Probe { Value = 1 };
        var b = new Probe { Value = 2 };

        var key = Create(CapturedTwo(a, b));

        key.Captures.Should().HaveCount(2);
        key.Captures[0].Name.Should().Be("Value");
        key.Captures[0].ValueType.Should().Be(typeof(int));
        key.Captures[0].Path.Select(m => m.Name).Should().Equal("a", "Value");
        key.Captures[1].Name.Should().Be("Value");
        key.Captures[1].Path.Select(m => m.Name).Should().Equal("b", "Value");
        key.Captures[0].ClosureType.Should().Be(key.Captures[1].ClosureType);
    }

    [Fact]
    public void NestedMemberCapture_ShouldRecordTheFullPath()
    {
        var probe = new Probe { Value = 7 };
        Expression<Func<ShapeEntity, bool>> expression = x => x.Id > probe.Value;

        var key = Create(expression);

        key.Captures.Should().ContainSingle();
        key.Captures[0].Path.Select(m => m.Name).Should().Equal("probe", "Value");
        key.Captures[0].ClosureType.Name.Should().Contain("DisplayClass",
            "the capture root is the compiler-generated closure, not the probe object");
    }

    [Fact]
    public void Matches_ShouldBeAllocationFree()
    {
        var published = Create(CapturedGreater(1));
        Expression<Func<ShapeEntity, bool>> fresh = CapturedGreater(2);

        for (var i = 0; i < 20; i++)
            _ = ShapeIdentity.Matches(published, Header, CapturedGreater(i));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var matched = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            matched &= ShapeIdentity.Matches(published, Header, fresh);
        var allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / 100.0;

        matched.Should().BeTrue();
        allocated.Should().Be(0, "the hit path walks the fresh graph and compares value-type tokens");
    }

    [Fact]
    public void NullRoot_ShouldYieldAnEmptyShape()
    {
        var key = ShapeIdentity.TryCreate(Header, (Expression<Func<ShapeEntity, bool>>?)null);

        key.Should().NotBeNull("a null root is a stable empty shape, not an unkeyable graph");
        key!.Captures.Should().BeEmpty();
        key.Tokens.Should().HaveCountGreaterThan(1, "the header tokens are always present");
    }

    private sealed class ShapeDto
    {
        public int First { get; set; }
        public int Second { get; set; }
    }

    private static readonly ShapeHeader ProjectionHeader = new(typeof(ShapeEntity), typeof(string), typeof(object), 2);

    private static ShapeKey CreateProjection(Expression<Func<ShapeEntity, string?>> projection)
    {
        var key = ShapeIdentity.TryCreate(ProjectionHeader, null, null, projection);
        key.Should().NotBeNull("a plain projection shape must be keyable");
        return key!;
    }

    [Fact]
    public void DifferentMethodCall_ShouldYieldADifferentShape()
    {
        // The invoked function is the statement: sha1 vs md5, upper vs lower. A shape that ignored the
        // method would let one function's compiled statement answer another function's query.
        var upper = CreateProjection(x => x.Name!.ToUpper());
        var lower = CreateProjection(x => x.Name!.ToLower());

        upper.Hash.Should().NotBe(lower.Hash, "the method takes part in the structural hash");
        ShapeIdentity.Matches(upper, ProjectionHeader, null, null, (Expression<Func<ShapeEntity, string?>>)(x => x.Name!.ToLower()))
            .Should().BeFalse("a different method is a different shape");
        ShapeIdentity.Matches(upper, ProjectionHeader, null, null, (Expression<Func<ShapeEntity, string?>>)(x => x.Name!.ToUpper()))
            .Should().BeTrue("the same method is the same shape");
    }

    [Fact]
    public void DifferentEntityColumn_ShouldYieldADifferentShape()
    {
        var id = Create(x => x.Id > 5);
        var value = Create(x => x.Value > 5);

        value.Hash.Should().NotBe(id.Hash, "the selected column is part of the shape");
        ShapeIdentity.Matches(id, Header, (Expression<Func<ShapeEntity, bool>>)(x => x.Value > 5))
            .Should().BeFalse("a different column is a different statement");
    }

    [Fact]
    public void DifferentCapturePath_ShouldYieldADifferentShape()
    {
        var first = Create(FirstPath(new ProbeHolder()));
        var second = Create(SecondPath(new ProbeHolder()));

        second.Hash.Should().NotBe(first.Hash, "the member path from the closure names the column");
    }

    [Fact]
    public void DifferentMemberInitBinding_ShouldYieldADifferentShape()
    {
        var first = ShapeIdentity.TryCreate(
            new ShapeHeader(typeof(ShapeEntity), typeof(ShapeDto), typeof(object), 2),
            null,
            null,
            (Expression<Func<ShapeEntity, ShapeDto>>)(x => new ShapeDto { First = x.Id }));
        var second = ShapeIdentity.TryCreate(
            new ShapeHeader(typeof(ShapeEntity), typeof(ShapeDto), typeof(object), 2),
            null,
            null,
            (Expression<Func<ShapeEntity, ShapeDto>>)(x => new ShapeDto { Second = x.Id }));

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.Hash.Should().NotBe(second!.Hash, "the bound member names the projected column");
    }
}

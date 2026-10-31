using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public class T360MemEntity
{
    public int Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
}

public class T360KeyDto
{
    public T360KeyDto() { }
    public T360KeyDto(int a) { A = a; }
    public T360KeyDto(int a, int b) { A = a; B = b; }
    public int A { get; set; }
    public int B { get; set; }
}

public class CompositeOrderByTests
{
    private static InMemoryDataContext CreateContext(params T360MemEntity[] data)
    {
        var ctx = new InMemoryDataContext();
        ctx.From<T360MemEntity>().WithData(data);
        return ctx;
    }

    [Fact]
    public void AnonymousComposite_OrdersByMembersInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new { x.B, x.A })
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void TupleComposite_OrdersByMembersInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new ValueTuple<int, int>(x.B, x.A))
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void TupleCreate_OrdersByMembersInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => Tuple.Create(x.B, x.A))
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void ValueTupleCreate_OrdersByMembersInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => ValueTuple.Create(x.B, x.A))
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void MemberInitComposite_OrdersByBindingsInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new T360KeyDto { B = x.B, A = x.A })
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void SingleKey_Regression()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2 },
            new T360MemEntity { Id = 2, A = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => x.A)
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(2, 1);
    }

    [Fact]
    public void NonExpandable_ConstructorWithArgs_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360KeyDto(x.A, x.B)))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_EmptyAnonymous_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new { }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_EmptyTuple_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => ValueTuple.Create()))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_EmptyMemberInit_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360KeyDto { }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_Array_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new[] { x.A, x.B }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_MixedMemberInit_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360KeyDto(x.A) { B = x.B }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DirectionAndNulls_Propagate()
    {
        // Descending composite
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new { x.B, x.A }, OrderDirection.Desc)
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(2, 1, 3);
    }

    [Fact]
    public void MemberInit_WithConvertWrapper_Works()
    {
        // struct scenario: KeyDto is class here; ensure no throw
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new T360KeyDto { A = x.A, B = x.B })
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(2, 1);
    }
}

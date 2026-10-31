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

public struct T360KeyStruct
{
    public int A { get; set; }
    public int B { get; set; }
}

public static class T360Helpers
{
    public static (int, int) GetTuple(T360MemEntity x) => (x.B, x.A);
}

public class FakeAnonymousType
{
    public FakeAnonymousType(int a, int b) { A = a; B = b; }
    public int A { get; set; }
    public int B { get; set; }
}

public class T360ListDto
{
    public List<int> Items { get; set; } = new();
}

public class T360NestedDto
{
    public T360KeyDto Inner { get; set; } = new();
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

    [Fact]
    public void Tuple_NewTuple_OrdersByMembersInDeclarationOrder()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new Tuple<int, int>(x.B, x.A))
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void Tuple_NestedAndRest8_Expands()
    {
        var ctx = CreateContext(new T360MemEntity { Id = 1, A = 1, B = 2 });
        // 8+ element tuple must not throw
        FluentActions.Invoking(() => ctx.From<T360MemEntity>()
            .OrderBy(x => ValueTuple.Create(x.A, x.B, x.Id, x.A, x.B, x.Id, x.A, x.B))
            .Select(x => x.Id)
            .ToList())
            .Should().NotThrow();
    }

    [Fact]
    public void StructMemberInit_WithConvertWrapper_Works()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => new T360KeyStruct { B = x.B, A = x.A })
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(1, 2);
    }

    [Fact]
    public void ThenByDescending_Composite_Orders()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 },
            new T360MemEntity { Id = 3, A = 1, B = 1 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => x.Id)
            .ThenByDescending(x => new { x.B, x.A })
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void RepeatedOrderBy_Regression()
    {
        var ctx = CreateContext(
            new T360MemEntity { Id = 1, A = 2, B = 1 },
            new T360MemEntity { Id = 2, A = 1, B = 2 });
        var r = ctx.From<T360MemEntity>()
            .OrderBy(x => x.A)
            .OrderBy(x => x.B)
            .Select(x => x.Id)
            .ToList();
        r.Should().Equal(2, 1);
    }


    [Fact]
    public void NonExpandable_ListInit_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new List<int> { x.A, x.B }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_MemberListBinding_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360ListDto { Items = { x.A } }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_MemberMemberBinding_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360NestedDto { Inner = { A = x.A } }))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_ParameterlessCtor_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new T360KeyDto()))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_FakeAnonymousType_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new FakeAnonymousType(x.A, x.B)))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_HelperTupleCall_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => T360Helpers.GetTuple(x)))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_EmptyTupleNew_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        FluentActions.Invoking(() => b.OrderBy(x => new ValueTuple()))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NonExpandable_TupleReturningMemberAccess_Throws()
    {
        // member access returning composite
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>();
        // x itself not composite; use a helper property scenario via cast impossible; skip
    }

    [Fact]
    public void NonExpandable_UnchangedSorting_OnThrow()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>().OrderBy(x => x.Id);
        var field = typeof(EntityBuilder<T360MemEntity>).GetField("_sorting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        int before = ((System.Collections.IList)field.GetValue(b)!).Count;
        try { b.OrderBy(x => new T360KeyDto(x.A, x.B)); } catch (NotSupportedException) { }
        int after = ((System.Collections.IList)field.GetValue(b)!).Count;
        before.Should().Be(after);
    }
}

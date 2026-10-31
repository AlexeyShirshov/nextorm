using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;
using System;
using System.Threading;

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
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>()
            .OrderBy(x => ValueTuple.Create(x.A, x.B, x.Id, x.A, x.B, x.Id, x.A, ValueTuple.Create(x.B, x.Id)));
        composite.Sorting.Should().HaveCount(9);
        var expected = new[]
        {
            "x => Convert(x.A, Object)",
            "x => Convert(x.B, Object)",
            "x => Convert(x.Id, Object)",
            "x => Convert(x.A, Object)",
            "x => Convert(x.B, Object)",
            "x => Convert(x.Id, Object)",
            "x => Convert(x.A, Object)",
            "x => Convert(x.B, Object)",
            "x => Convert(x.Id, Object)",
        };
        for (var i = 0; i < expected.Length; i++)
        {
            composite.Sorting![i].SortExpression!.ToString().Should().Be(expected[i]);
            composite.Sorting![i].Direction.Should().Be(OrderDirection.Asc);
            composite.Sorting![i].NullsPosition.Should().Be(NullsPosition.Default);
        }

        var compCmd = composite.ToCommand();
        ctx.GetPreparedQueryCommand(compCmd, false, false, CancellationToken.None);
        for (var i = 0; i < expected.Length; i++)
            compCmd.Sorting![i].PreparedExpression!.ToString().Should().Be(expected[i]);

        var chained = ctx.From<T360MemEntity>()
            .OrderBy(x => x.A).ThenBy(x => x.B).ThenBy(x => x.Id)
            .ThenBy(x => x.A).ThenBy(x => x.B).ThenBy(x => x.Id)
            .ThenBy(x => x.A).ThenBy(x => x.B).ThenBy(x => x.Id);
        AssertCompositeMatchesChained(ctx, composite, chained);
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
    public void NonExpandable_MessageNamesShape_Throws()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>().OrderBy(x => x.Id);
        FluentActions.Invoking(() => b.ThenBy(x => T360Helpers.GetTuple(x)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*non-expandable method call 'GetTuple' returning composite*");
    }

    [Fact]
    public void NonExpandable_UnchangedSorting_OnThrow()
    {
        var ctx = new InMemoryDataContext();
        var b = ctx.From<T360MemEntity>().OrderBy(x => x.Id);
        var beforeList = b.Sorting;
        var before = b.Sorting!.ToArray();
        FluentActions.Invoking(() => b.OrderBy(x => new T360KeyDto(x.A, x.B)))
            .Should().Throw<NotSupportedException>();
        var after = b.Sorting!.ToArray();
        b.Sorting.Should().BeSameAs(beforeList);
        before.Length.Should().Be(after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            before[i].Direction.Should().Be(after[i].Direction);
            before[i].NullsPosition.Should().Be(after[i].NullsPosition);
            before[i].ColumnIndex.Should().Be(after[i].ColumnIndex);
            before[i].SortExpression?.ToString().Should().Be(after[i].SortExpression?.ToString());
            before[i].PreparedExpression?.ToString().Should().Be(after[i].PreparedExpression?.ToString());
        }
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, EntityBuilder<T> builder)
        => Normalize(Prepare(ctx, builder.ToCommand()).DbCommand.CommandText);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static void AssertCompositeMatchesChained(InMemoryDataContext ctx, EntityBuilder<T360MemEntity> composite, EntityBuilder<T360MemEntity> chained)
    {
        composite.Sorting.Should().NotBeNull();
        chained.Sorting.Should().NotBeNull();
        composite.Sorting!.Count.Should().Be(chained.Sorting!.Count);
        var compCmd = composite.ToCommand();
        var chainCmd = chained.ToCommand();
        ctx.GetPreparedQueryCommand(compCmd, false, false, CancellationToken.None);
        ctx.GetPreparedQueryCommand(chainCmd, false, false, CancellationToken.None);
        var compComparer = compCmd.GetSortingExpressionPlanEqualityComparer();
        var chainComparer = chainCmd.GetSortingExpressionPlanEqualityComparer();
        for (var i = 0; i < compCmd.Sorting!.Length; i++)
        {
            var cs = compCmd.Sorting[i];
            var ks = chainCmd.Sorting![i];
            cs.Direction.Should().Be(ks.Direction, $"key {i} Direction");
            cs.NullsPosition.Should().Be(ks.NullsPosition, $"key {i} NullsPosition");
            cs.PreparedExpression!.ToString().Should().Be(ks.PreparedExpression!.ToString(), $"key {i} PreparedExpression");
            compComparer.Equals(cs, ks).Should().BeTrue($"key {i} comparer Equals");
            compComparer.GetHashCode(cs).Should().Be(chainComparer.GetHashCode(ks), $"key {i} comparer GetHashCode");
        }
    }

    // ---- AST / plan-key assertions (T360-C4, AC-T360-03.1, AC-T360-05.1) -------------------

    [Fact]
    public void Composite_AstPlanKey_Anonymous()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => new { x.B, x.A });
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_ValueTupleLiteral()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => new ValueTuple<int, int>(x.B, x.A));
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_ValueTupleCreate()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => ValueTuple.Create(x.B, x.A));
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_TupleNew()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => new Tuple<int, int>(x.B, x.A));
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_TupleCreate()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => Tuple.Create(x.B, x.A));
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_MemberInitClass()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => new T360KeyDto { B = x.B, A = x.A });
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    [Fact]
    public void Composite_AstPlanKey_MemberInitStruct()
    {
        var ctx = new InMemoryDataContext();
        var composite = ctx.From<T360MemEntity>().OrderBy(x => new T360KeyStruct { B = x.B, A = x.A });
        var chained = ctx.From<T360MemEntity>().OrderBy(x => x.B).ThenBy(x => x.A);
        AssertCompositeMatchesChained(ctx, composite, chained);
    }

    // ---- scalar-construction preservation ---------------------------------------------------

    [Fact]
    public void ScalarConstruction_PreservedAsSingleSorting()
    {
        var ctx = new InMemoryDataContext();

        var b1 = ctx.From<T360MemEntity>().OrderBy(x => new DateTime(2020, 1, 1));
        b1.Sorting.Should().HaveCount(1);
        var c1 = b1.ToCommand();
        ctx.GetPreparedQueryCommand(c1, false, false, CancellationToken.None);
        c1.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new DateTime(2020, 1, 1), Object)");

        var b2 = ctx.From<T360MemEntity>().OrderBy(x => new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        b2.Sorting.Should().HaveCount(1);
        var c2 = b2.ToCommand();
        ctx.GetPreparedQueryCommand(c2, false, false, CancellationToken.None);
        c2.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), Object)");

        var b3 = ctx.From<T360MemEntity>().OrderBy(x => new DateOnly(2020, 1, 1));
        b3.Sorting.Should().HaveCount(1);
        var c3 = b3.ToCommand();
        ctx.GetPreparedQueryCommand(c3, false, false, CancellationToken.None);
        c3.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new DateOnly(2020, 1, 1), Object)");

        var b4 = ctx.From<T360MemEntity>().OrderBy(x => new TimeOnly(1, 2, 3));
        b4.Sorting.Should().HaveCount(1);
        var c4 = b4.ToCommand();
        ctx.GetPreparedQueryCommand(c4, false, false, CancellationToken.None);
        c4.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new TimeOnly(1, 2, 3), Object)");

        var b5 = ctx.From<T360MemEntity>().OrderBy(x => new TimeSpan(1, 2, 3, 4));
        b5.Sorting.Should().HaveCount(1);
        var c5 = b5.ToCommand();
        ctx.GetPreparedQueryCommand(c5, false, false, CancellationToken.None);
        c5.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new TimeSpan(1, 2, 3, 4), Object)");

        var b6 = ctx.From<T360MemEntity>().OrderBy(x => new Guid("11111111-2222-3333-4444-555555555555"));
        b6.Sorting.Should().HaveCount(1);
        var c6 = b6.ToCommand();
        ctx.GetPreparedQueryCommand(c6, false, false, CancellationToken.None);
        c6.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new Guid(\"11111111-2222-3333-4444-555555555555\"), Object)");

        var b7 = ctx.From<T360MemEntity>().OrderBy(x => new decimal(1.5));
        b7.Sorting.Should().HaveCount(1);
        var c7 = b7.ToCommand();
        ctx.GetPreparedQueryCommand(c7, false, false, CancellationToken.None);
        c7.Sorting![0].PreparedExpression!.ToString().Should().Be("x => Convert(new Decimal(1.5), Object)");
    }

    // ---- ordinal / repeated OrderBy preservation ------------------------------------------

    private const string SqlServerPlaceholder = "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    [Fact]
    public void OrdinalOrderBy_PreservesSql()
    {
        using var ctx = new SqlServerDataContext(SqlServerPlaceholder, new DataContextBuilder());
        var builder = ctx.From<T360MemEntity>().OrderBy(1);
        builder.Sorting.Should().HaveCount(1);
        builder.Sorting![0].ColumnIndex.Should().Be(1);
        var sql = SqlOf(ctx, builder);
        sql.Should().Be("select Id, A, B\n from T360MemEntity\n order by 1");
    }

    [Fact]
    public void RepeatedOrderBy_PreservesSql()
    {
        using var ctx = new SqlServerDataContext(SqlServerPlaceholder, new DataContextBuilder());
        var builder = ctx.From<T360MemEntity>().OrderBy(x => x.A).OrderBy(x => x.B);
        builder.Sorting.Should().HaveCount(2);
        var sql = SqlOf(ctx, builder);
        sql.Should().Be("select Id, A, B\n from T360MemEntity\n order by A, B");
    }
}

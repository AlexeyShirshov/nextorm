using System.Linq.Expressions;
using FluentAssertions;

namespace nextorm.core.tests;

public class SqlCommandTests
{
    [Fact]
    public void Expression_Test()
    {
        Expression<Func<int, int, TestEntity>> exp = (id, p) => new TestEntity(id, p);

        var l = Expression.Lambda(exp.Body, exp.Parameters[0], exp.Parameters[1]).Compile();

        var r = l.DynamicInvoke(1, 2) as TestEntity;

        r.Should().NotBeNull();
        r!.Id.Should().Be(1);
        r.P.Should().Be(2);
    }
    [Fact]
    public void Expression_Test2()
    {
        Expression<Func<Ctor, TestEntity>> exp = (c) => new TestEntity(c.GetId(), c.GetP());

        var l = Expression.Lambda(exp.Body, exp.Parameters[0]).Compile();

        var r = l.DynamicInvoke(new Ctor()) as TestEntity;

        r.Should().NotBeNull();
        r!.Id.Should().Be(1);
        r.P.Should().Be(2);
    }
    [Fact]
    public void Expression_Test3()
    {
        Expression<Func<Ctor, TestEntity>> exp = (c) => new TestEntity(c.IsSome() ? null : c.GetP());

        var l = Expression.Lambda(exp.Body, exp.Parameters[0]).Compile();

        var r = l.DynamicInvoke(new Ctor()) as TestEntity;

        r.Should().NotBeNull();
    }
    [Fact]
    public void TestTypename()
    {
        // Given
        var name = typeof(Projection<,>).FullName!;
        // When
        var type = typeof(Projection<,>).Assembly.GetType(name);
        // Then
        type.Should().NotBeNull();
    }
}
internal sealed class Ctor
{
    public int GetId() => 1;
    public int GetP() => 2;
    public bool IsSome() => true;
}
public class TestEntity
{
    public TestEntity()
    {
    }
    public TestEntity(int? r)
    {
    }
    public TestEntity(int id, int p)
    {
        Id = id;
        P = p;
    }

    public int Id { get; set; }
    public int P { get; }
}
